using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class FlHookEventContractsTests
{
    [Fact]
    public void EventRoundTripsWithStableFieldOrder()
    {
        var value = Event(FlHookEventKind.PlayerSystemChanged) with { SystemId = "li02" };

        Assert.True(value.IsValid());
        var bytes = MessagePackSerializer.Serialize(value);
        var decoded = MessagePackSerializer.Deserialize<FlHookEventV1>(bytes);
        Assert.Equal(value, decoded);
        Assert.Equal(7, new MessagePackReader(bytes).ReadArrayHeader());
        Assert.Equal(1, decoded.SchemaVersion);
    }

    [Fact]
    public void RejectsMalformedEventsAndOutOfRangeReplayRequests()
    {
        Assert.False(Event((FlHookEventKind)99).IsValid());
        Assert.False((Event(FlHookEventKind.PlayerConnected) with { SystemId = "li02" }).IsValid());
        Assert.False(Event(FlHookEventKind.PlayerSystemChanged).IsValid());
        Assert.False((Event(FlHookEventKind.PlayerConnected) with { OccurredUtc = DateTimeOffset.Now }).IsValid());
        Assert.False(new FlHookEventReplayRequestV1 { CorrelationId = Guid.NewGuid(), InstanceId = "li-01", MaximumEvents = 501 }.IsValid());
        Assert.False(new FlHookEventReplayRequestV1 { CorrelationId = Guid.NewGuid(), InstanceId = "li-01", AfterSequence = -1 }.IsValid());
    }

    [Fact]
    public void ReplayResultRequiresOrderedEventsFromTheDeclaredInstance()
    {
        var result = new FlHookEventReplayResultV1
        {
            CorrelationId = Guid.NewGuid(), InstanceId = "li-01", OldestAvailableSequence = 4,
            LatestSequence = 5, RequestedSequence = 2, Gap = true,
            Events = [Event(FlHookEventKind.PlayerConnected) with { Sequence = 4, InstanceId = "li-01" }]
        };

        Assert.True(result.IsValid());
        Assert.False((result with { Events = [result.Events[0] with { InstanceId = "li-02" }] }).IsValid());
        Assert.False((result with { Events = [result.Events[0], result.Events[0]] }).IsValid());
        Assert.False((result with { Events = [result.Events[0] with { Sequence = 3 }] }).IsValid());
        Assert.False((result with { RequestedSequence = 3 }).IsValid());
        Assert.True(new FlHookEventReplayResultV1
        {
            CorrelationId = Guid.NewGuid(), InstanceId = "li-01", Events = []
        }.IsValid());
    }

    private static FlHookEventV1 Event(FlHookEventKind kind) => new()
    {
        InstanceId = "li-01", Sequence = 1, OccurredUtc = DateTimeOffset.UnixEpoch,
        Kind = kind, PlayerId = "player-01"
    };
}
