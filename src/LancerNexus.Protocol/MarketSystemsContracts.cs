using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>Reads a bounded page from the catalog-pinned cluster system directory.</summary>
[MessagePackObject]
public sealed record MarketSystemsReadRequestV1
{
    public const int MaximumPageSize = 500;

    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public string? AfterSystemId { get; init; }
    [Key(3)] public string? ExpectedCatalogRevision { get; init; }
    [Key(4)] public int PageSize { get; init; } = 100;

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        (AfterSystemId is null || MarketContractValidation.ValidId(AfterSystemId)) &&
        (ExpectedCatalogRevision is null || MarketCatalogReadRequestV1.IsRevision(ExpectedCatalogRevision)) &&
        (AfterSystemId is null || ExpectedCatalogRevision is not null) &&
        PageSize is >= 1 and <= MaximumPageSize;
}

[MessagePackObject]
public sealed record MarketSystemSummaryV1
{
    [Key(0)] public string SystemId { get; init; } = "";
    [Key(1)] public long MarketCount { get; init; }
    [Key(2)] public long OfferingMarketCount { get; init; }
    [Key(3)] public long StaticOfferCount { get; init; }

    public bool IsValid() => MarketContractValidation.ValidId(SystemId) && MarketCount >= 0 &&
        OfferingMarketCount >= 0 && OfferingMarketCount <= MarketCount && StaticOfferCount >= OfferingMarketCount;
}

/// <summary>A page pinned to the immutable market seed and including systems with no offers.</summary>
[MessagePackObject]
public sealed record MarketSystemsReadResponseV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public MarketCatalogReadStatusV1 Status { get; init; }
    [Key(3)] public string? CatalogRevision { get; init; }
    [Key(4)] public MarketSystemSummaryV1[] Systems { get; init; } = [];
    [Key(5)] public string? NextSystemId { get; init; }

    public bool IsValidFor(MarketSystemsReadRequestV1 request)
    {
        if (request is null || !request.IsValid() || SchemaVersion != 1 ||
            CorrelationId != request.CorrelationId || !Enum.IsDefined(Status) || Systems is null ||
            Systems.Length > request.PageSize ||
            (CatalogRevision is not null && !MarketCatalogReadRequestV1.IsRevision(CatalogRevision)) ||
            (NextSystemId is not null && !MarketContractValidation.ValidId(NextSystemId)))
            return false;
        if (Status != MarketCatalogReadStatusV1.Ok)
            return CatalogRevision is null && Systems.Length == 0 && NextSystemId is null;
        if (CatalogRevision is null || (request.ExpectedCatalogRevision is not null &&
            request.ExpectedCatalogRevision != CatalogRevision))
            return false;
        string? previous = request.AfterSystemId;
        foreach (var system in Systems)
        {
            if (system is null || !system.IsValid() ||
                (previous is not null && StringComparer.Ordinal.Compare(system.SystemId, previous) <= 0))
                return false;
            previous = system.SystemId;
        }
        return NextSystemId is null || (Systems.Length > 0 && NextSystemId == previous);
    }
}
