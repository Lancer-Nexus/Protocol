using MessagePack;

namespace LancerNexus.Protocol;

public enum MarketQuoteStatusV1 : byte
{
    Quoted = 1,
    MarketRevisionMismatch = 2,
    ReputationRevisionMismatch = 3,
    NotFound = 4,
    Rejected = 5,
    Unavailable = 6
}

/// <summary>Requests a market quote from an authenticated game instance and binds it to a live character lease.</summary>
[MessagePackObject]
public sealed record MarketTradeQuoteReadRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public long CharacterId { get; init; }
    [Key(3)] public string LeaseToken { get; init; } = "";
    [Key(4)] public long LeaseVersion { get; init; }
    [Key(5)] public string SystemId { get; init; } = "";
    [Key(6)] public string MarketId { get; init; } = "";
    [Key(7)] public string GoodId { get; init; } = "";
    [Key(8)] public MarketTradeSideV1 Side { get; init; }
    [Key(9)] public long Quantity { get; init; }
    [Key(10)] public long? ExpectedMarketRevision { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty && CharacterId > 0 &&
        CharacterSaveSnapshotWriteRequestV1.IsLeaseToken(LeaseToken) && LeaseVersion > 0 &&
        MarketContractValidation.ValidId(SystemId) && MarketContractValidation.ValidId(MarketId) &&
        MarketContractValidation.ValidId(GoodId) && Enum.IsDefined(Side) &&
        Quantity is > 0 and <= MarketTradeEventV1.MaximumQuantity &&
        (ExpectedMarketRevision is null or > 0);

    public MarketQuoteRequestV1 ToMarketQuoteRequest(long expectedReputationRevision) => new()
    {
        CorrelationId = CorrelationId,
        CharacterId = CharacterId,
        SystemId = SystemId,
        MarketId = MarketId,
        GoodId = GoodId,
        Side = Side,
        Quantity = Quantity,
        ExpectedMarketRevision = ExpectedMarketRevision,
        ExpectedReputationRevision = expectedReputationRevision
    };
}

/// <summary>
/// Requests a character-bound market price preview. The Coordinator obtains reputation
/// from Gateway using the expected snapshot revision; callers never submit reputation values.
/// </summary>
[MessagePackObject]
public sealed record MarketQuoteRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public long CharacterId { get; init; }
    [Key(3)] public string SystemId { get; init; } = "";
    [Key(4)] public string MarketId { get; init; } = "";
    [Key(5)] public string GoodId { get; init; } = "";
    [Key(6)] public MarketTradeSideV1 Side { get; init; }
    [Key(7)] public long Quantity { get; init; }
    [Key(8)] public long? ExpectedMarketRevision { get; init; }
    [Key(9)] public long ExpectedReputationRevision { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty && CharacterId > 0 &&
        MarketContractValidation.ValidId(SystemId) && MarketContractValidation.ValidId(MarketId) &&
        MarketContractValidation.ValidId(GoodId) && Enum.IsDefined(Side) &&
        Quantity is > 0 and <= MarketTradeEventV1.MaximumQuantity &&
        (ExpectedMarketRevision is null or > 0) && ExpectedReputationRevision > 0;
}

/// <summary>Reputation-aware preview; it reserves neither market stock nor player credits.</summary>
[MessagePackObject]
public sealed record MarketQuoteResponseV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public long CharacterId { get; init; }
    [Key(3)] public string SystemId { get; init; } = "";
    [Key(4)] public string MarketId { get; init; } = "";
    [Key(5)] public string GoodId { get; init; } = "";
    [Key(6)] public MarketTradeSideV1 Side { get; init; }
    [Key(7)] public long Quantity { get; init; }
    [Key(8)] public MarketQuoteStatusV1 Status { get; init; }
    [Key(9)] public long MarketRevision { get; init; }
    [Key(10)] public long ReputationRevision { get; init; }
    [Key(11)] public string? MarketFactionId { get; init; }
    [Key(12)] public float AppliedReputation { get; init; }
    [Key(13)] public int ReputationFactorPartsPerMillion { get; init; }
    [Key(14)] public long QuoteUnitPrice { get; init; }
    [Key(15)] public long TotalPrice { get; init; }
    [Key(16)] public DateTimeOffset ExpiresUtc { get; init; }
    [Key(17)] public int PricingPolicyVersion { get; init; }

    public bool IsValidFor(MarketQuoteRequestV1 request)
    {
        if (request is null || !request.IsValid() || SchemaVersion != 1 ||
            CorrelationId != request.CorrelationId || CharacterId != request.CharacterId ||
            SystemId != request.SystemId || MarketId != request.MarketId || GoodId != request.GoodId ||
            Side != request.Side || Quantity != request.Quantity || !Enum.IsDefined(Status) ||
            MarketRevision < 0 || ReputationRevision < 0 || QuoteUnitPrice < 0 || TotalPrice < 0)
            return false;

        if (Status == MarketQuoteStatusV1.Quoted)
            return MarketRevision > 0 &&
                (request.ExpectedMarketRevision is null || request.ExpectedMarketRevision == MarketRevision) &&
                ReputationRevision == request.ExpectedReputationRevision &&
                MarketContractValidation.ValidId(MarketFactionId) && float.IsFinite(AppliedReputation) &&
                AppliedReputation is >= -1f and <= 1f &&
                ReputationFactorPartsPerMillion is >= 750_000 and <= 1_250_000 &&
                ExpiresUtc.Offset == TimeSpan.Zero && ExpiresUtc > DateTimeOffset.MinValue &&
                PricingPolicyVersion == 1;
        if (Status == MarketQuoteStatusV1.MarketRevisionMismatch)
            return request.ExpectedMarketRevision is not null && MarketRevision > 0 &&
                MarketRevision != request.ExpectedMarketRevision && ReputationRevision == 0 &&
                MarketFactionId is null && AppliedReputation == 0 && ReputationFactorPartsPerMillion == 0 &&
                QuoteUnitPrice == 0 && TotalPrice == 0 && ExpiresUtc == default && PricingPolicyVersion == 0;
        if (Status == MarketQuoteStatusV1.ReputationRevisionMismatch)
            return ReputationRevision > 0 && ReputationRevision != request.ExpectedReputationRevision &&
                MarketRevision == 0 && MarketFactionId is null && AppliedReputation == 0 &&
                ReputationFactorPartsPerMillion == 0 && QuoteUnitPrice == 0 && TotalPrice == 0 &&
                ExpiresUtc == default && PricingPolicyVersion == 0;
        return MarketRevision == 0 && ReputationRevision == 0 && MarketFactionId is null &&
            AppliedReputation == 0 && ReputationFactorPartsPerMillion == 0 && QuoteUnitPrice == 0 &&
            TotalPrice == 0 && ExpiresUtc == default && PricingPolicyVersion == 0;
    }
}
