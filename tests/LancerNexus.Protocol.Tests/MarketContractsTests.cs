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
        Assert.Equal((ushort)320, (ushort)ClusterMessageType.MarketCatalogReadRequest);
        Assert.Equal((ushort)321, (ushort)ClusterMessageType.MarketCatalogReadResponse);
        Assert.Equal((ushort)322, (ushort)ClusterMessageType.MarketQuoteRequest);
        Assert.Equal((ushort)323, (ushort)ClusterMessageType.MarketQuoteResponse);
        Assert.Equal((ushort)324, (ushort)ClusterMessageType.MarketGoodReadRequest);
        Assert.Equal((ushort)325, (ushort)ClusterMessageType.MarketGoodReadResponse);
        Assert.Equal((ushort)328, (ushort)ClusterMessageType.MarketTradeBundleReservationRequest);
        Assert.Equal((ushort)329, (ushort)ClusterMessageType.MarketTradeBundleReservationResponse);
        Assert.Equal((ushort)330, (ushort)ClusterMessageType.MarketTradeBundleEvent);
        Assert.Equal((ushort)331, (ushort)ClusterMessageType.MarketTradeBundleEventAcknowledgement);
        Assert.Equal("dynamic_market_v1", ClusterCapabilities.DynamicMarketV1);
    }

    [Fact]
    public void MarketGoodReadContractsBindTheExactLocationAndBothRevisions()
    {
        var request = new MarketGoodReadRequestV1
        {
            CorrelationId = Guid.NewGuid(), SystemId = "li01", MarketId = "li01_01_base",
            GoodId = "commodity_food", ExpectedSystemRevision = 12, ExpectedMarketRevision = 8
        };
        var good = Good(request.MarketId, request.GoodId) with { MarketRevision = 8 };
        var response = new MarketGoodReadResponseV1
        {
            CorrelationId = request.CorrelationId, SystemId = request.SystemId,
            MarketId = request.MarketId, GoodId = request.GoodId,
            Status = MarketGoodReadStatusV1.Found, SystemRevision = 12,
            MarketRevision = 8, Good = good
        };

        Assert.True(request.IsValid());
        Assert.True(response.IsValidFor(request));
        Assert.Equal(request, MessagePackSerializer.Deserialize<MarketGoodReadRequestV1>(
            MessagePackSerializer.Serialize(request)));
        Assert.False(response.IsValidFor(request with { MarketId = "li01_02_base" }));
        Assert.False(response.IsValidFor(request with { ExpectedSystemRevision = 13 }));
        Assert.False((response with { Good = good with { MarketRevision = 9 } }).IsValidFor(request));
        Assert.False((request with { GoodId = "COMMODITY_FOOD" }).IsValid());
    }

    [Fact]
    public void MarketGoodReadRevisionMismatchAndNotFoundResponsesAreExplicit()
    {
        var request = new MarketGoodReadRequestV1
        {
            CorrelationId = Guid.NewGuid(), SystemId = "li01", MarketId = "li01_01_base",
            GoodId = "commodity_food", ExpectedSystemRevision = 12
        };
        var mismatch = new MarketGoodReadResponseV1
        {
            CorrelationId = request.CorrelationId, SystemId = request.SystemId,
            MarketId = request.MarketId, GoodId = request.GoodId,
            Status = MarketGoodReadStatusV1.RevisionMismatch, SystemRevision = 13
        };
        var missing = mismatch with { Status = MarketGoodReadStatusV1.NotFound, SystemRevision = 12 };

        Assert.True(mismatch.IsValidFor(request));
        Assert.True(missing.IsValidFor(request));
        Assert.False((missing with { SystemRevision = 0 }).IsValidFor(request));
    }

    [Fact]
    public void MarketQuoteContractsBindIdentityRevisionsAndNeverCarryReputationInRequest()
    {
        var request = new MarketQuoteRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            CharacterId = 42,
            SystemId = "li-01",
            MarketId = "li01_base",
            GoodId = "commodity_food",
            Side = MarketTradeSideV1.Buy,
            Quantity = 3,
            ExpectedMarketRevision = 7,
            ExpectedReputationRevision = 9
        };
        var response = new MarketQuoteResponseV1
        {
            CorrelationId = request.CorrelationId,
            CharacterId = request.CharacterId,
            SystemId = request.SystemId,
            MarketId = request.MarketId,
            GoodId = request.GoodId,
            Side = request.Side,
            Quantity = request.Quantity,
            Status = MarketQuoteStatusV1.Quoted,
            MarketRevision = 7,
            ReputationRevision = 9,
            MarketFactionId = "li_grp",
            AppliedReputation = 0.5f,
            ReputationFactorPartsPerMillion = 950_000,
            QuoteUnitPrice = 95,
            TotalPrice = 285,
            ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(15),
            PricingPolicyVersion = 1
        };
        Assert.True(request.IsValid());
        Assert.True(response.IsValidFor(request));
        Assert.False(response.IsValidFor(request with { CharacterId = 43 }));
        Assert.False(response.IsValidFor(request with { ExpectedReputationRevision = 10 }));
        Assert.False(response.IsValidFor(request with { ExpectedMarketRevision = 8 }));
        Assert.False((request with { Quantity = MarketTradeEventV1.MaximumQuantity + 1 }).IsValid());
        Assert.False((response with { ReputationFactorPartsPerMillion = 500_000 }).IsValidFor(request));
    }

    [Fact]
    public void GameInstanceMarketQuoteRequiresLeaseFenceAndProjectsOnlyQuoteInputs()
    {
        var request = new MarketTradeQuoteReadRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            CharacterId = 42,
            LeaseToken = new string('A', 43),
            LeaseVersion = 3,
            SystemId = "li-01",
            MarketId = "li01_base",
            GoodId = "commodity_food",
            Side = MarketTradeSideV1.Buy,
            Quantity = 2,
            ExpectedMarketRevision = 7
        };

        Assert.True(request.IsValid());
        Assert.Equal(request, MessagePackSerializer.Deserialize<MarketTradeQuoteReadRequestV1>(
            MessagePackSerializer.Serialize(request)));
        Assert.False((request with { LeaseToken = "invalid" }).IsValid());
        Assert.False((request with { LeaseVersion = 0 }).IsValid());
        Assert.False((request with { ExpectedMarketRevision = 0 }).IsValid());

        var quoteRequest = request.ToMarketQuoteRequest(expectedReputationRevision: 9);
        Assert.True(quoteRequest.IsValid());
        Assert.Equal(request.CorrelationId, quoteRequest.CorrelationId);
        Assert.Equal(request.CharacterId, quoteRequest.CharacterId);
        Assert.Equal(request.ExpectedMarketRevision, quoteRequest.ExpectedMarketRevision);
        Assert.Equal(9, quoteRequest.ExpectedReputationRevision);

        var quote = new MarketQuoteResponseV1
        {
            CorrelationId = request.CorrelationId,
            CharacterId = request.CharacterId,
            SystemId = request.SystemId,
            MarketId = request.MarketId,
            GoodId = request.GoodId,
            Side = request.Side,
            Quantity = request.Quantity,
            Status = MarketQuoteStatusV1.Quoted,
            MarketRevision = 7,
            ReputationRevision = 9,
            MarketFactionId = "li_grp",
            AppliedReputation = 0.5f,
            ReputationFactorPartsPerMillion = 950_000,
            QuoteUnitPrice = 95,
            TotalPrice = 190,
            ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(15),
            PricingPolicyVersion = 1
        };
        var response = new MarketTradeQuoteReadResponseV1
        {
            CorrelationId = request.CorrelationId,
            CharacterId = request.CharacterId,
            LeaseVersion = request.LeaseVersion,
            ExpectedReputationRevision = 9,
            Quote = quote
        };
        Assert.True(response.IsValidFor(request));
        Assert.Equal(response, MessagePackSerializer.Deserialize<MarketTradeQuoteReadResponseV1>(
            MessagePackSerializer.Serialize(response)));
        Assert.False(response.IsValidFor(request with { LeaseVersion = 4 }));
        Assert.False((response with { ExpectedReputationRevision = 10 }).IsValidFor(request));
    }

    [Fact]
    public void CatalogReadContractsPinBoundedPagesToSeedRevision()
    {
        var revision = new string('a', 64);
        var request = new MarketCatalogReadRequestV1 { CorrelationId = Guid.NewGuid(), PageSize = 1 };
        var response = new MarketCatalogReadResponseV1
        {
            CorrelationId = request.CorrelationId,
            Status = MarketCatalogReadStatusV1.Ok,
            CatalogRevision = revision,
            Goods = [new MarketCatalogGoodV1 { GoodId = "commodity_a", Category = "commodity" }],
            NextGoodId = "commodity_a"
        };
        Assert.True(response.IsValidFor(request));
        var continuation = request with
        {
            CorrelationId = Guid.NewGuid(),
            AfterGoodId = "commodity_a",
            ExpectedCatalogRevision = revision
        };
        Assert.True(continuation.IsValid());
        Assert.False((request with { AfterGoodId = "commodity_a" }).IsValid());
        Assert.False((request with { ExpectedCatalogRevision = "stale" }).IsValid());
        Assert.False(response.IsValidFor(continuation));
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
            CorrelationId = Guid.NewGuid(),
            SystemId = "li01",
            PageSize = 2
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
            CorrelationId = Guid.NewGuid(),
            SystemId = "li01",
            PageSize = 2,
            ExpectedSystemRevision = 12,
            AfterMarketId = "li01_01_base",
            AfterGoodId = "commodity_food"
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
            EventId = Guid.NewGuid(),
            InstanceId = "li-01",
            ProducerEpoch = Guid.NewGuid(),
            Sequence = 42,
            OccurredUtc = DateTimeOffset.UtcNow,
            MarketId = "li01_01_base",
            SystemId = "li01",
            GoodId = "commodity_food",
            Side = MarketTradeSideV1.Buy,
            Quantity = 3
        };
        var acknowledgement = new MarketTradeEventAcknowledgementV1
        {
            EventId = trade.EventId,
            InstanceId = trade.InstanceId,
            ProducerEpoch = trade.ProducerEpoch,
            Sequence = trade.Sequence,
            Status = MarketTradeIngestStatusV1.Applied,
            MarketRevision = 9
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
            EventId = Guid.NewGuid(),
            InstanceId = "li-01",
            ProducerEpoch = Guid.NewGuid(),
            Sequence = 1,
            OccurredUtc = DateTimeOffset.UtcNow,
            MarketId = "li01_01_base",
            SystemId = "li01",
            GoodId = "commodity_food",
            Side = MarketTradeSideV1.Buy,
            Quantity = 1
        };

        Assert.False((valid with { EventId = Guid.Empty }).IsValid());
        Assert.False((valid with { GoodId = "Commodity_Food" }).IsValid());
        Assert.False((valid with
        {
            OccurredUtc = new DateTimeOffset(2026, 10, 11, 12, 0, 0,
            TimeSpan.FromHours(1))
        }).IsValid());
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
