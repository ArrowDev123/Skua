using System.Collections.Concurrent;
using System.IO.Pipes;
using Newtonsoft.Json;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;

namespace Skua.Core.Services;

public record ConnectedClientInfo(int ClientId, string Username, string Group, bool IsLeader);
public record RoutedMessageInfo(int SenderId, string SenderUsername, string MessageName, string PayloadJson, string TargetDescription, DateTime Timestamp);

public class ClientIpcServer : IDisposable
{
    private readonly ILogService _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<int, ClientConnection> _clients = new();
    private int _nextClientId = 1;
    private NamedPipeServerStream? _serverStream;
    private bool _disposed;

    public event Action<ConnectedClientInfo>? ClientConnected;
    public event Action<int>? ClientDisconnected;
    public event Action<ConnectedClientInfo>? ClientUpdated;
    public event Action<RoutedMessageInfo>? MessageRouted;

    public ClientIpcServer(ILogService logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<ConnectedClientInfo> GetConnectedClients()
    {
        return _clients.Values
            .Select(c => new ConnectedClientInfo(c.ClientId, c.Username, c.Group, c.IsLeader))
            .ToList();
    }

    public async Task StartAsync()
    {
        _logger.DebugLog("Starting Client IPC Server on pipe 'SkuaManagerIPC'...");
        _ = Task.Run(AcceptClientsAsync, _cts.Token);
    }

    private async Task AcceptClientsAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _logger.DebugLog("Creating new named pipe server instance...");
                var serverStream = new NamedPipeServerStream(
                    "SkuaManagerIPC",
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous
                );

                _logger.DebugLog("Waiting for client connection...");
                await serverStream.WaitForConnectionAsync(_cts.Token);
                _logger.DebugLog("Client connected, starting handler...");
                _ = Task.Run(() => HandleClientAsync(serverStream));
            }
            catch (OperationCanceledException)
            {
                _logger.DebugLog("IPC Server cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.DebugLog($"IPC Server error: {ex.Message}");
                await Task.Delay(1000);
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream stream)
    {
        int clientId = Interlocked.Increment(ref _nextClientId);
        ClientConnection? connection = null;
        StreamReader? reader = null;
        StreamWriter? writer = null;

        try
        {
            reader = new StreamReader(stream);
            writer = new StreamWriter(stream) { AutoFlush = true };

            var identity = await ReadMessageAsync<ClientIdentityMessage>(reader);
            if (identity == null)
            {
                _logger.DebugLog($"Client {clientId} failed to send identity");
                return;
            }

            // Auto-elect leader: first client in a group becomes leader
            bool isLeader = identity.IsLeader;
            if (!isLeader)
            {
                bool groupHasLeader = _clients.Values
                    .Any(c => c.Group.Equals(identity.Group, StringComparison.OrdinalIgnoreCase) && c.IsLeader);
                if (!groupHasLeader)
                    isLeader = true;
            }

            connection = new ClientConnection
            {
                ClientId = clientId,
                Username = identity.Username,
                Group = identity.Group,
                IsLeader = isLeader,
                Stream = stream,
                Reader = reader,
                Writer = writer
            };

            _clients[clientId] = connection;
            _logger.DebugLog($"Client connected: {identity.Username} (ID: {clientId}, Group: {identity.Group}, Leader: {isLeader})");
            ClientConnected?.Invoke(new ConnectedClientInfo(clientId, identity.Username, identity.Group, isLeader));

            var identityResponse = new ClientIdentityMessage(clientId, identity.Username, identity.Group, isLeader);
            await SendMessageAsync(writer, identityResponse);

            while (!_cts.IsCancellationRequested && stream.IsConnected)
            {
                var message = await ReadMessageAsync<ClientMessage>(reader);
                if (message == null)
                    break;

                await RouteMessageAsync(message);
            }
        }
        catch (Exception ex)
        {
            _logger.DebugLog($"Client {clientId} error: {ex.Message}");
        }
        finally
        {
            if (connection != null)
                _clients.TryRemove(clientId, out _);
            _logger.DebugLog($"Client {clientId} disconnected");
            ClientDisconnected?.Invoke(clientId);
            writer?.Dispose();
            reader?.Dispose();
            await stream.DisposeAsync();
        }
    }

    private async Task HandleGroupUpdateAsync(ClientMessage message)
    {
        if (!_clients.TryGetValue(message.SenderId, out var connection))
            return;

        var payload = JsonConvert.DeserializeObject<Dictionary<string, object>>(message.PayloadJson);
        string newGroup = payload?["NewGroup"]?.ToString() ?? "default";
        bool requestLeader = payload?.ContainsKey("RequestLeader") == true
            && bool.TryParse(payload["RequestLeader"]?.ToString(), out var rl) && rl;

        string oldGroup = connection.Group;
        connection.Group = newGroup;

        // Re-evaluate leader status for the new group
        bool isLeader = requestLeader;
        if (!isLeader)
        {
            bool groupHasLeader = _clients.Values
                .Any(c => c.ClientId != message.SenderId
                    && c.Group.Equals(newGroup, StringComparison.OrdinalIgnoreCase)
                    && c.IsLeader);
            if (!groupHasLeader)
                isLeader = true;
        }
        connection.IsLeader = isLeader;

        _logger.DebugLog($"Client {message.SenderId} moved from group '{oldGroup}' to '{newGroup}' (Leader: {isLeader})");
        ClientUpdated?.Invoke(new ConnectedClientInfo(connection.ClientId, connection.Username, newGroup, isLeader));

        // Send confirmation back to the requesting client
        var response = new ClientMessage(
            0, // senderId 0 = server
            SystemMessages.GroupUpdated,
            JsonConvert.SerializeObject(new { Group = newGroup, IsLeader = isLeader }),
            MessageTarget.ToClient(message.SenderId),
            message.SenderId,
            null,
            null
        );

        try
        {
            await SendMessageAsync(connection.Writer, response);
        }
        catch (Exception ex)
        {
            _logger.DebugLog($"Failed to send group update response to client {message.SenderId}: {ex.Message}");
        }
    }

    private string ResolveClientName(int? clientId)
    {
        if (clientId.HasValue && _clients.TryGetValue(clientId.Value, out var conn))
            return conn.Username;
        return clientId.HasValue ? $"Client #{clientId}" : "Unknown";
    }

    private string DescribeTarget(MessageTarget target)
    {
        return target.Type switch
        {
            TargetType.Broadcast => "Broadcast",
            TargetType.Leader => "Leader",
            TargetType.SpecificClient => ResolveClientName(target.ClientId),
            TargetType.Group => $"Group '{target.GroupName}'",
            TargetType.FromClient => ResolveClientName(target.ClientId),
            TargetType.FromGroup => $"Group '{target.GroupName}'",
            _ => target.Type.ToString()
        };
    }

    private async Task RouteMessageAsync(ClientMessage message)
    {
        // Handle system messages (not routed to other clients)
        if (message.MessageName == SystemMessages.UpdateGroup)
        {
            await HandleGroupUpdateAsync(message);
            return;
        }

        // Raise event for UI monitoring
        string senderUsername = _clients.TryGetValue(message.SenderId, out var sender) ? sender.Username : $"Client-{message.SenderId}";
        MessageRouted?.Invoke(new RoutedMessageInfo(
            message.SenderId,
            senderUsername,
            message.MessageName,
            message.PayloadJson,
            DescribeTarget(message.Target),
            DateTime.Now));

        // Resolve the sender's group for group-scoped targeting
        string? senderGroup = null;
        if (_clients.TryGetValue(message.SenderId, out var senderConn))
            senderGroup = senderConn.Group;

        List<int> targetClientIds = new();

        switch (message.Target.Type)
        {
            case TargetType.Broadcast:
                targetClientIds = _clients.Values
                    .Where(c => c.ClientId != message.SenderId
                        && (senderGroup == null || c.Group.Equals(senderGroup, StringComparison.OrdinalIgnoreCase)))
                    .Select(c => c.ClientId)
                    .ToList();
                break;

            case TargetType.Leader:
                targetClientIds = _clients.Values
                    .Where(c => c.IsLeader && c.ClientId != message.SenderId
                        && (senderGroup == null || c.Group.Equals(senderGroup, StringComparison.OrdinalIgnoreCase)))
                    .Select(c => c.ClientId)
                    .ToList();
                break;

            case TargetType.SpecificClient:
                if (message.Target.ClientId.HasValue && _clients.ContainsKey(message.Target.ClientId.Value))
                    targetClientIds.Add(message.Target.ClientId.Value);
                break;

            case TargetType.Group:
                if (!string.IsNullOrEmpty(message.Target.GroupName))
                {
                    targetClientIds = _clients.Values
                        .Where(c => c.Group.Equals(message.Target.GroupName, StringComparison.OrdinalIgnoreCase) && c.ClientId != message.SenderId)
                        .Select(c => c.ClientId)
                        .ToList();
                }
                break;

            case TargetType.FromClient:
                if (message.Target.ClientId.HasValue && _clients.ContainsKey(message.Target.ClientId.Value))
                    targetClientIds.Add(message.Target.ClientId.Value);
                break;

            case TargetType.FromGroup:
                if (!string.IsNullOrEmpty(message.Target.GroupName))
                {
                    targetClientIds = _clients.Values
                        .Where(c => c.Group.Equals(message.Target.GroupName, StringComparison.OrdinalIgnoreCase) && c.ClientId != message.SenderId)
                        .Select(c => c.ClientId)
                        .ToList();
                }
                break;
        }

        foreach (int targetId in targetClientIds)
        {
            if (_clients.TryGetValue(targetId, out var targetClient))
            {
                try
                {
                    await SendMessageAsync(targetClient.Writer, message);
                }
                catch (Exception ex)
                {
                    _logger.DebugLog($"Failed to send message to client {targetId}: {ex.Message}");
                }
            }
        }
    }

    private async Task SendMessageAsync<T>(StreamWriter writer, T message)
    {
        string json = JsonConvert.SerializeObject(message);
        await writer.WriteAsync(json.Length.ToString().PadLeft(10, '0'));
        await writer.WriteAsync(json);
        await writer.FlushAsync();
    }

    private async Task<T?> ReadMessageAsync<T>(StreamReader reader)
    {
        try
        {
            char[] lengthBuffer = new char[10];
            int totalRead = 0;
            while (totalRead < 10)
            {
                int read = await reader.ReadAsync(lengthBuffer, totalRead, 10 - totalRead);
                if (read == 0)
                    return default;
                totalRead += read;
            }

            int length = int.Parse(new string(lengthBuffer));
            char[] messageBuffer = new char[length];
            totalRead = 0;
            while (totalRead < length)
            {
                int read = await reader.ReadAsync(messageBuffer, totalRead, length - totalRead);
                if (read == 0)
                    return default;
                totalRead += read;
            }

            string json = new string(messageBuffer);
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch
        {
            return default;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cts.Cancel();
        _serverStream?.Dispose();

        foreach (var client in _clients.Values)
        {
            client.Stream?.Dispose();
        }
        _clients.Clear();

        _cts.Dispose();
        _disposed = true;
    }

    private class ClientConnection
    {
        public int ClientId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public bool IsLeader { get; set; }
        public NamedPipeServerStream? Stream { get; set; }
        public StreamReader? Reader { get; set; }
        public StreamWriter? Writer { get; set; }
    }
}
