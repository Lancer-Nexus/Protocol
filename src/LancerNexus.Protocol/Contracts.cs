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
