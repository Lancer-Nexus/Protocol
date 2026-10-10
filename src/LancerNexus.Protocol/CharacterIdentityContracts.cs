using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>Resolves a locally selected character to its Gateway-owned stable identity.</summary>
[MessagePackObject]
public sealed record CharacterIdentityResolveRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public Guid AccountId { get; init; }
    [Key(3)] public Guid SessionId { get; init; }
    [Key(4)] public string DisplayName { get; init; } = "";

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        AccountId != Guid.Empty && SessionId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(DisplayName) && DisplayName.Length <= 96 &&
        DisplayName == DisplayName.Trim() && !DisplayName.Any(char.IsControl);
}

[MessagePackObject]
public sealed record CharacterIdentityResolveResultV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public bool Accepted { get; init; }
    [Key(3)] public string ReasonCode { get; init; } = "";
    [Key(4)] public long? CharacterId { get; init; }
    [Key(5)] public string? LeaseTicket { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        AdminQuery.ValidIdentifier(ReasonCode) &&
        (Accepted ? ReasonCode == "resolved" && CharacterId is > 0 && !string.IsNullOrWhiteSpace(LeaseTicket)
            : CharacterId is null && LeaseTicket is null);
}
