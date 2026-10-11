using LancerNexus.Protocol;
using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class MarketFactionReputationContractsTests
{
    [Fact]
    public void MessageIdsAndRevisionBoundResponseAreStable()
    {
        Assert.Equal((ushort)318, (ushort)ClusterMessageType.MarketFactionReputationReadRequest);
        Assert.Equal((ushort)319, (ushort)ClusterMessageType.MarketFactionReputationReadResponse);
        var request = new MarketFactionReputationReadRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            CharacterId = 43,
            ExpectedReputationRevision = 8
        };
        var response = new MarketFactionReputationReadResponseV1
        {
            CorrelationId = request.CorrelationId,
            CharacterId = request.CharacterId,
            Status = MarketFactionReputationStatusV1.Found,
            ReputationRevision = 8,
            Factions = [new MarketFactionReputationV1 { FactionId = "li_n_grp", Value = 0.5f }]
        };

        var roundtrip = MessagePackSerializer.Deserialize<MarketFactionReputationReadResponseV1>(
            MessagePackSerializer.Serialize(response));

        Assert.True(request.IsValid());
        Assert.True(roundtrip.IsValidFor(request));
    }

    [Fact]
    public void RejectsMismatchedRevisionAndOutOfRangeReputation()
    {
        var request = new MarketFactionReputationReadRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            CharacterId = 43,
            ExpectedReputationRevision = 8
        };
        var stale = new MarketFactionReputationReadResponseV1
        {
            CorrelationId = request.CorrelationId,
            CharacterId = request.CharacterId,
            Status = MarketFactionReputationStatusV1.Found,
            ReputationRevision = 9,
            Factions = [new MarketFactionReputationV1 { FactionId = "li_n_grp", Value = 0.5f }]
        };
        var invalid = stale with
        {
            ReputationRevision = 8,
            Factions = [new MarketFactionReputationV1 { FactionId = "li_n_grp", Value = 1.5f }]
        };

        Assert.False(stale.IsValidFor(request));
        Assert.False(invalid.IsValidFor(request));
    }
}
