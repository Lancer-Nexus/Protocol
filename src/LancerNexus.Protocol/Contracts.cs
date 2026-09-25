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
