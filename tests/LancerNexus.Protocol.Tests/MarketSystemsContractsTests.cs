using LancerNexus.Protocol;
using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class MarketSystemsContractsTests
{
    [Fact]
    public void PinnedSystemDirectorySupportsKeysetPagingAndZeroOfferWorlds()
    {
        var revision = new string('a', 64);
        var firstRequest = new MarketSystemsReadRequestV1 { CorrelationId = Guid.NewGuid(), PageSize = 1 };
        var firstResponse = new MarketSystemsReadResponseV1
        {
            CorrelationId = firstRequest.CorrelationId,
            Status = MarketCatalogReadStatusV1.Ok,
            CatalogRevision = revision,
            Systems = [new MarketSystemSummaryV1 { SystemId = "br-01", MarketCount = 2,
                OfferingMarketCount = 1, StaticOfferCount = 12 }],
            NextSystemId = "br-01"
        };

        Assert.True(firstResponse.IsValidFor(firstRequest));
        Assert.Equal(firstRequest, MessagePackSerializer.Deserialize<MarketSystemsReadRequestV1>(
            MessagePackSerializer.Serialize(firstRequest)));

        var nextRequest = firstRequest with
        {
            CorrelationId = Guid.NewGuid(),
            AfterSystemId = "br-01",
            ExpectedCatalogRevision = revision
        };
        var zeroOfferPage = new MarketSystemsReadResponseV1
        {
            CorrelationId = nextRequest.CorrelationId,
            Status = MarketCatalogReadStatusV1.Ok,
            CatalogRevision = revision,
            Systems = [new MarketSystemSummaryV1 { SystemId = "bw-01" }]
        };
        Assert.True(zeroOfferPage.IsValidFor(nextRequest));
        Assert.False((firstRequest with { AfterSystemId = "br-01" }).IsValid());
        Assert.False((zeroOfferPage with { CatalogRevision = new string('b', 64) }).IsValidFor(nextRequest));
        Assert.False((zeroOfferPage with
        {
            Systems = [new MarketSystemSummaryV1
            {
                SystemId = "bw-01", OfferingMarketCount = 2, MarketCount = 1
            }]
        }).IsValidFor(nextRequest));
    }
}
