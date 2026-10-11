using LancerNexus.Protocol;
using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class MarketContractsTests
{
    [Fact]
    public void MarketMessageIdsAndCapabilityAreStable()
    {
        Assert.Equal((ushort)310, (ushort)ClusterMessageType.MarketSnapshotReadRequest);
        Assert.Equal((ushort)311, (ushort)ClusterMessageType.MarketSnapshotReadResponse);
        Assert.Equal((ushort)312, (ushort)ClusterMessageType.MarketTradeEvent);
        Assert.Equal((ushort)313, (ushort)ClusterMessageType.MarketTradeEventAcknowledgement);
        Assert.Equal("dynamic_market_v1", ClusterCapabilities.DynamicMarketV1);
    }

    private static MarketGoodSnapshotV1 Good(string marketId, string goodId) => new()
    {
        MarketId = marketId,
        SystemId = "li01",
        GoodId = goodId,
        BaseUnitPrice = 100,
        CurrentUnitPrice = 125,
        AvailableStock = 40,
        TargetStock = 100,
        BuyEnabled = true,
        SellEnabled = true,
        MarketRevision = 8
    };

    [Fact]
    public void SnapshotContractsRoundTripWithStableExplicitArrayKeys()
    {
        var request = new MarketSnapshotReadRequestV1
        {
            CorrelationId = Guid.NewGuid(), SystemId = "li01", PageSize = 2
        };
        var response = new MarketSnapshotReadResponseV1
        {
            CorrelationId = request.CorrelationId,
            SystemId = request.SystemId,
            Status = MarketSnapshotReadStatusV1.Ok,
            SystemRevision = 12,
            Goods = [Good("li01_01_base", "commodity_food"), Good("li01_02_base", "commodity_food")],
            NextMarketId = "li01_02_base",
            NextGoodId = "commodity_food"
        };

        Assert.True(request.IsValid());
        Assert.True(response.IsValidFor(request));
        Assert.Equal(request, MessagePackSerializer.Deserialize<MarketSnapshotReadRequestV1>(
            MessagePackSerializer.Serialize(request)));
        var decodedResponse = MessagePackSerializer.Deserialize<MarketSnapshotReadResponseV1>(
            MessagePackSerializer.Serialize(response));
        Assert.Equal(response.CorrelationId, decodedResponse.CorrelationId);
        Assert.Equal(response.SystemRevision, decodedResponse.SystemRevision);
        Assert.Equal(response.NextMarketId, decodedResponse.NextMarketId);
        Assert.Equal(response.Goods, decodedResponse.Goods);
        Assert.Equal(7, ArrayLength(request));
        Assert.Equal(8, ArrayLength(response));
        Assert.Equal(10, ArrayLength(response.Goods[0]));
    }

    [Fact]
    public void SnapshotRejectsMixedRevisionPagesAndInvalidCursors()
    {
        var request = new MarketSnapshotReadRequestV1
        {
            CorrelationId = Guid.NewGuid(), SystemId = "li01", PageSize = 2,
            ExpectedSystemRevision = 12, AfterMarketId = "li01_01_base", AfterGoodId = "commodity_food"
        };
        var response = new MarketSnapshotReadResponseV1
        {
            CorrelationId = request.CorrelationId,
            SystemId = request.SystemId,
            Status = MarketSnapshotReadStatusV1.Ok,
            SystemRevision = 13,
            Goods = [Good("li01_02_base", "commodity_food")]
        };

        Assert.False(response.IsValidFor(request));
        Assert.False((request with { AfterGoodId = null }).IsValid());
        Assert.False((request with { ExpectedSystemRevision = null }).IsValid());
        Assert.False((request with { PageSize = MarketSnapshotReadRequestV1.MaximumPageSize + 1 }).IsValid());
        Assert.False((request with { SystemId = "LI01" }).IsValid());
        Assert.False((response with
        {
            SystemRevision = 12,
            Goods = [Good("li01_01_base", "commodity_food"), Good("li01_01_base", "commodity_food")]
        }).IsValidFor(request));
    }

    [Fact]
    public void TradeEventAcknowledgementBindsProducerSequenceAndRetrySemantics()
    {
        var trade = new MarketTradeEventV1
        {
            EventId = Guid.NewGuid(), InstanceId = "li-01", ProducerEpoch = Guid.NewGuid(), Sequence = 42,
            OccurredUtc = DateTimeOffset.UtcNow, MarketId = "li01_01_base", SystemId = "li01",
            GoodId = "commodity_food", Side = MarketTradeSideV1.Buy, Quantity = 3
        };
        var acknowledgement = new MarketTradeEventAcknowledgementV1
        {
            EventId = trade.EventId, InstanceId = trade.InstanceId, ProducerEpoch = trade.ProducerEpoch,
            Sequence = trade.Sequence, Status = MarketTradeIngestStatusV1.Applied, MarketRevision = 9
        };
        var unavailable = acknowledgement with { Status = MarketTradeIngestStatusV1.Unavailable, MarketRevision = 0 };
        var duplicate = acknowledgement with { Status = MarketTradeIngestStatusV1.Duplicate };

        Assert.True(trade.IsValid());
        Assert.True(acknowledgement.IsValid());
        Assert.True(acknowledgement.IsDurablyAccepted);
        Assert.True(acknowledgement.Matches(trade));
        Assert.False(acknowledgement.Matches(trade with { Sequence = trade.Sequence + 1 }));
        Assert.False(unavailable.IsDurablyAccepted);
        Assert.True(unavailable.IsRetryable);
        Assert.True(duplicate.IsDurablyAccepted);
        Assert.Equal(trade, MessagePackSerializer.Deserialize<MarketTradeEventV1>(
            MessagePackSerializer.Serialize(trade)));
        Assert.Equal(acknowledgement, MessagePackSerializer.Deserialize<MarketTradeEventAcknowledgementV1>(
            MessagePackSerializer.Serialize(acknowledgement)));
        Assert.Equal(11, ArrayLength(trade));
        Assert.Equal(7, ArrayLength(acknowledgement));
    }

    [Fact]
    public void TradeEventRejectsInvalidIdentityTimeSideAndQuantity()
    {
        var valid = new MarketTradeEventV1
        {
            EventId = Guid.NewGuid(), InstanceId = "li-01", ProducerEpoch = Guid.NewGuid(), Sequence = 1,
            OccurredUtc = DateTimeOffset.UtcNow, MarketId = "li01_01_base", SystemId = "li01",
            GoodId = "commodity_food", Side = MarketTradeSideV1.Buy, Quantity = 1
        };

        Assert.False((valid with { EventId = Guid.Empty }).IsValid());
        Assert.False((valid with { GoodId = "Commodity_Food" }).IsValid());
        Assert.False((valid with { OccurredUtc = new DateTimeOffset(2026, 10, 11, 12, 0, 0,
            TimeSpan.FromHours(1)) }).IsValid());
        Assert.False((valid with { Side = (MarketTradeSideV1)99 }).IsValid());
        Assert.False((valid with { Quantity = 0 }).IsValid());
        Assert.False((valid with { Quantity = MarketTradeEventV1.MaximumQuantity + 1 }).IsValid());
    }

    private static int ArrayLength<T>(T value)
    {
        var reader = new MessagePackReader(MessagePackSerializer.Serialize(value));
        return reader.ReadArrayHeader();
    }
}
