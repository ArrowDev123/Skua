using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Skua.Core.Interfaces;
using Skua.Core.Services;
using Skua.Core.Utils;
using System.Collections.ObjectModel;

namespace Skua.Core.ViewModels.Manager;

public partial class MessagingViewModel : BotControlViewModelBase, IDisposable
{
    private readonly ClientIpcServer _ipcServer;
    private readonly IDispatcherService _dispatcher;
    private bool _disposed;
    private const int MaxMessages = 2000;

    public RangedObservableCollection<string> Messages { get; } = new();
    public RangedObservableCollection<ConnectedClientViewModel> ConnectedClients { get; } = new();
    public ObservableCollection<string> AvailableGroups { get; } = new() { "All" };

    [ObservableProperty]
    private string _messageFilter = string.Empty;

    [ObservableProperty]
    private string _selectedGroup = "All";

    [ObservableProperty]
    private bool _showPayload;

    public MessagingViewModel(ClientIpcServer ipcServer, IDispatcherService dispatcherService)
        : base("Messaging")
    {
        _ipcServer = ipcServer;
        _dispatcher = dispatcherService;

        // Load any already-connected clients
        foreach (var info in _ipcServer.GetConnectedClients())
        {
            ConnectedClients.Add(new ConnectedClientViewModel(info));
            AddGroupIfNew(info.Group);
        }

        // Subscribe to events
        _ipcServer.ClientConnected += OnClientConnected;
        _ipcServer.ClientDisconnected += OnClientDisconnected;
        _ipcServer.ClientUpdated += OnClientUpdated;
        _ipcServer.MessageRouted += OnMessageRouted;
    }

    private void OnClientConnected(ConnectedClientInfo info)
    {
        _dispatcher.Invoke(() =>
        {
            ConnectedClients.Add(new ConnectedClientViewModel(info));
            AddGroupIfNew(info.Group);
        });
    }

    private void OnClientDisconnected(int clientId)
    {
        _dispatcher.Invoke(() =>
        {
            var client = ConnectedClients.FirstOrDefault(c => c.ClientId == clientId);
            if (client != null)
                ConnectedClients.Remove(client);
            RebuildGroupList();
        });
    }

    private void OnClientUpdated(ConnectedClientInfo info)
    {
        _dispatcher.Invoke(() =>
        {
            var client = ConnectedClients.FirstOrDefault(c => c.ClientId == info.ClientId);
            if (client != null)
                client.Update(info);
            else
                ConnectedClients.Add(new ConnectedClientViewModel(info));
            RebuildGroupList();
        });
    }

    private void OnMessageRouted(RoutedMessageInfo info)
    {
        string line = $"[{info.Timestamp:HH:mm:ss}] {info.SenderUsername} → {info.TargetDescription}: {info.MessageName}";
        if (ShowPayload && !string.IsNullOrEmpty(info.PayloadJson))
            line += $"  |  {info.PayloadJson}";

        // Apply filters
        if (SelectedGroup != "All")
        {
            // Only show messages from clients in the selected group
            var senderClient = ConnectedClients.FirstOrDefault(c => c.ClientId == info.SenderId);
            if (senderClient != null && !senderClient.Group.Equals(SelectedGroup, StringComparison.OrdinalIgnoreCase))
                return;
        }

        if (!string.IsNullOrEmpty(MessageFilter))
        {
            if (!line.Contains(MessageFilter, StringComparison.OrdinalIgnoreCase))
                return;
        }

        _dispatcher.Invoke(() =>
        {
            Messages.Add(line);
            while (Messages.Count > MaxMessages)
                Messages.RemoveAt(0);
        });
    }

    private void AddGroupIfNew(string group)
    {
        if (!AvailableGroups.Contains(group, StringComparer.OrdinalIgnoreCase))
            AvailableGroups.Add(group);
    }

    private void RebuildGroupList()
    {
        var currentGroups = ConnectedClients.Select(c => c.Group).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        // Remove groups no longer present (keep "All")
        for (int i = AvailableGroups.Count - 1; i >= 1; i--)
        {
            if (!currentGroups.Contains(AvailableGroups[i], StringComparer.OrdinalIgnoreCase))
                AvailableGroups.RemoveAt(i);
        }
        // Add new groups
        foreach (string group in currentGroups)
            AddGroupIfNew(group);
    }

    [RelayCommand]
    private void ClearMessages()
    {
        Messages.Clear();
    }

    [RelayCommand]
    private void CopyMessages()
    {
        string text = string.Join(Environment.NewLine, Messages);
        Ioc.Default.GetRequiredService<IClipboardService>().SetText(text);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _ipcServer.ClientConnected -= OnClientConnected;
                _ipcServer.ClientDisconnected -= OnClientDisconnected;
                _ipcServer.ClientUpdated -= OnClientUpdated;
                _ipcServer.MessageRouted -= OnMessageRouted;
            }
            _disposed = true;
        }
    }
}
