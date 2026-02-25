using Newtonsoft.Json;

namespace Skua.Core.Messaging;

public sealed record ClientIdentityMessage(int ClientId, string Username, string Group, bool IsLeader);

/// <summary>
/// Reserved system message names for internal IPC operations.
/// </summary>
public static class SystemMessages
{
    /// <summary>Client requests to change group. Payload: { NewGroup, RequestLeader }</summary>
    public const string UpdateGroup = "__system:update-group";

    /// <summary>Server confirms group change. Payload: { Group, IsLeader }</summary>
    public const string GroupUpdated = "__system:group-updated";
}

public sealed record ClientMessage(
    int SenderId,
    string MessageName,
    string PayloadJson,
    MessageTarget Target,
    int? TargetClientId = null,
    string? TargetGroup = null,
    bool? TargetLeader = null
);

public sealed record MessageTarget
{
    public static readonly MessageTarget Broadcast = new() { Type = TargetType.Broadcast };
    public static readonly MessageTarget Leader = new() { Type = TargetType.Leader };

    public static MessageTarget ToClient(int clientId) => new() { Type = TargetType.SpecificClient, ClientId = clientId };

    public static MessageTarget ToGroup(string groupName) => new() { Type = TargetType.Group, GroupName = groupName };

    public static MessageTarget FromClient(int clientId) => new() { Type = TargetType.FromClient, ClientId = clientId };

    public static MessageTarget FromGroup(string groupName) => new() { Type = TargetType.FromGroup, GroupName = groupName };

    public TargetType Type { get; init; }
    public int? ClientId { get; init; }
    public string? GroupName { get; init; }
}

public enum TargetType
{
    Broadcast,
    Leader,
    SpecificClient,
    Group,
    FromClient,
    FromGroup
}
