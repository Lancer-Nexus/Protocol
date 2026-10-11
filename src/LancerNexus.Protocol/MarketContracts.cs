using MessagePack;

namespace LancerNexus.Protocol;

public static class MarketContractValidation
{
    public static bool ValidId(string? value) => value is { Length: > 0 } &&
        AdminQuery.ValidIdentifier(value) &&
        string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal);
}

public enum MarketSnapshotReadStatusV1 : byte
{
    Ok = 1,
    RevisionExpired = 2,
    Unavailable = 3
}

public enum MarketTradeSideV1 : byte
{
    Buy = 1,
    Sell = 2
}

public enum MarketTradeIngestStatusV1 : byte
{
    Applied = 1,
    Duplicate = 2,
    Conflict = 3,
    Rejected = 4,
    Unavailable = 5
}

public enum MarketTradeResolutionV1 : byte
{
    Commit = 1,
    Abort = 2
}

public enum MarketTradeReservationStatusV1 : byte
{
    Reserved = 1,
    Committed = 2,
    Aborted = 3,
    Conflict = 4,
    Rejected = 5,
    Unavailable = 6
}

/// <summary>Idempotently reserves market availability for one Gateway-owned trade ID.</summary>
[MessagePackObject]
public sealed record MarketTradeReservationRequestV1
{
    public const long MaximumQuantity = MarketTradeEventV1.MaximumQuantity;

    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public Guid TradeId { get; init; }
    [Key(3)] public string MarketId { get; init; } = "";
    [Key(4)] public string SystemId { get; init; } = "";
    [Key(5)] public string GoodId { get; init; } = "";
    [Key(6)] public MarketTradeSideV1 Side { get; init; }
    [Key(7)] public long Quantity { get; init; }
    [Key(8)] public long ExpectedMarketRevision { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty && TradeId != Guid.Empty &&
        MarketContractValidation.ValidId(MarketId) && MarketContractValidation.ValidId(SystemId) &&
        MarketContractValidation.ValidId(GoodId) && Enum.IsDefined(Side) &&
        Quantity is > 0 and <= MaximumQuantity && ExpectedMarketRevision > 0;
}

/// <summary>Commits or aborts one previously reserved Gateway trade.</summary>
[MessagePackObject]
public sealed record MarketTradeResolutionRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public Guid TradeId { get; init; }
    [Key(3)] public MarketTradeResolutionV1 Resolution { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty && TradeId != Guid.Empty &&
        Enum.IsDefined(Resolution);
}

/// <summary>Stable result of reserving or resolving one trade ID.</summary>
[MessagePackObject]
public sealed record MarketTradeReservationResponseV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public Guid TradeId { get; init; }
    [Key(3)] public MarketTradeReservationStatusV1 Status { get; init; }
    [Key(4)] public long QuoteUnitPrice { get; init; }
    [Key(5)] public long TotalPrice { get; init; }
    [Key(6)] public long MarketRevision { get; init; }
    [Key(7)] public long SystemRevision { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty && TradeId != Guid.Empty &&
        Enum.IsDefined(Status) && QuoteUnitPrice >= 0 && TotalPrice >= 0 && MarketRevision >= 0 &&
        SystemRevision >= 0 &&
        (Status is MarketTradeReservationStatusV1.Reserved or MarketTradeReservationStatusV1.Committed or
            MarketTradeReservationStatusV1.Aborted
            ? MarketRevision > 0 && SystemRevision > 0
            : MarketRevision == 0 && SystemRevision == 0 && QuoteUnitPrice == 0 && TotalPrice == 0);

    public bool Matches(MarketTradeReservationRequestV1 request) => request is not null && request.IsValid() &&
        IsValid() && TradeId == request.TradeId && CorrelationId == request.CorrelationId;
    public bool Matches(MarketTradeResolutionRequestV1 request) => request is not null && request.IsValid() &&
        IsValid() && TradeId == request.TradeId && CorrelationId == request.CorrelationId;
}

/// <summary>Reads one bounded page from the shared market projection for a system.</summary>
[MessagePackObject]
public sealed record MarketSnapshotReadRequestV1
{
    public const int MaximumPageSize = 500;

    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public string SystemId { get; init; } = "";
    [Key(3)] public string? AfterMarketId { get; init; }
    [Key(4)] public string? AfterGoodId { get; init; }
    [Key(5)] public long? ExpectedSystemRevision { get; init; }
    [Key(6)] public int PageSize { get; init; } = 100;

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        MarketContractValidation.ValidId(SystemId) &&
        ((AfterMarketId is null && AfterGoodId is null) ||
         (MarketContractValidation.ValidId(AfterMarketId) && MarketContractValidation.ValidId(AfterGoodId))) &&
        (ExpectedSystemRevision is null or > 0) &&
        (AfterMarketId is null || ExpectedSystemRevision is not null) &&
        PageSize is >= 1 and <= MaximumPageSize;
}

/// <summary>One location-specific, reputation-neutral market row.</summary>
[MessagePackObject]
public sealed record MarketGoodSnapshotV1
{
    [Key(0)] public string MarketId { get; init; } = "";
    [Key(1)] public string SystemId { get; init; } = "";
    [Key(2)] public string GoodId { get; init; } = "";
    [Key(3)] public long BaseUnitPrice { get; init; }
    [Key(4)] public long CurrentUnitPrice { get; init; }
    [Key(5)] public long AvailableStock { get; init; }
    [Key(6)] public long TargetStock { get; init; }
    [Key(7)] public bool BuyEnabled { get; init; }
    [Key(8)] public bool SellEnabled { get; init; }
    [Key(9)] public long MarketRevision { get; init; }

    public bool IsValid() => MarketContractValidation.ValidId(MarketId) &&
        MarketContractValidation.ValidId(SystemId) && MarketContractValidation.ValidId(GoodId) &&
        BaseUnitPrice >= 0 && CurrentUnitPrice >= 0 && AvailableStock >= 0 && TargetStock >= 0 &&
        MarketRevision > 0;
}

/// <summary>
/// A page is pinned to one system revision. Continue with ExpectedSystemRevision to avoid
/// combining pages from different market states; the service returns RevisionExpired if it
/// can no longer serve that revision consistently.
/// </summary>
[MessagePackObject]
public sealed record MarketSnapshotReadResponseV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public string SystemId { get; init; } = "";
    [Key(3)] public MarketSnapshotReadStatusV1 Status { get; init; }
    [Key(4)] public long SystemRevision { get; init; }
    [Key(5)] public MarketGoodSnapshotV1[] Goods { get; init; } = [];
    [Key(6)] public string? NextMarketId { get; init; }
    [Key(7)] public string? NextGoodId { get; init; }

    public bool IsValidFor(MarketSnapshotReadRequestV1 request)
    {
        if (request is null || !request.IsValid() || SchemaVersion != 1 ||
            CorrelationId != request.CorrelationId || SystemId != request.SystemId ||
            !Enum.IsDefined(Status) || Goods is null || Goods.Length > request.PageSize ||
            SystemRevision < 0 ||
            ((NextMarketId is null) != (NextGoodId is null)) ||
            (NextMarketId is not null &&
             (!MarketContractValidation.ValidId(NextMarketId) || !MarketContractValidation.ValidId(NextGoodId))))
            return false;

        if (Status != MarketSnapshotReadStatusV1.Ok)
            return SystemRevision == 0 && Goods.Length == 0 && NextMarketId is null;

        if (SystemRevision <= 0 ||
            (request.ExpectedSystemRevision is not null &&
             request.ExpectedSystemRevision != SystemRevision))
            return false;

        string? previousMarket = request.AfterMarketId;
        string? previousGood = request.AfterGoodId;
        foreach (var good in Goods)
        {
            if (good is null || !good.IsValid() || good.SystemId != SystemId ||
                (previousMarket is not null && CompareKey(good.MarketId, good.GoodId,
                    previousMarket, previousGood!) <= 0))
                return false;
            previousMarket = good.MarketId;
            previousGood = good.GoodId;
        }

        if (NextMarketId is not null && Goods.Length == 0)
            return false;
        return NextMarketId is null ||
            (NextMarketId == previousMarket && NextGoodId == previousGood);
    }

    private static int CompareKey(string marketId, string goodId, string otherMarketId, string otherGoodId)
    {
        var marketComparison = StringComparer.Ordinal.Compare(marketId, otherMarketId);
        return marketComparison != 0 ? marketComparison : StringComparer.Ordinal.Compare(goodId, otherGoodId);
    }
}

/// <summary>
/// A confirmed trade's market effect. It reports quantity only; the Coordinator computes
/// market prices and never accepts a client-supplied price or reputation value.
/// </summary>
[MessagePackObject]
public sealed record MarketTradeEventV1
{
    public const long MaximumQuantity = 1_000_000;

    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid EventId { get; init; }
    [Key(2)] public string InstanceId { get; init; } = "";
    [Key(3)] public Guid ProducerEpoch { get; init; }
    [Key(4)] public long Sequence { get; init; }
    [Key(5)] public DateTimeOffset OccurredUtc { get; init; }
    [Key(6)] public string MarketId { get; init; } = "";
    [Key(7)] public string SystemId { get; init; } = "";
    [Key(8)] public string GoodId { get; init; } = "";
    [Key(9)] public MarketTradeSideV1 Side { get; init; }
    [Key(10)] public long Quantity { get; init; }

    public bool IsValid() => SchemaVersion == 1 && EventId != Guid.Empty &&
        MarketContractValidation.ValidId(InstanceId) && ProducerEpoch != Guid.Empty && Sequence > 0 &&
        OccurredUtc.Offset == TimeSpan.Zero && MarketContractValidation.ValidId(MarketId) &&
        MarketContractValidation.ValidId(SystemId) && MarketContractValidation.ValidId(GoodId) &&
        Enum.IsDefined(Side) && Quantity is > 0 and <= MaximumQuantity;
}

/// <summary>Outcome of durable, idempotent market-event ingestion.</summary>
[MessagePackObject]
public sealed record MarketTradeEventAcknowledgementV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid EventId { get; init; }
    [Key(2)] public string InstanceId { get; init; } = "";
    [Key(3)] public Guid ProducerEpoch { get; init; }
    [Key(4)] public long Sequence { get; init; }
    [Key(5)] public MarketTradeIngestStatusV1 Status { get; init; }
    [Key(6)] public long MarketRevision { get; init; }

    [IgnoreMember]
    public bool IsDurablyAccepted => Status is MarketTradeIngestStatusV1.Applied or
        MarketTradeIngestStatusV1.Duplicate;

    [IgnoreMember]
    public bool IsRetryable => Status == MarketTradeIngestStatusV1.Unavailable;

    public bool IsValid() => SchemaVersion == 1 && EventId != Guid.Empty &&
        MarketContractValidation.ValidId(InstanceId) && ProducerEpoch != Guid.Empty && Sequence > 0 &&
        Enum.IsDefined(Status) && MarketRevision >= 0 &&
        (Status is not (MarketTradeIngestStatusV1.Applied or MarketTradeIngestStatusV1.Duplicate) ||
         MarketRevision > 0);

    public bool Matches(MarketTradeEventV1 trade) => trade is not null && trade.IsValid() && IsValid() &&
        EventId == trade.EventId && InstanceId == trade.InstanceId &&
        ProducerEpoch == trade.ProducerEpoch && Sequence == trade.Sequence;
}
