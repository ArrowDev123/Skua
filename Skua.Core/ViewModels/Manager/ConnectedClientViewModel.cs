using CommunityToolkit.Mvvm.ComponentModel;
using Skua.Core.Services;

namespace Skua.Core.ViewModels.Manager;

public partial class ConnectedClientViewModel : ObservableObject
{
    public int ClientId { get; }

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _group = string.Empty;

    [ObservableProperty]
    private bool _isLeader;

    public ConnectedClientViewModel(ConnectedClientInfo info)
    {
        ClientId = info.ClientId;
        Username = info.Username;
        Group = info.Group;
        IsLeader = info.IsLeader;
    }

    public void Update(ConnectedClientInfo info)
    {
        Username = info.Username;
        Group = info.Group;
        IsLeader = info.IsLeader;
    }
}
