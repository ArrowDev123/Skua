namespace Skua.Core.Messaging;

/// <summary>
/// Defines the IPC message protocol for army coordination.
/// All message names use the "army:" prefix for namespacing.
/// </summary>
public static class ArmyProtocol
{
    // === Follower -> Leader messages ===

    /// <summary>Follower announces presence to leader.</summary>
    public const string Announce = "army:announce";

    /// <summary>Follower sends its list of available classes.</summary>
    public const string ClassList = "army:class-list";

    /// <summary>Follower confirms it has equipped the requested class.</summary>
    public const string ClassEquipped = "army:class-equipped";

    /// <summary>Follower confirms it has joined the map.</summary>
    public const string MapJoined = "army:map-joined";

    /// <summary>Follower reports its current state (heartbeat).</summary>
    public const string StatusReport = "army:status";

    // === Leader -> Follower messages ===

    /// <summary>Leader acknowledges follower and requests class list.</summary>
    public const string RequestClasses = "army:request-classes";

    /// <summary>Leader tells follower which class to equip.</summary>
    public const string EquipClass = "army:equip-class";

    /// <summary>Leader tells follower to join a specific map/cell/pad.</summary>
    public const string JoinMap = "army:join-map";

    /// <summary>Leader tells follower to start combat.</summary>
    public const string StartCombat = "army:start-combat";

    /// <summary>Leader tells follower to pause combat but stay in map.</summary>
    public const string PauseCombat = "army:pause-combat";

    /// <summary>Leader tells follower to move to a different cell in the current map.</summary>
    public const string MoveCell = "army:move-cell";

    /// <summary>Leader tells all followers to stop and exit.</summary>
    public const string Stop = "army:stop";

    // === Consensus / Barrier Sync messages ===

    /// <summary>Any client reports it has completed a named stage.</summary>
    public const string StageComplete = "army:stage-complete";

    /// <summary>Leader tells all clients that everyone has completed the stage — proceed.</summary>
    public const string StageAllReady = "army:stage-all-ready";

    /// <summary>Any client registers itself as a participant in consensus barriers.</summary>
    public const string ConsensusJoin = "army:consensus-join";

    /// <summary>A client is leaving the consensus group (disconnect/stop).</summary>
    public const string ConsensusLeave = "army:consensus-leave";
}

// --- Payload records ---

/// <summary>Sent by follower with army:announce</summary>
public sealed record ArmyAnnouncePayload(int ClientId, string Username);

/// <summary>Sent by follower with army:class-list</summary>
public sealed record ArmyClassListPayload(int ClientId, string[] ClassNames);

/// <summary>Sent by leader with army:request-classes</summary>
public sealed record ArmyRequestClassesPayload(int LeaderClientId);

/// <summary>Sent by leader with army:equip-class</summary>
public sealed record ArmyEquipClassPayload(string ClassName);

/// <summary>Sent by leader with army:join-map</summary>
public sealed record ArmyJoinMapPayload(string Map, string Cell, string Pad, int RoomNumber);

/// <summary>Sent by leader with army:start-combat</summary>
public sealed record ArmyStartCombatPayload(string Target, bool AggroMonsters);

/// <summary>Sent by leader with army:move-cell</summary>
public sealed record ArmyMoveCellPayload(string Cell, string Pad);

/// <summary>Sent by follower with army:class-equipped</summary>
public sealed record ArmyClassEquippedPayload(int ClientId, string ClassName);

/// <summary>Sent by follower with army:map-joined</summary>
public sealed record ArmyMapJoinedPayload(int ClientId, string Map);

/// <summary>Sent by any client with army:stage-complete</summary>
public sealed record ArmyStageCompletePayload(int ClientId, string StageName);

/// <summary>Sent by leader with army:stage-all-ready</summary>
public sealed record ArmyStageAllReadyPayload(string StageName);

/// <summary>Sent by any client with army:consensus-join</summary>
public sealed record ArmyConsensusJoinPayload(int ClientId, string Username);

/// <summary>Sent by any client with army:consensus-leave</summary>
public sealed record ArmyConsensusLeavePayload(int ClientId);
