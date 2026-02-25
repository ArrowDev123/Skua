using System;

namespace Skua.Core.Interfaces;

public interface IClientMessagingService
{
    Identity GetIdentity();

    /// <summary>
    /// Changes this client's group at runtime. The server re-evaluates leader election for the new group.
    /// If <paramref name="requestLeader"/> is true, this client requests leadership of the new group.
    /// Otherwise, the server auto-elects the first client in the group as leader.
    /// Blocks until the server confirms the update.
    /// </summary>
    void UpdateGroup(string newGroup, bool requestLeader = false);

    void Broadcast(string messageName, object payload);
    void SendToGroup(string groupName, string messageName, object payload);
    void SendToClient(int clientId, string messageName, object payload);
    void SendToLeader(string messageName, object payload);

    void OnMessage(string messageName, Action<int, object> callback);
    void OnMessageFrom(int clientId, string messageName, Action<int, object> callback);
    void OnGroupMessage(string groupName, string messageName, Action<int, object> callback);
}

public sealed record Identity(int ClientId, string Group, bool IsLeader);
