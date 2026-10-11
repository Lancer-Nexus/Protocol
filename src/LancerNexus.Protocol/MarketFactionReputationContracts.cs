using MessagePack;

namespace LancerNexus.Protocol;

public enum MarketFactionReputationStatusV1 : byte
{
    Found = 1,
    RevisionMismatch = 2,
    NotFound = 3,
    Unavailable = 4
}

[MessagePackObject]
public sealed record MarketFactionReputationReadRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public long CharacterId { get; init; }
    [Key(3)] public long? ExpectedReputationRevision { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty && CharacterId > 0 &&
        (ExpectedReputationRevision is null or > 0);
}

[MessagePackObject]
public sealed record MarketFactionReputationV1
{
    [Key(0)] public string FactionId { get; init; } = "";
    [Key(1)] public float Value { get; init; }

    public bool IsValid() => MarketContractValidation.ValidId(FactionId) && FactionId.Length <= 64 &&
        float.IsFinite(Value) && Value is >= -1f and <= 1f;
}

[MessagePackObject]
public sealed record MarketFactionReputationReadResponseV1
{
    public const int MaximumFactionCount = 256;

    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public long CharacterId { get; init; }
    [Key(3)] public MarketFactionReputationStatusV1 Status { get; init; }
    [Key(4)] public long ReputationRevision { get; init; }
    [Key(5)] public MarketFactionReputationV1[] Factions { get; init; } = [];

    public bool IsValidFor(MarketFactionReputationReadRequestV1 request)
    {
        if (request is null || !request.IsValid() || SchemaVersion != 1 ||
            CorrelationId != request.CorrelationId || CharacterId != request.CharacterId ||
            !Enum.IsDefined(Status) || ReputationRevision < 0 || Factions is null ||
            Factions.Length > MaximumFactionCount)
            return false;

        var validFactions = Factions.All(faction => faction is not null && faction.IsValid()) &&
                            Factions.Select(faction => faction.FactionId).Distinct(StringComparer.Ordinal).Count() == Factions.Length;
        return Status switch
        {
            MarketFactionReputationStatusV1.Found => ReputationRevision > 0 && validFactions &&
                (request.ExpectedReputationRevision is null ||
                 request.ExpectedReputationRevision == ReputationRevision),
            MarketFactionReputationStatusV1.RevisionMismatch => ReputationRevision > 0 && Factions.Length == 0 &&
                request.ExpectedReputationRevision is not null &&
                request.ExpectedReputationRevision != ReputationRevision,
            MarketFactionReputationStatusV1.NotFound or MarketFactionReputationStatusV1.Unavailable =>
                ReputationRevision == 0 && Factions.Length == 0,
            _ => false
        };
    }
}
