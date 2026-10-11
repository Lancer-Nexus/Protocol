using LancerNexus.Protocol;
using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class MarketTradeBundleContractsTests
{
    [Fact]
    public void ReservationBindsEveryLineToOneIdempotentTrade()
    {
        var request = BundleRequest();
        var response = new MarketTradeBundleReservationResponseV1
        {
            CorrelationId = request.CorrelationId,
            TradeId = request.TradeId,
            Status = MarketTradeReservationStatusV1.Reserved,
            SystemRevision = 31,
            ReputationRevision = request.ExpectedReputationRevision,
            MarketFactionId = request.MarketFactionId,
            Lines = request.Lines.Select(line => new MarketTradeBundleLineResultV1
            {
                GoodId = line.GoodId,
                Side = line.Side,
                Quantity = line.Quantity,
                QuoteUnitPrice = line.ExpectedQuoteUnitPrice,
                TotalPrice = line.ExpectedTotalPrice,
                MarketRevision = line.ExpectedMarketRevision
            }).ToArray()
        };

        Assert.True(request.IsValid());
        Assert.True(response.IsValidFor(request));
        var decodedRequest = MessagePackSerializer.Deserialize<MarketTradeBundleReservationRequestV1>(
            MessagePackSerializer.Serialize(request));
        Assert.Equal(request.CorrelationId, decodedRequest.CorrelationId);
        Assert.Equal(request.TradeId, decodedRequest.TradeId);
        Assert.Equal(request.Lines, decodedRequest.Lines);
        Assert.False(response.IsValidFor(request with { TradeId = Guid.NewGuid() }));
        Assert.False((response with { Lines = [response.Lines[0]] }).IsValidFor(request));
        Assert.False((response with { Lines = [response.Lines[0] with { TotalPrice = 1 }, response.Lines[1]] })
            .IsValidFor(request));
    }

    [Fact]
    public void ReservationRejectsUnsortedDuplicateAndOversizedLines()
    {
        var request = BundleRequest();
        Assert.False((request with { Lines = request.Lines.Reverse().ToArray() }).IsValid());
        Assert.False((request with { Lines = [request.Lines[0], request.Lines[0]] }).IsValid());
        Assert.False((request with
        {
            Lines = Enumerable.Range(0, MarketTradeBundleReservationRequestV1.MaximumLines + 1)
                .Select(index => BundleLine($"good_{index:D2}", MarketTradeSideV1.Buy)).ToArray()
        }).IsValid());
        Assert.False((request with { Lines = [request.Lines[0] with { ReputationPriceFactorPartsPerMillion = 1 }] })
            .IsValid());
    }

    [Fact]
    public void RevisionMismatchIsAtomicAndBundleEventsCarryOnlyMarketEffects()
    {
        var request = BundleRequest();
        var mismatch = new MarketTradeBundleReservationResponseV1
        {
            CorrelationId = request.CorrelationId,
            TradeId = request.TradeId,
            Status = MarketTradeReservationStatusV1.MarketRevisionMismatch,
            CurrentMarketRevision = 23,
            CurrentMarketGoodId = request.Lines[0].GoodId,
            CurrentMarketSide = request.Lines[0].Side
        };
        var trade = new MarketTradeBundleEventV1
        {
            EventId = Guid.NewGuid(),
            TradeId = request.TradeId,
            InstanceId = "li01",
            ProducerEpoch = Guid.NewGuid(),
            Sequence = 7,
            OccurredUtc = DateTimeOffset.UtcNow,
            MarketId = request.MarketId,
            SystemId = request.SystemId,
            Lines = request.Lines.Select(line => new MarketTradeBundleEventLineV1
            {
                GoodId = line.GoodId,
                Side = line.Side,
                Quantity = line.Quantity
            }).ToArray()
        };
        var acknowledgement = new MarketTradeBundleEventAcknowledgementV1
        {
            CorrelationId = Guid.NewGuid(),
            EventId = trade.EventId,
            TradeId = trade.TradeId,
            InstanceId = trade.InstanceId,
            ProducerEpoch = trade.ProducerEpoch,
            Sequence = trade.Sequence,
            Status = MarketTradeIngestStatusV1.Applied
        };

        Assert.True(mismatch.IsValidFor(request));
        Assert.False((mismatch with { CurrentMarketGoodId = "unknown_good" }).IsValidFor(request));
        Assert.False((mismatch with { CurrentMarketSide = MarketTradeSideV1.Sell }).IsValidFor(request));
        Assert.True(trade.IsValid());
        Assert.True(acknowledgement.IsValidFor(trade));
        Assert.False(acknowledgement.IsValidFor(trade with { TradeId = Guid.NewGuid() }));
        Assert.False((trade with { Lines = trade.Lines.Reverse().ToArray() }).IsValid());
        var decodedTrade = MessagePackSerializer.Deserialize<MarketTradeBundleEventV1>(
            MessagePackSerializer.Serialize(trade));
        Assert.Equal(trade.EventId, decodedTrade.EventId);
        Assert.Equal(trade.TradeId, decodedTrade.TradeId);
        Assert.Equal(trade.Lines, decodedTrade.Lines);
    }

    private static MarketTradeBundleReservationRequestV1 BundleRequest() => new()
    {
        CorrelationId = Guid.NewGuid(),
        TradeId = Guid.NewGuid(),
        MarketId = "li01_01_base",
        SystemId = "li01",
        CharacterId = 42,
        ExpectedReputationRevision = 9,
        MarketFactionId = "li_grp",
        Lines =
        [
            BundleLine("package_fighter", MarketTradeSideV1.Buy),
            BundleLine("shield01", MarketTradeSideV1.Sell)
        ]
    };

    private static MarketTradeBundleLineV1 BundleLine(string goodId, MarketTradeSideV1 side) => new()
    {
        GoodId = goodId,
        Side = side,
        Quantity = 1,
        ExpectedMarketRevision = 3,
        ExpectedQuoteUnitPrice = 100,
        ExpectedTotalPrice = 100,
        ReputationPriceFactorPartsPerMillion = side == MarketTradeSideV1.Buy ? 950_000 : 1_050_000
    };
}
