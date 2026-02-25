using System.Collections.Concurrent;
using System.IO.Pipes;
using Newtonsoft.Json;
using Skua.Core.Interfaces;
using Skua.Core.Messaging;

namespace Skua.Core.Services;

public class ClientMessagingService : IClientMessagingService, IDisposable
{
    private readonly ILogService _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, List<Action<int, object>>> _messageCallbacks = new();
    private readonly ConcurrentDictionary<int, List<Action<int, object>>> _fromClientCallbacks = new();
    private readonly ConcurrentDictionary<string, List<Action<int, object>>> _groupCallbacks = new();
    private NamedPipeClientStream? _clientStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private bool _disposed;

    private readonly ManualResetEventSlim _groupUpdateEvent = new(false);

    public Identity? Identity { get; private set; }
    public bool IsConnected { get; private set; }

    public ClientMessagingService(ILogService logger)
    {
        _logger = logger;
    }

    public async Task ConnectAsync(string username, string group, bool isLeader)
    {
        try
        {
            _logger.ScriptLog($"[Messaging] Connecting to Manager pipe 'SkuaManagerIPC'...");

            _clientStream = new NamedPipeClientStream(
                ".",
                "SkuaManagerIPC",
                PipeDirection.InOut,
                PipeOptions.Asynchronous
            );

            _logger.ScriptLog($"[Messaging] Pipe created, waiting for connection (5 second timeout)...");
            var connectTask = _clientStream.ConnectAsync(_cts.Token);
            var timeoutTask = Task.Delay(5000);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.ScriptLog($"[Messaging] Connection timeout - Manager may not be running or IPC server not started");
                return;
            }

            await connectTask;
            _logger.ScriptLog($"[Messaging] Connected to pipe!");
            IsConnected = true;

            _reader = new StreamReader(_clientStream);
            _writer = new StreamWriter(_clientStream) { AutoFlush = true };

            var identityMessage = new ClientIdentityMessage(0, username, group, isLeader);
            _logger.ScriptLog($"[Messaging] Sending identity: {username}, {group}, {isLeader}");
            await SendMessageAsync(identityMessage);

            _logger.ScriptLog($"[Messaging] Waiting for server response...");
            var response = await ReadMessageAsync<ClientIdentityMessage>();
            if (response != null)
            {
                Identity = new Identity(response.ClientId, response.Group, response.IsLeader);
                _logger.ScriptLog($"Connected to Manager - Client ID: {Identity.ClientId}, Group: {Identity.Group}, IsLeader: {Identity.IsLeader}");
            }

            // Register system message handler for group updates (once)
            OnMessage(SystemMessages.GroupUpdated, (senderId, payload) =>
            {
                var data = payload as Newtonsoft.Json.Linq.JObject;
                if (data != null)
                {
                    string grp = data.Value<string>("Group") ?? Identity!.Group;
                    bool leader = data.Value<bool>("IsLeader");
                    Identity = new Identity(Identity!.ClientId, grp, leader);
                    _logger.ScriptLog($"[Messaging] Group updated: {grp}, IsLeader: {leader}");
                }
                _groupUpdateEvent.Set();
            });

            _ = Task.Run(ListenForMessagesAsync, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.ScriptLog($"[Messaging] Connection cancelled");
        }
        catch (Exception ex)
        {
            _logger.ScriptLog($"[Messaging] Failed to connect to Manager: {ex.GetType().Name} - {ex.Message}");
            _logger.ScriptLog($"[Messaging] Stack trace: {ex.StackTrace}");
        }
    }

    private async Task ListenForMessagesAsync()
    {
        while (!_cts.IsCancellationRequested && IsConnected && _clientStream?.IsConnected == true)
        {
            try
            {
                var message = await ReadMessageAsync<ClientMessage>();
                if (message == null)
                    break;

                await ProcessIncomingMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger.ScriptLog($"Error receiving message: {ex.Message}");
                break;
            }
        }

        IsConnected = false;
    }

    private async Task ProcessIncomingMessageAsync(ClientMessage message)
    {
        var payload = JsonConvert.DeserializeObject(message.PayloadJson);

        if (_messageCallbacks.TryGetValue(message.MessageName, out var callbacks))
        {
            foreach (var callback in callbacks)
            {
                try
                {
                    callback(message.SenderId, payload);
                }
                catch (Exception ex)
                {
                    _logger.ScriptLog($"Error in message callback for '{message.MessageName}': {ex.Message}");
                }
            }
        }

        if (_fromClientCallbacks.TryGetValue(message.SenderId, out var fromClientCallbacks))
        {
            foreach (var callback in fromClientCallbacks)
            {
                try
                {
                    callback(message.SenderId, payload);
                }
                catch (Exception ex)
                {
                    _logger.ScriptLog($"Error in from-client callback for client {message.SenderId}: {ex.Message}");
                }
            }
        }

        if (message.Target.Type == TargetType.Group && !string.IsNullOrEmpty(message.Target.GroupName))
        {
            if (_groupCallbacks.TryGetValue(message.Target.GroupName, out var groupCallbacks))
            {
                foreach (var callback in groupCallbacks)
                {
                    try
                    {
                        callback(message.SenderId, payload);
                    }
                    catch (Exception ex)
                    {
                        _logger.ScriptLog($"Error in group callback for '{message.Target.GroupName}': {ex.Message}");
                    }
                }
            }
        }
    }

    public Identity GetIdentity()
    {
        if (Identity == null)
            throw new InvalidOperationException("Not connected to Manager");
        return Identity;
    }

    public void UpdateGroup(string newGroup, bool requestLeader = false)
    {
        if (!IsConnected || Identity == null)
            throw new InvalidOperationException("Not connected to Manager");

        _groupUpdateEvent.Reset();

        // Send the update request through the normal message channel
        SendInternal(MessageTarget.Broadcast, SystemMessages.UpdateGroup,
            new { NewGroup = newGroup, RequestLeader = requestLeader });

        // Wait for server confirmation (10 second timeout)
        if (!_groupUpdateEvent.Wait(10000))
        {
            _logger.ScriptLog("[Messaging] WARNING: Group update timed out");
        }
    }

    public void Broadcast(string messageName, object payload)
    {
        SendInternal(MessageTarget.Broadcast, messageName, payload);
    }

    public void SendToGroup(string groupName, string messageName, object payload)
    {
        SendInternal(MessageTarget.ToGroup(groupName), messageName, payload);
    }

    public void SendToClient(int clientId, string messageName, object payload)
    {
        SendInternal(MessageTarget.ToClient(clientId), messageName, payload);
    }

    public void SendToLeader(string messageName, object payload)
    {
        SendInternal(MessageTarget.Leader, messageName, payload);
    }

    public void OnMessage(string messageName, Action<int, object> callback)
    {
        _messageCallbacks.AddOrUpdate(
            messageName,
            _ => new List<Action<int, object>> { callback },
            (_, list) =>
            {
                list.Add(callback);
                return list;
            });
    }

    public void OnMessageFrom(int clientId, string messageName, Action<int, object> callback)
    {
        _fromClientCallbacks.AddOrUpdate(
            clientId,
            _ => new List<Action<int, object>> { callback },
            (_, list) =>
            {
                list.Add(callback);
                return list;
            });
    }

    public void OnGroupMessage(string groupName, string messageName, Action<int, object> callback)
    {
        _groupCallbacks.AddOrUpdate(
            groupName,
            _ => new List<Action<int, object>> { callback },
            (_, list) =>
            {
                list.Add(callback);
                return list;
            });
    }

    private async Task SendInternal(MessageTarget target, string messageName, object payload)
    {
        if (!IsConnected || Identity == null || _writer == null)
            return;

        try
        {
            var message = new ClientMessage(
                Identity.ClientId,
                messageName,
                JsonConvert.SerializeObject(payload),
                target,
                target.ClientId,
                target.GroupName,
                target.Type == TargetType.Leader ? true : null
            );

            await SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.ScriptLog($"Failed to send message '{messageName}': {ex.Message}");
        }
    }

    private async Task SendMessageAsync<T>(T message)
    {
        if (_writer == null)
            return;

        string json = JsonConvert.SerializeObject(message);
        await _writer.WriteAsync(json.Length.ToString().PadLeft(10, '0'));
        await _writer.WriteAsync(json);
        await _writer.FlushAsync();
    }

    private async Task<T?> ReadMessageAsync<T>()
    {
        if (_reader == null)
            return default;

        try
        {
            char[] lengthBuffer = new char[10];
            int totalRead = 0;
            while (totalRead < 10)
            {
                int read = await _reader.ReadAsync(lengthBuffer, totalRead, 10 - totalRead);
                if (read == 0)
                    return default;
                totalRead += read;
            }

            int length = int.Parse(new string(lengthBuffer));
            char[] messageBuffer = new char[length];
            totalRead = 0;
            while (totalRead < length)
            {
                int read = await _reader.ReadAsync(messageBuffer, totalRead, length - totalRead);
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
        _writer?.Dispose();
        _reader?.Dispose();
        _clientStream?.Dispose();

        _messageCallbacks.Clear();
        _fromClientCallbacks.Clear();
        _groupCallbacks.Clear();

        _groupUpdateEvent.Dispose();
        _cts.Dispose();
        _disposed = true;
    }
}
