using MessagePack;

namespace LancerNexus.Protocol;

[MessagePackObject]
public sealed class ClusterHello
{
    [Key(0)] public string NodeId { get; init; } = "";
    [Key(1)] public string InstanceId { get; init; } = "";
    [Key(2)] public string BuildVersion { get; init; } = "";
    [Key(3)] public ushort ProtocolVersion { get; init; } = ProtocolConstants.ProtocolVersion;
    [Key(4)] public string[] Capabilities { get; init; } = [];
    [Key(5)] public string[] OwnedSystems { get; init; } = [];
}

[MessagePackObject]
public sealed class ClusterHandshakeResponse
{
    [Key(0)] public bool Accepted { get; init; }
    [Key(1)] public string ReasonCode { get; init; } = "";
    [Key(2)] public string[] NegotiatedCapabilities { get; init; } = [];
}

[MessagePackObject]
public sealed class AgentHeartbeat
{
    [Key(0)] public string AgentId { get; init; } = "";
    [Key(1)] public string NodeId { get; init; } = "";
    [Key(2)] public string BuildVersion { get; init; } = "";
    [Key(3)] public ushort ProtocolVersion { get; init; } = ProtocolConstants.ProtocolVersion;
    [Key(4)] public string[] Capabilities { get; init; } = [];
    [Key(5)] public ulong Sequence { get; init; }
}

[MessagePackObject]
public sealed class AgentHeartbeatResponse
{
    [Key(0)] public bool Accepted { get; init; }
    [Key(1)] public string ReasonCode { get; init; } = "";
    [Key(2)] public ulong Sequence { get; init; }
}

[MessagePackObject]
public sealed class InstanceHeartbeat
{
    [Key(0)] public string AgentId { get; init; } = "";
    [Key(1)] public string InstanceId { get; init; } = "";
    [Key(2)] public string SystemId { get; init; } = "";
    [Key(3)] public ulong Sequence { get; init; }
    [Key(4)] public bool IsReady { get; init; }
    [Key(5)] public bool IsDraining { get; init; }
    [Key(6)] public int CurrentPlayers { get; init; }
    [Key(7)] public int MaxPlayers { get; init; }
    [Key(8)] public string Endpoint { get; init; } = "";
    [Key(9)] public string[] Capabilities { get; init; } = [];
    // Empty retains the legacy single-SystemId behavior.
    private string[]? systemIds;
    [Key(10)] public string[] SystemIds { get => systemIds ?? []; init => systemIds = value; }
}

[MessagePackObject]
public sealed class InstanceHeartbeatResponse
{
    [Key(0)] public bool Accepted { get; init; }
    [Key(1)] public string ReasonCode { get; init; } = "";
    [Key(2)] public ulong Sequence { get; init; }
}

[MessagePackObject]
public sealed class PlacementRequest
{
    [Key(0)] public Guid RequestId { get; init; }
    [Key(1)] public Guid SessionId { get; init; }
    [Key(2)] public long? CharacterId { get; init; }
    [Key(3)] public string TargetSystem { get; init; } = "";
    [Key(4)] public string? GroupId { get; init; }
    [Key(5)] public string? EventId { get; init; }
    [Key(6)] public string ClientBuild { get; init; } = "";
    [Key(7)] public string Region { get; init; } = "";
    [Key(8)] public string IdempotencyKey { get; init; } = "";
}

[MessagePackObject]
public sealed class PlacementDecision
{
    [Key(0)] public Guid RequestId { get; init; }
    [Key(1)] public bool Accepted { get; init; }
    [Key(2)] public string? InstanceId { get; init; }
    [Key(3)] public string? SystemId { get; init; }
    [Key(4)] public string? Endpoint { get; init; }
    [Key(5)] public string ReasonCode { get; init; } = "";
    [Key(6)] public DateTime ExpiresUtc { get; init; }
    [Key(7)] public string? JoinTicket { get; init; }
}

[MessagePackObject]
public sealed class TransferPrepareRequest
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public Guid SessionId { get; init; }
    [Key(2)] public long CharacterId { get; init; }
    [Key(3)] public string SourceInstanceId { get; init; } = "";
    [Key(4)] public string TargetInstanceId { get; init; } = "";
    [Key(5)] public string TargetSystemId { get; init; } = "";
    [Key(6)] public string? GroupId { get; init; }
    [Key(7)] public DateTime ExpiresUtc { get; init; }
    [Key(8)] public string IdempotencyKey { get; init; } = "";
}

[MessagePackObject]
public sealed class TransferPrepared
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public bool Accepted { get; init; }
    [Key(2)] public string? TransferTicket { get; init; }
    [Key(3)] public DateTime ExpiresUtc { get; init; }
    [Key(4)] public string ReasonCode { get; init; } = "";
}

/// <summary>Short-lived bearer ticket claims bound to one source-to-target character transfer.</summary>
[MessagePackObject]
public sealed class TransferTicketClaims
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public Guid SessionId { get; init; }
    [Key(2)] public Guid AccountId { get; init; }
    [Key(3)] public long CharacterId { get; init; }
    [Key(4)] public string SourceInstanceId { get; init; } = "";
    [Key(5)] public string TargetInstanceId { get; init; } = "";
    [Key(6)] public string TargetSystemId { get; init; } = "";
    [Key(7)] public long LeaseVersion { get; init; }
    [Key(8)] public DateTime IssuedAtUtc { get; init; }
    [Key(9)] public DateTime ExpiresAtUtc { get; init; }
    [Key(10)] public string Nonce { get; init; } = "";
    [Key(11)] public string Audience { get; init; } = "";
    [Key(12)] public string KeyId { get; init; } = "";
}

[MessagePackObject]
public sealed class TransferTicketVerificationRequest
{
    [Key(0)] public string Ticket { get; init; } = "";
    [Key(1)] public string TargetInstanceId { get; init; } = "";
}

/// <summary>Authenticated target-instance confirmation with its proposed next lease credential.</summary>
[MessagePackObject]
public sealed class TransferTargetAcceptanceRequest
{
    [Key(0)] public string Ticket { get; init; } = "";
    [Key(1)] public string TargetLeaseToken { get; init; } = "";
}

/// <summary>Idempotent request from the authenticated source instance to release a committed transfer.</summary>
[MessagePackObject]
public sealed class TransferSourceReleaseRequest
{
    [Key(0)] public Guid TransferId { get; init; }
}

[MessagePackObject]
public sealed class TransferStatusResponse
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public string SourceInstanceId { get; init; } = "";
    [Key(2)] public string TargetInstanceId { get; init; } = "";
    [Key(3)] public string TargetSystemId { get; init; } = "";
    [Key(4)] public TransferState State { get; init; }
    [Key(5)] public DateTime ExpiresUtc { get; init; }
    [Key(6)] public long LeaseVersion { get; init; }
}

/// <summary>Client-authenticated request to begin a transfer; source ownership is resolved by Gateway.</summary>
[MessagePackObject]
public sealed class TransferStartRequest
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public Guid SessionId { get; init; }
    [Key(2)] public long CharacterId { get; init; }
    [Key(3)] public string TargetInstanceId { get; init; } = "";
    [Key(4)] public string TargetSystemId { get; init; } = "";
    [Key(5)] public string? GroupId { get; init; }
    [Key(6)] public DateTime ExpiresUtc { get; init; }
    [Key(7)] public string IdempotencyKey { get; init; } = "";
    [Key(8)] public Guid AccountId { get; init; }
}

[MessagePackObject]
public sealed class TransferStartResult
{
    [Key(0)] public TransferPrepared Prepared { get; init; } = new();
    [Key(1)] public string? SourceInstanceId { get; init; }
    [Key(2)] public string? TargetEndpoint { get; init; }
    [Key(3)] public string? TargetSystemId { get; init; }
    [Key(4)] public long LeaseVersion { get; init; }
    [Key(5)] public bool Duplicate { get; init; }
    [Key(6)] public string? TargetInstanceId { get; init; }
}

[MessagePackObject]
public sealed class TransferCommit
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public long CharacterId { get; init; }
    [Key(2)] public long LeaseVersion { get; init; }
    [Key(3)] public byte[] Snapshot { get; init; } = [];
}

[MessagePackObject]
public sealed class TransferAbort
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public string ReasonCode { get; init; } = "";
    [Key(2)] public bool Retryable { get; init; }
}

/// <summary>Requests an idempotent reservation for a moving NPC or an atomic NPC formation handoff.</summary>
[MessagePackObject]
public sealed class NpcTransferPrepareRequest
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public string SourceInstanceId { get; init; } = "";
    [Key(2)] public string TargetInstanceId { get; init; } = "";
    [Key(3)] public string TargetSystemId { get; init; } = "";
    [Key(4)] public Guid[] NpcIds { get; init; } = [];
    [Key(5)] public Guid? FormationId { get; init; }
    [Key(6)] public Guid? MissionRuntimeId { get; init; }
    [Key(7)] public DateTime ExpiresUtc { get; init; }
    [Key(8)] public string IdempotencyKey { get; init; } = "";
}

[MessagePackObject]
public sealed class NpcTransferPrepared
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public bool Accepted { get; init; }
    [Key(2)] public string? TargetEndpoint { get; init; }
    [Key(3)] public DateTime ExpiresUtc { get; init; }
    [Key(4)] public string ReasonCode { get; init; } = "";
}

/// <summary>Durable ownership fence for one moving NPC.</summary>
[MessagePackObject]
public sealed class NpcOwnershipLease
{
    [Key(0)] public Guid NpcId { get; init; }
    [Key(1)] public string InstanceId { get; init; } = "";
    [Key(2)] public long OwnershipVersion { get; init; }
    [Key(3)] public Guid? ActiveTransferId { get; init; }
}

/// <summary>
/// Versioned runtime payload. The payload bytes contain the serialized fields defined by
/// RuntimeSchemaVersion; receivers must reject unknown versions and never start simulation
/// until ownership commit has completed.
/// </summary>
[MessagePackObject]
public sealed class NpcRuntimeSnapshot
{
    [Key(0)] public Guid NpcId { get; init; }
    [Key(1)] public long OwnershipVersion { get; init; }
    [Key(2)] public string SystemId { get; init; } = "";
    [Key(3)] public ushort RuntimeSchemaVersion { get; init; } = 2;
    [Key(4)] public byte[] RuntimeState { get; init; } = [];
}

/// <summary>
/// MessagePack payload for RuntimeSchemaVersion 1. All references to other runtime objects
/// use stable NPC IDs; no GameObject or component references cross the transfer boundary.
/// </summary>
[MessagePackObject]
public sealed class NpcRuntimeStateV1
{
    [Key(0)] public NpcVector3 Position { get; init; } = new();
    [Key(1)] public NpcQuaternion Orientation { get; init; } = new();
    [Key(2)] public NpcVector3 LinearVelocity { get; init; } = new();
    [Key(3)] public NpcVector3 AngularVelocity { get; init; } = new();
    [Key(4)] public float Health { get; init; }
    [Key(5)] public string LoadoutArchetype { get; init; } = "";
    [Key(6)] public NpcEquipmentState[] Equipment { get; init; } = [];
    [Key(7)] public NpcCargoState[] Cargo { get; init; } = [];
    [Key(8)] public NpcAutopilotState Autopilot { get; init; } = new();
    [Key(9)] public NpcAiState Ai { get; init; } = new();
    [Key(10)] public Guid? CurrentTargetNpcId { get; init; }
    [Key(11)] public byte[] MissionState { get; init; } = [];
    [Key(12)] public string Nickname { get; init; } = "";
    [Key(13)] public string DisplayName { get; init; } = "";
    [Key(14)] public string FactionId { get; init; } = "";
    [Key(15)] public string PilotId { get; init; } = "";
    [Key(16)] public string StateGraphId { get; init; } = "";
    [Key(17)] public string CommHeadId { get; init; } = "";
    [Key(18)] public string CommBodyId { get; init; } = "";
    [Key(19)] public string CommAccessoryId { get; init; } = "";
}

[MessagePackObject]
public sealed class NpcVector3
{
    [Key(0)] public float X { get; init; }
    [Key(1)] public float Y { get; init; }
    [Key(2)] public float Z { get; init; }
}

[MessagePackObject]
public sealed class NpcQuaternion
{
    [Key(0)] public float X { get; init; }
    [Key(1)] public float Y { get; init; }
    [Key(2)] public float Z { get; init; }
    [Key(3)] public float W { get; init; } = 1;
}

[MessagePackObject]
public sealed class NpcEquipmentState
{
    [Key(0)] public string EquipmentId { get; init; } = "";
    [Key(1)] public string Hardpoint { get; init; } = "";
    [Key(2)] public float Health { get; init; }
    [Key(3)] public float Energy { get; init; }
    [Key(4)] public byte[] ExtensionData { get; init; } = [];
}

[MessagePackObject]
public sealed class NpcCargoState
{
    [Key(0)] public string ItemId { get; init; } = "";
    [Key(1)] public int Count { get; init; }
    [Key(2)] public string? Hardpoint { get; init; }
}

[MessagePackObject]
public sealed class NpcAutopilotState
{
    [Key(0)] public string Behavior { get; init; } = "none";
    [Key(1)] public Guid? TargetNpcId { get; init; }
    [Key(2)] public NpcVector3? TargetPosition { get; init; }
    [Key(3)] public float Throttle { get; init; }
    [Key(4)] public bool Cruise { get; init; }
    [Key(5)] public double BehaviorElapsedSeconds { get; init; }
    [Key(6)] public byte[] ExtensionData { get; init; } = [];
}

[MessagePackObject]
public sealed class NpcAiState
{
    [Key(0)] public string StateId { get; init; } = "";
    [Key(1)] public string PreviousStateId { get; init; } = "";
    [Key(2)] public double StateElapsedSeconds { get; init; }
    [Key(3)] public double[] Timers { get; init; } = [];
    [Key(4)] public ulong RandomState { get; init; }
    [Key(5)] public byte[] ExtensionData { get; init; } = [];
}

[MessagePackObject]
public sealed class NpcTransferSnapshot
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public Guid[] NpcIds { get; init; } = [];
    [Key(2)] public Guid? FormationId { get; init; }
    [Key(3)] public Guid? MissionRuntimeId { get; init; }
    [Key(4)] public ushort SnapshotSchemaVersion { get; init; } = 3;
    [Key(5)] public NpcRuntimeSnapshot[] Npcs { get; init; } = [];
    [Key(6)] public string TargetSystemId { get; init; } = "";
    [Key(7)] public byte[] MissionRuntimeState { get; init; } = [];
}

[MessagePackObject]
public sealed class NpcMissionRuntimeStateV1
{
    [Key(0)] public string MissionNickname { get; init; } = "";
    [Key(1)] public ulong RandomState { get; init; }
    [Key(2)] public string? ActiveObjective { get; init; }
    [Key(3)] public string LastSaveTrigger { get; init; } = "";
    [Key(4)] public string[] CompletedTriggers { get; init; } = [];
    [Key(5)] public NpcMissionActiveTriggerState[] ActiveTriggers { get; init; } = [];
    [Key(6)] public NpcMissionPendingLineState[] PendingLines { get; init; } = [];
    [Key(7)] public ushort SchemaVersion { get; init; } = 4;
    [Key(8)] public NpcMissionLabelState[] Labels { get; init; } = [];
    [Key(9)] public NpcGeneratedMissionState? GeneratedMission { get; init; }
}

[MessagePackObject]
public sealed class NpcGeneratedMissionState
{
    [Key(0)] public string OfferBaseNickname { get; init; } = "";
    [Key(1)] public string OfferFactionNickname { get; init; } = "";
    [Key(2)] public string HostileFactionNickname { get; init; } = "";
    [Key(3)] public string DestinationSystemNickname { get; init; } = "";
    [Key(4)] public string TargetZoneNickname { get; init; } = "";
    [Key(5)] public string TargetShipArchNickname { get; init; } = "";
    [Key(6)] public string MissionType { get; init; } = "";
    [Key(7)] public int TargetLocationIds { get; init; }
    [Key(8)] public int Reward { get; init; }
    [Key(9)] public float Difficulty { get; init; }
    [Key(10)] public int Seed { get; init; }
    [Key(11)] public NpcVector3 TargetPosition { get; init; } = new();
    [Key(12)] public int Id { get; init; }
    [Key(13)] public string OfferText { get; init; } = "";
    [Key(14)] public string TargetName { get; init; } = "";
}

[MessagePackObject]
public sealed class NpcMissionLabelState
{
    [Key(0)] public string Nickname { get; init; } = "";
    [Key(1)] public string[] Alive { get; init; } = [];
    [Key(2)] public string[] Destroyed { get; init; } = [];
}

[MessagePackObject]
public sealed class NpcMissionActiveTriggerState
{
    [Key(0)] public string Nickname { get; init; } = "";
    [Key(1)] public bool Deactivated { get; init; }
    [Key(2)] public double ActiveSeconds { get; init; }
    [Key(3)] public byte[] Satisfied { get; init; } = [];
    [Key(4)] public NpcMissionConditionState[] Conditions { get; init; } = [];
}

[MessagePackObject]
public sealed class NpcMissionConditionState
{
    [Key(0)] public string Kind { get; init; } = "none";
    [Key(1)] public bool BooleanValue { get; init; }
    [Key(2)] public double NumberValue { get; init; }
    [Key(3)] public string[] StringValues { get; init; } = [];
}

[MessagePackObject]
public sealed class NpcMissionPendingLineState
{
    [Key(0)] public uint Hash { get; init; }
    [Key(1)] public string Line { get; init; } = "";
}

[MessagePackObject]
public sealed class NpcTransferPhaseRequest
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public NpcTransferState State { get; init; }
    [Key(2)] public NpcTransferSnapshot? Snapshot { get; init; }
}

public enum NpcTransferState : byte
{
    Requested = 1,
    Reserved = 2,
    Prepared = 3,
    SourceFrozen = 4,
    TargetAccepted = 5,
    Committed = 6,
    SourceReleased = 7,
    Aborted = 22
}

public enum TransferState : byte
{
    Requested = 1,
    Reserved = 2,
    Prepared = 3,
    SourceFrozen = 4,
    TargetAccepted = 5,
    Committed = 6,
    SourceReleased = 7,
    Rejected = 20,
    Expired = 21,
    Aborted = 22,
    TimedOut = 23
}
