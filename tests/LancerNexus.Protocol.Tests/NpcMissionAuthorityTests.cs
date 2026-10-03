using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class NpcMissionAuthorityTests
{
    [Theory]
    [InlineData(NpcTransferState.Committed, 15L)]
    [InlineData(NpcTransferState.Aborted, null)]
    public void DecisionsRoundTripWithExplicitKeys(NpcTransferState state, long? version)
    {
        var request = new NpcMissionAuthorityRequestV1
        {
            TransferId = Guid.NewGuid(), SourceInstanceId = "source", TargetInstanceId = "target",
            TargetSystemId = "li02", Decision = state
        };
        var reply = new NpcMissionAuthorityResultV1
        {
            TransferId = request.TransferId, SourceInstanceId = "source", TargetInstanceId = "target",
            TargetSystemId = "LI02", Decision = state, Accepted = true, CommittedLeaseVersion = version
        };
        var bytes = MessagePackSerializer.Serialize(request);
        Assert.Equal(6, new MessagePackReader(bytes).ReadArrayHeader());
        Assert.Equal(request, MessagePackSerializer.Deserialize<NpcMissionAuthorityRequestV1>(bytes));
        bytes = MessagePackSerializer.Serialize(reply);
        Assert.Equal(9, new MessagePackReader(bytes).ReadArrayHeader());
        Assert.True(MessagePackSerializer.Deserialize<NpcMissionAuthorityResultV1>(bytes).Authorizes(request));
        Assert.False((reply with { TransferId = Guid.NewGuid() }).Authorizes(request));
        Assert.False((reply with { Accepted = false }).Authorizes(request));
        Assert.False((reply with { SchemaVersion = 2 }).Authorizes(request));
        Assert.False((reply with { TargetInstanceId = "other" }).Authorizes(request));
        Assert.False((reply with { SourceInstanceId = "other" }).Authorizes(request));
        Assert.False((reply with { TargetSystemId = "li03" }).Authorizes(request));
        Assert.False((reply with { Decision = NpcTransferState.SourceFrozen }).Authorizes(request));
        Assert.False((reply with { CommittedLeaseVersion = state == NpcTransferState.Committed ? null : 15 }).Authorizes(request));
        Assert.False((request with { SchemaVersion = 2 }).IsValid());
        Assert.False((request with { Decision = NpcTransferState.TargetAccepted }).IsValid());
        Assert.False((request with { TransferId = Guid.Empty }).IsValid());
        Assert.False((request with { TargetInstanceId = "source" }).IsValid());
    }
}
