using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>
/// One market effect within an atomic ship/package trade. Lines are sorted by
/// GoodId and then Side so every participant hashes and locks them identically.
/// </summary>
[MessagePackObject]
public sealed record MarketTradeBundleLineV1
{
    [Key(0)] public string GoodId { get; init; } = "";
    [Key(1)] public MarketTradeSideV1 Side { get; init; }
    [Key(2)] public long Quantity { get; init; }
    [Key(3)] public long ExpectedMarketRevision { get; init; }
    [Key(4)] public long ExpectedQuoteUnitPrice { get; init; }
    [Key(5)] public long ExpectedTotalPrice { get; init; }
    [Key(6)] public long ReputationPriceFactorPartsPerMillion { get; init; }

    public bool IsValid() => MarketContractValidation.ValidId(GoodId) && Enum.IsDefined(Side) &&
        Quantity is > 0 and <= MarketTradeEventV1.MaximumQuantity && ExpectedMarketRevision > 0 &&
        ExpectedQuoteUnitPrice >= 0 && ExpectedTotalPrice >= 0 &&
        ReputationPriceFactorPartsPerMillion is
            >= MarketTradeQuoteReservationRequestV1.MinimumPriceFactorPartsPerMillion and <=
            MarketTradeQuoteReservationRequestV1.MaximumPriceFactorPartsPerMillion;
}

/// <summary>
/// One idempotent, reputation-bound reservation for all market effects of a
/// ship/package exchange at one base. A failure rejects every line.
/// </summary>
[MessagePackObject]
public sealed record MarketTradeBundleReservationRequestV1
{
    public const int MaximumLines = 64;

    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public Guid TradeId { get; init; }
    [Key(3)] public string MarketId { get; init; } = "";
    [Key(4)] public string SystemId { get; init; } = "";
    [Key(5)] public long CharacterId { get; init; }
    [Key(6)] public long ExpectedReputationRevision { get; init; }
    [Key(7)] public string MarketFactionId { get; init; } = "";
    [Key(8)] public MarketTradeBundleLineV1[] Lines { get; init; } = [];

    public bool IsValid()
    {
        if (SchemaVersion != 1 || CorrelationId == Guid.Empty || TradeId == Guid.Empty ||
            !MarketContractValidation.ValidId(MarketId) || !MarketContractValidation.ValidId(SystemId) ||
            CharacterId <= 0 || ExpectedReputationRevision <= 0 ||
            !MarketContractValidation.ValidId(MarketFactionId) || MarketFactionId.Length > 64 ||
            Lines is null || Lines.Length is < 1 or > MaximumLines)
            return false;

        MarketTradeBundleLineV1? previous = null;
        foreach (var line in Lines)
        {
            if (line is null || !line.IsValid() ||
                previous is not null && Compare(previous, line) >= 0)
                return false;
            previous = line;
        }
        return true;
    }

    internal static int Compare(MarketTradeBundleLineV1 left, MarketTradeBundleLineV1 right)
    {
        var good = StringComparer.Ordinal.Compare(left.GoodId, right.GoodId);
        return good != 0 ? good : left.Side.CompareTo(right.Side);
    }
}

/// <summary>The reserved quote result for one bundle line.</summary>
[MessagePackObject]
public sealed record MarketTradeBundleLineResultV1
{
    [Key(0)] public string GoodId { get; init; } = "";
    [Key(1)] public MarketTradeSideV1 Side { get; init; }
    [Key(2)] public long Quantity { get; init; }
    [Key(3)] public long QuoteUnitPrice { get; init; }
    [Key(4)] public long TotalPrice { get; init; }
    [Key(5)] public long MarketRevision { get; init; }

    public bool IsValid() => MarketContractValidation.ValidId(GoodId) && Enum.IsDefined(Side) &&
        Quantity is > 0 and <= MarketTradeEventV1.MaximumQuantity && QuoteUnitPrice >= 0 &&
        TotalPrice >= 0 && MarketRevision > 0;
}

/// <summary>Stable all-or-nothing result for a bundle reservation.</summary>
[MessagePackObject]
public sealed record MarketTradeBundleReservationResponseV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public Guid TradeId { get; init; }
    [Key(3)] public MarketTradeReservationStatusV1 Status { get; init; }
    [Key(4)] public long SystemRevision { get; init; }
    [Key(5)] public long ReputationRevision { get; init; }
    [Key(6)] public string? MarketFactionId { get; init; }
    [Key(7)] public long CurrentMarketRevision { get; init; }
    [Key(8)] public MarketTradeBundleLineResultV1[] Lines { get; init; } = [];
    [Key(9)] public string? CurrentMarketGoodId { get; init; }
    [Key(10)] public MarketTradeSideV1? CurrentMarketSide { get; init; }

    public bool IsValidFor(MarketTradeBundleReservationRequestV1 request)
    {
        if (request is null || !request.IsValid() || SchemaVersion != 1 ||
            CorrelationId != request.CorrelationId || TradeId != request.TradeId ||
            !Enum.IsDefined(Status) || SystemRevision < 0 || ReputationRevision < 0 ||
            CurrentMarketRevision < 0 || Lines is null || Lines.Length > request.Lines.Length)
            return false;

        if (Status is MarketTradeReservationStatusV1.Reserved or MarketTradeReservationStatusV1.Committed or
            MarketTradeReservationStatusV1.Aborted)
        {
            if (SystemRevision <= 0 || ReputationRevision != request.ExpectedReputationRevision ||
                MarketFactionId != request.MarketFactionId || CurrentMarketRevision != 0 ||
                CurrentMarketGoodId is not null || CurrentMarketSide is not null ||
                Lines.Length != request.Lines.Length)
                return false;
            for (var i = 0; i < Lines.Length; i++)
            {
                var actual = Lines[i];
                var expected = request.Lines[i];
                if (actual is null || !actual.IsValid() || actual.GoodId != expected.GoodId ||
                    actual.Side != expected.Side || actual.Quantity != expected.Quantity ||
                    actual.QuoteUnitPrice != expected.ExpectedQuoteUnitPrice ||
                    actual.TotalPrice != expected.ExpectedTotalPrice ||
                    actual.MarketRevision != expected.ExpectedMarketRevision)
                    return false;
            }
            return true;
        }

        if (Lines.Length != 0 || SystemRevision != 0 || MarketFactionId is not null)
            return false;
        return Status switch
        {
            MarketTradeReservationStatusV1.MarketRevisionMismatch =>
                CurrentMarketRevision > 0 && ReputationRevision == 0 &&
                CurrentMarketGoodId is not null &&
                CurrentMarketSide is MarketTradeSideV1 marketSide && Enum.IsDefined(marketSide) &&
                request.Lines.Any(line => line.GoodId == CurrentMarketGoodId && line.Side == marketSide),
            MarketTradeReservationStatusV1.ReputationRevisionMismatch =>
                CurrentMarketRevision == 0 && ReputationRevision > 0 &&
                ReputationRevision != request.ExpectedReputationRevision && CurrentMarketGoodId is null &&
                CurrentMarketSide is null,
            _ => CurrentMarketRevision == 0 && ReputationRevision == 0 && CurrentMarketGoodId is null &&
                 CurrentMarketSide is null
        };
    }
}

[MessagePackObject]
public sealed record MarketTradeBundleEventLineV1
{
    [Key(0)] public string GoodId { get; init; } = "";
    [Key(1)] public MarketTradeSideV1 Side { get; init; }
    [Key(2)] public long Quantity { get; init; }

    public bool IsValid() => MarketContractValidation.ValidId(GoodId) && Enum.IsDefined(Side) &&
        Quantity is > 0 and <= MarketTradeEventV1.MaximumQuantity;
}

/// <summary>One atomic market effect emitted after the Gateway commits a bundle.</summary>
[MessagePackObject]
public sealed record MarketTradeBundleEventV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid EventId { get; init; }
    [Key(2)] public Guid TradeId { get; init; }
    [Key(3)] public string InstanceId { get; init; } = "";
    [Key(4)] public Guid ProducerEpoch { get; init; }
    [Key(5)] public long Sequence { get; init; }
    [Key(6)] public DateTimeOffset OccurredUtc { get; init; }
    [Key(7)] public string MarketId { get; init; } = "";
    [Key(8)] public string SystemId { get; init; } = "";
    [Key(9)] public MarketTradeBundleEventLineV1[] Lines { get; init; } = [];

    public bool IsValid() => SchemaVersion == 1 && EventId != Guid.Empty && TradeId != Guid.Empty &&
        MarketContractValidation.ValidId(InstanceId) && ProducerEpoch != Guid.Empty && Sequence > 0 &&
        OccurredUtc.Offset == TimeSpan.Zero && MarketContractValidation.ValidId(MarketId) &&
        MarketContractValidation.ValidId(SystemId) && Lines is { Length: > 0 and <= MarketTradeBundleReservationRequestV1.MaximumLines } &&
        Lines.All(line => line is not null && line.IsValid()) &&
        Lines.Zip(Lines.Skip(1), (left, right) => Compare(left, right) < 0).All(x => x);

    private static int Compare(MarketTradeBundleEventLineV1 left, MarketTradeBundleEventLineV1 right)
    {
        var good = StringComparer.Ordinal.Compare(left.GoodId, right.GoodId);
        return good != 0 ? good : left.Side.CompareTo(right.Side);
    }
}

[MessagePackObject]
public sealed record MarketTradeBundleEventAcknowledgementV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public Guid EventId { get; init; }
    [Key(3)] public Guid TradeId { get; init; }
    [Key(4)] public string InstanceId { get; init; } = "";
    [Key(5)] public Guid ProducerEpoch { get; init; }
    [Key(6)] public long Sequence { get; init; }
    [Key(7)] public MarketTradeIngestStatusV1 Status { get; init; }

    public bool IsValidFor(MarketTradeBundleEventV1 trade) => trade is not null && trade.IsValid() &&
        SchemaVersion == 1 && CorrelationId != Guid.Empty && EventId == trade.EventId &&
        TradeId == trade.TradeId && InstanceId == trade.InstanceId && ProducerEpoch == trade.ProducerEpoch &&
        Sequence == trade.Sequence && Enum.IsDefined(Status);
}
