using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>Registers a newly spawned NPC with the authoritative ownership registry.</summary>
[MessagePackObject]
public sealed class NpcOwnershipRegistrationRequest
{
    [Key(0)] public Guid NpcId { get; init; }
    [Key(1)] public string InstanceId { get; init; } = "";
    [Key(2)] public string SystemId { get; init; } = "";
    [Key(3)] public string IdempotencyKey { get; init; } = "";
}

[MessagePackObject]
public sealed class NpcOwnershipRegistrationResult
{
    [Key(0)] public bool Accepted { get; init; }
    [Key(1)] public string ReasonCode { get; init; } = "";
    [Key(2)] public bool Duplicate { get; init; }
    [Key(3)] public NpcOwnershipLease? Lease { get; init; }
}

/// <summary>Requests a bounded block of coordinator-issued NPC identities.</summary>
[MessagePackObject]
public sealed class NpcIdBatchAllocationRequest
{
    [Key(0)] public Guid RequestId { get; init; }
    [Key(1)] public string InstanceId { get; init; } = "";
    [Key(2)] public string SystemId { get; init; } = "";
    [Key(3)] public ushort Count { get; init; }
}

[MessagePackObject]
public sealed class NpcIdBatchAllocationResponse
{
    [Key(0)] public Guid RequestId { get; init; }
    [Key(1)] public bool Accepted { get; init; }
    [Key(2)] public string ReasonCode { get; init; } = "";
    [Key(3)] public NpcOwnershipLease[] Npcs { get; init; } = [];
    [Key(4)] public bool Duplicate { get; init; }
}
