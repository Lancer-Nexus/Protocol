using MessagePack;

namespace LancerNexus.Protocol;

public enum MarketGoodReadStatusV1 : byte
{
    Found = 1,
    NotFound = 2,
    RevisionMismatch = 3,
    Unavailable = 4
}

/// <summary>Reads one market-good row from the shared cluster market projection.</summary>
[MessagePackObject]
public sealed record MarketGoodReadRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public string SystemId { get; init; } = "";
    [Key(3)] public string MarketId { get; init; } = "";
    [Key(4)] public string GoodId { get; init; } = "";
    [Key(5)] public long? ExpectedSystemRevision { get; init; }
    [Key(6)] public long? ExpectedMarketRevision { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        MarketContractValidation.ValidId(SystemId) && MarketContractValidation.ValidId(MarketId) &&
        MarketContractValidation.ValidId(GoodId) &&
        (ExpectedSystemRevision is null or > 0) && (ExpectedMarketRevision is null or > 0);
}

/// <summary>One exact market row and the revisions used to read it.</summary>
[MessagePackObject]
public sealed record MarketGoodReadResponseV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public string SystemId { get; init; } = "";
    [Key(3)] public string MarketId { get; init; } = "";
    [Key(4)] public string GoodId { get; init; } = "";
    [Key(5)] public MarketGoodReadStatusV1 Status { get; init; }
    [Key(6)] public long SystemRevision { get; init; }
    [Key(7)] public long MarketRevision { get; init; }
    [Key(8)] public MarketGoodSnapshotV1? Good { get; init; }

    public bool IsValidFor(MarketGoodReadRequestV1 request)
    {
        if (request is null || !request.IsValid() || SchemaVersion != 1 ||
            CorrelationId != request.CorrelationId || SystemId != request.SystemId ||
            MarketId != request.MarketId || GoodId != request.GoodId || !Enum.IsDefined(Status) ||
            SystemRevision < 0 || MarketRevision < 0)
            return false;

        return Status switch
        {
            MarketGoodReadStatusV1.Found => SystemRevision > 0 && MarketRevision > 0 &&
                Good is { } good && good.IsValid() && good.SystemId == SystemId &&
                good.MarketId == MarketId && good.GoodId == GoodId &&
                good.MarketRevision == MarketRevision &&
                (request.ExpectedSystemRevision is null || request.ExpectedSystemRevision == SystemRevision) &&
                (request.ExpectedMarketRevision is null || request.ExpectedMarketRevision == MarketRevision),
            MarketGoodReadStatusV1.NotFound => SystemRevision > 0 && MarketRevision == 0 && Good is null &&
                (request.ExpectedSystemRevision is null || request.ExpectedSystemRevision == SystemRevision),
            MarketGoodReadStatusV1.RevisionMismatch => SystemRevision > 0 && Good is null &&
                ((request.ExpectedSystemRevision is long expectedSystem && SystemRevision > 0 &&
                  expectedSystem != SystemRevision) ||
                 (request.ExpectedMarketRevision is long expectedMarket && MarketRevision > 0 &&
                  expectedMarket != MarketRevision)),
            MarketGoodReadStatusV1.Unavailable => SystemRevision == 0 && MarketRevision == 0 && Good is null,
            _ => false
        };
    }
}
