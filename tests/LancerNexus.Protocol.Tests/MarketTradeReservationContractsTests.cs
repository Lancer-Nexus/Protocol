using LancerNexus.Protocol;
using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class MarketTradeReservationContractsTests
{
    [Fact]
    public void ReservationAndResolutionContractsRoundTripAndBindTradeId()
    {
        Assert.Equal((ushort)314, (ushort)ClusterMessageType.MarketTradeReservationRequest);
        Assert.Equal((ushort)315, (ushort)ClusterMessageType.MarketTradeReservationResponse);
        Assert.Equal((ushort)316, (ushort)ClusterMessageType.MarketTradeResolutionRequest);
        Assert.Equal((ushort)317, (ushort)ClusterMessageType.MarketTradeResolutionResponse);

        var reserve = new MarketTradeReservationRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            TradeId = Guid.NewGuid(),
            MarketId = "li01_01_base",
            SystemId = "li01",
            GoodId = "commodity_food",
            Side = MarketTradeSideV1.Buy,
            Quantity = 10,
            ExpectedMarketRevision = 44
        };
        var response = new MarketTradeReservationResponseV1
        {
            CorrelationId = reserve.CorrelationId,
            TradeId = reserve.TradeId,
            Status = MarketTradeReservationStatusV1.Reserved,
            QuoteUnitPrice = 125,
            TotalPrice = 1250,
            MarketRevision = 45,
            SystemRevision = 81
        };
        var resolve = new MarketTradeResolutionRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            TradeId = reserve.TradeId,
            Resolution = MarketTradeResolutionV1.Commit
        };
        var final = response with
        {
            CorrelationId = resolve.CorrelationId,
            Status = MarketTradeReservationStatusV1.Committed,
            MarketRevision = 46,
            SystemRevision = 82
        };

        Assert.True(reserve.IsValid());
        Assert.True(response.IsValid());
        Assert.True(response.Matches(reserve));
        Assert.True(resolve.IsValid());
        Assert.True(final.IsValid());
        Assert.True(final.Matches(resolve));
        Assert.Equal(reserve, RoundTrip(reserve));
        Assert.Equal(resolve, RoundTrip(resolve));
        Assert.Equal(response, RoundTrip(response));
        Assert.Equal(9, ArrayLength(reserve));
        Assert.Equal(4, ArrayLength(resolve));
        Assert.Equal(8, ArrayLength(response));
    }

    [Fact]
    public void ReservationContractsRejectInvalidRevisionQuantityAndTerminalResults()
    {
        var request = new MarketTradeReservationRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            TradeId = Guid.NewGuid(),
            MarketId = "li01_01_base",
            SystemId = "li01",
            GoodId = "commodity_food",
            Side = MarketTradeSideV1.Buy,
            Quantity = 1,
            ExpectedMarketRevision = 7
        };
        var response = new MarketTradeReservationResponseV1
        {
            CorrelationId = request.CorrelationId,
            TradeId = request.TradeId,
            Status = MarketTradeReservationStatusV1.Reserved,
            QuoteUnitPrice = 100,
            TotalPrice = 100,
            MarketRevision = 8,
            SystemRevision = 9
        };

        Assert.False((request with { ExpectedMarketRevision = 0 }).IsValid());
        Assert.False((request with { Quantity = MarketTradeReservationRequestV1.MaximumQuantity + 1 }).IsValid());
        Assert.False((response with { MarketRevision = 0 }).IsValid());
        Assert.False((response with { CorrelationId = Guid.NewGuid() }).Matches(request));
        Assert.False((response with { Status = (MarketTradeReservationStatusV1)99 }).IsValid());
        Assert.False(new MarketTradeResolutionRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            TradeId = request.TradeId,
            Resolution = (MarketTradeResolutionV1)99
        }.IsValid());
    }

    [Fact]
    public void QuoteBoundReservationBindsBothRevisionsAndExactPrices()
    {
        Assert.Equal((ushort)326, (ushort)ClusterMessageType.MarketTradeQuoteReservationRequest);
        Assert.Equal((ushort)327, (ushort)ClusterMessageType.MarketTradeQuoteReservationResponse);
        var request = new MarketTradeQuoteReservationRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            TradeId = Guid.NewGuid(),
            MarketId = "li01_01_base",
            SystemId = "li01",
            GoodId = "commodity_food",
            Side = MarketTradeSideV1.Buy,
            Quantity = 3,
            ExpectedMarketRevision = 14,
            ExpectedReputationRevision = 8,
            ReputationPriceFactorPartsPerMillion = 950_000,
            ExpectedQuoteUnitPrice = 95,
            ExpectedTotalPrice = 276,
            CharacterId = 42,
            MarketFactionId = "li_grp"
        };
        var response = new MarketTradeQuoteReservationResponseV1
        {
            CorrelationId = request.CorrelationId,
            TradeId = request.TradeId,
            Status = MarketTradeReservationStatusV1.Reserved,
            QuoteUnitPrice = 95,
            TotalPrice = 276,
            MarketRevision = 15,
            SystemRevision = 22,
            ReputationRevision = request.ExpectedReputationRevision,
            ReputationPriceFactorPartsPerMillion = request.ReputationPriceFactorPartsPerMillion,
            MarketFactionId = request.MarketFactionId
        };

        Assert.True(request.IsValid());
        Assert.True(response.IsValid());
        Assert.True(response.Matches(request));
        Assert.Equal(request, RoundTrip(request));
        Assert.Equal(response, RoundTrip(response));
        Assert.Equal(15, ArrayLength(request));
        Assert.Equal(11, ArrayLength(response));
        Assert.False((request with { ExpectedReputationRevision = 0 }).IsValid());
        Assert.False((request with { ReputationPriceFactorPartsPerMillion = 2_000_000 }).IsValid());
        Assert.False((response with { ReputationRevision = 9 }).Matches(request));
        var staleReputation = response with
        {
            Status = MarketTradeReservationStatusV1.ReputationRevisionMismatch,
            QuoteUnitPrice = 0, TotalPrice = 0, MarketRevision = 0, SystemRevision = 0,
            ReputationRevision = 9, ReputationPriceFactorPartsPerMillion = 0, MarketFactionId = null
        };
        var staleMarket = response with
        {
            Status = MarketTradeReservationStatusV1.MarketRevisionMismatch,
            QuoteUnitPrice = 0, TotalPrice = 0, MarketRevision = 15, SystemRevision = 0,
            ReputationRevision = 0, ReputationPriceFactorPartsPerMillion = 0, MarketFactionId = null
        };
        Assert.True(staleReputation.IsValid());
        Assert.True(staleReputation.Matches(request));
        Assert.True(staleMarket.IsValid());
        Assert.True(staleMarket.Matches(request));
    }

    private static T RoundTrip<T>(T value) =>
        MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value));

    private static int ArrayLength<T>(T value)
    {
        var reader = new MessagePackReader(MessagePackSerializer.Serialize(value));
        return reader.ReadArrayHeader();
    }
}
