using MessagePack;

namespace LancerNexus.Protocol;

public enum NpcRetirementReasonV1 : byte
{
    Destroyed = 1,
    Docked = 2,
    Despawned = 3
}

[MessagePackObject]
public sealed record NpcRetirementEntryV1
{
    [Key(0)] public Guid NpcId { get; init; }
    [Key(1)] public long OwnershipVersion { get; init; }
    [Key(2)] public NpcRetirementReasonV1 Reason { get; init; }
}

/// <summary>
/// Retires up to 256 NPCs without reusing their identities. Retry with the same
/// RequestId and exact payload after an unknown outcome. Entries are fenced independently;
/// a rejected entry does not prevent other entries from being retired atomically.
/// </summary>
[MessagePackObject]
public sealed record NpcRetirementRequestV1
{
    [Key(0)] public Guid RequestId { get; init; }
    [Key(1)] public string InstanceId { get; init; } = "";
    [Key(2)] public NpcRetirementEntryV1[] Npcs { get; init; } = [];

    public bool IsValid() => RequestId != Guid.Empty && !string.IsNullOrWhiteSpace(InstanceId) &&
        InstanceId.Length <= 96 && Npcs is { Length: > 0 and <= 256 } &&
        Npcs.All(npc => npc is not null && npc.NpcId != Guid.Empty &&
            npc.OwnershipVersion is > 0 and < long.MaxValue && Enum.IsDefined(npc.Reason)) &&
        Npcs.Select(npc => npc.NpcId).Distinct().Count() == Npcs.Length;
}

[MessagePackObject]
public sealed record NpcRetirementResultV1
{
    [Key(0)] public Guid NpcId { get; init; }
    [Key(1)] public bool Accepted { get; init; }
    [Key(2)] public string ReasonCode { get; init; } = "";
    [Key(3)] public long OwnershipVersion { get; init; }
}

[MessagePackObject]
public sealed record NpcRetirementResponseV1
{
    [Key(0)] public Guid RequestId { get; init; }
    [Key(1)] public NpcRetirementResultV1[] Npcs { get; init; } = [];
    [Key(2)] public string ReasonCode { get; init; } = "";
}
