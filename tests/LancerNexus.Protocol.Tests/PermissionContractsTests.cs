using LancerNexus.Protocol;
using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class PermissionContractsTests
{
    [Fact]
    public void PermissionRevisionMessagesUseStableExplicitKeys()
    {
        var changed = new PermissionRevisionChanged
        { EventId = Guid.NewGuid(), Revision = 42, CreatedUtc = DateTimeOffset.Parse("2026-09-27T12:00:00Z") };
        var changedCopy = MessagePackSerializer.Deserialize<PermissionRevisionChanged>(MessagePackSerializer.Serialize(changed));
        Assert.Equal(changed.EventId, changedCopy.EventId);
        Assert.Equal(changed.Revision, changedCopy.Revision);
        var ack = new PermissionRevisionAcknowledged
        { InstanceId = "li-01", Revision = 42, AppliedUtc = changed.CreatedUtc };
        var ackCopy = MessagePackSerializer.Deserialize<PermissionRevisionAcknowledged>(MessagePackSerializer.Serialize(ack));
        Assert.Equal(ack.InstanceId, ackCopy.InstanceId);
        Assert.Equal(ack.Revision, ackCopy.Revision);
    }
}
