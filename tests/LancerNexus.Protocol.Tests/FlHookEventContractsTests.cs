using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class FlHookEventContractsTests
{
    [Fact]
    public void ClusterMessageIdsAndCapabilityAreStable()
    {
        Assert.Equal((ushort)302, (ushort)ClusterMessageType.FlHookEvent);
        Assert.Equal((ushort)303, (ushort)ClusterMessageType.FlHookEventReplayRequest);
        Assert.Equal((ushort)304, (ushort)ClusterMessageType.FlHookEventReplayResult);
        Assert.Equal((ushort)305, (ushort)ClusterMessageType.FlHookEventIngestResult);
        Assert.Equal("flhook_events_v1", ClusterCapabilities.FlHookEventsV1);
    }

    [Theory]
    [InlineData(FlHookEventIngestStatusV1.Stored, true)]
    [InlineData(FlHookEventIngestStatusV1.Duplicate, true)]
    [InlineData(FlHookEventIngestStatusV1.Conflict, false)]
    [InlineData(FlHookEventIngestStatusV1.Rejected, false)]
    [InlineData(FlHookEventIngestStatusV1.Unavailable, false)]
    public void IngestResultRoundTripsAndOnlyDurableOutcomesAdvanceProducer(FlHookEventIngestStatusV1 status, bool accepted)
    {
        var result = new FlHookEventIngestResultV1
        {
            CorrelationId = Guid.NewGuid(),
            InstanceId = "li-01",
            ProducerEpoch = Guid.NewGuid(),
            Sequence = 42,
            Status = status
        };

        Assert.True(result.IsValid());
        var bytes = MessagePackSerializer.Serialize(result);
        var decoded = MessagePackSerializer.Deserialize<FlHookEventIngestResultV1>(bytes);
        Assert.Equal(result, decoded);
        Assert.Equal(6, new MessagePackReader(bytes).ReadArrayHeader());
        Assert.Equal(accepted, decoded.IsDurablyAccepted);
    }

    [Fact]
    public void IngestResultRejectsInvalidIdentityAndUnknownStatus()
    {
        var valid = new FlHookEventIngestResultV1
        {
            CorrelationId = Guid.NewGuid(),
            InstanceId = "li-01",
            ProducerEpoch = Guid.NewGuid(),
            Sequence = 1,
            Status = FlHookEventIngestStatusV1.Stored
        };

        Assert.False((valid with { ProducerEpoch = Guid.Empty }).IsValid());
        Assert.False((valid with { Sequence = 0 }).IsValid());
        Assert.False((valid with { Status = (FlHookEventIngestStatusV1)99 }).IsValid());
    }

    [Fact]
    public void EventRoundTripsWithStableFieldOrder()
    {
        var value = Event(FlHookEventKind.PlayerSystemChanged) with
        {
            SystemId = "li02",
            CharacterId = 123456,
            GatewayLeaseVersion = 12
        };

        Assert.True(value.IsValid());
        var bytes = MessagePackSerializer.Serialize(value);
        var decoded = MessagePackSerializer.Deserialize<FlHookEventV1>(bytes);
        Assert.Equal(value, decoded);
        Assert.Equal(11, new MessagePackReader(bytes).ReadArrayHeader());
        Assert.Equal(1, decoded.SchemaVersion);
    }

    [Theory]
    [InlineData(FlHookEventKind.CharacterConnected)]
    [InlineData(FlHookEventKind.CharacterDisconnected)]
    public void CharacterLifecycleEventsRoundTripWithStableCharacterIdentity(FlHookEventKind kind)
    {
        var value = Event(kind) with { CharacterId = 123456, GatewayLeaseVersion = 12 };

        Assert.True(value.IsValid());
        var bytes = MessagePackSerializer.Serialize(value);
        var decoded = MessagePackSerializer.Deserialize<FlHookEventV1>(bytes);

        Assert.Equal(value, decoded);
        Assert.Equal(11, new MessagePackReader(bytes).ReadArrayHeader());
    }

    [Fact]
    public void LegacySevenFieldEventRemainsReadable()
    {
        var legacyBytes = MessagePackSerializer.Serialize(new LegacyFlHookEventV1
        {
            SchemaVersion = 1,
            InstanceId = "li-01",
            Sequence = 1,
            OccurredUtc = DateTimeOffset.UnixEpoch,
            Kind = FlHookEventKind.PlayerConnected,
            PlayerId = "player-01"
        });

        var decoded = MessagePackSerializer.Deserialize<FlHookEventV1>(legacyBytes);

        Assert.Null(decoded.CharacterId);
        Assert.Equal(Guid.Empty, decoded.ProducerEpoch);
        Assert.Null(decoded.GatewaySessionId);
        Assert.Null(decoded.GatewayLeaseVersion);
        Assert.True(decoded.IsValid());
    }

    [Fact]
    public void PreviousNineFieldEventRemainsReadableAndNewFieldsAreIgnoredByOldReaders()
    {
        var previous = MessagePackSerializer.Serialize(new PreviousFlHookEventV1
        {
            SchemaVersion = 1,
            InstanceId = "li-01",
            Sequence = 7,
            OccurredUtc = DateTimeOffset.UnixEpoch,
            Kind = FlHookEventKind.CharacterDisconnected,
            PlayerId = "player-01",
            CharacterId = 123,
            ProducerEpoch = Guid.NewGuid()
        });

        var decoded = MessagePackSerializer.Deserialize<FlHookEventV1>(previous);
        Assert.True(decoded.IsValid());
        Assert.Null(decoded.GatewaySessionId);
        Assert.Null(decoded.GatewayLeaseVersion);

        var current = Event(FlHookEventKind.CharacterDisconnected) with
        {
            CharacterId = 123,
            GatewayLeaseVersion = 12
        };
        var oldReader = MessagePackSerializer.Deserialize<PreviousFlHookEventV1>(
            MessagePackSerializer.Serialize(current));
        Assert.Equal(current.CharacterId, oldReader.CharacterId);
        Assert.Equal(current.ProducerEpoch, oldReader.ProducerEpoch);
    }

    [Fact]
    public void ReplayAcrossProducerRestartReportsEpochGap()
    {
        var currentEpoch = Guid.NewGuid();
        var previousEpoch = Guid.NewGuid();
        var result = new FlHookEventReplayResultV1
        {
            CorrelationId = Guid.NewGuid(),
            InstanceId = "li-01",
            OldestAvailableSequence = 1,
            LatestSequence = 2,
            RequestedSequence = 42,
            Gap = true,
            ProducerEpoch = currentEpoch,
            RequestedProducerEpoch = previousEpoch,
            Events =
            [
                Event(FlHookEventKind.PlayerConnected) with { Sequence = 1, ProducerEpoch = currentEpoch },
                Event(FlHookEventKind.PlayerDisconnected) with { Sequence = 2, ProducerEpoch = currentEpoch }
            ]
        };

        Assert.True(result.IsValid());
        Assert.False((result with { Gap = false }).IsValid());
        Assert.False((result with
        {
            Events = [result.Events[0] with { ProducerEpoch = previousEpoch }, result.Events[1]]
        }).IsValid());
    }

    [Fact]
    public void RejectsMalformedEventsAndOutOfRangeReplayRequests()
    {
        Assert.False(Event((FlHookEventKind)99).IsValid());
        Assert.False((Event(FlHookEventKind.PlayerConnected) with { SystemId = "li02" }).IsValid());
        Assert.False(Event(FlHookEventKind.PlayerSystemChanged).IsValid());
        Assert.False((Event(FlHookEventKind.CharacterConnected) with { CharacterId = 0 }).IsValid());
        Assert.False(Event(FlHookEventKind.CharacterDisconnected).IsValid());
        Assert.False((Event(FlHookEventKind.CharacterConnected) with { CharacterId = 123, SystemId = "li02" }).IsValid());
        Assert.False((Event(FlHookEventKind.PlayerConnected) with { CharacterId = 123 }).IsValid());
        Assert.False((Event(FlHookEventKind.PlayerConnected) with { GatewaySessionId = Guid.Empty }).IsValid());
        Assert.False((Event(FlHookEventKind.CharacterConnected) with
        {
            CharacterId = 123,
            GatewayLeaseVersion = 0
        }).IsValid());
        Assert.False((Event(FlHookEventKind.CharacterConnected) with
        {
            CharacterId = 123,
            GatewayLeaseVersion = 10,
            GatewaySessionId = null
        }).IsValid());
        Assert.False((Event(FlHookEventKind.PlayerConnected) with { OccurredUtc = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(1)) }).IsValid());
        Assert.False(new FlHookEventReplayRequestV1 { CorrelationId = Guid.NewGuid(), InstanceId = "li-01", MaximumEvents = 501 }.IsValid());
        Assert.False(new FlHookEventReplayRequestV1 { CorrelationId = Guid.NewGuid(), InstanceId = "li-01", AfterSequence = -1 }.IsValid());
        Assert.False(new FlHookEventReplayRequestV1 { CorrelationId = Guid.NewGuid(), InstanceId = "li-01", ProducerEpoch = Guid.Empty }.IsValid());
    }

    [Fact]
    public void ReplayResultRequiresOrderedEventsFromTheDeclaredInstance()
    {
        var result = new FlHookEventReplayResultV1
        {
            CorrelationId = Guid.NewGuid(),
            InstanceId = "li-01",
            OldestAvailableSequence = 4,
            LatestSequence = 5,
            RequestedSequence = 2,
            Gap = true,
            ProducerEpoch = Event(FlHookEventKind.PlayerConnected).ProducerEpoch,
            Events = [Event(FlHookEventKind.PlayerConnected) with { Sequence = 4, InstanceId = "li-01" }]
        };

        Assert.True(result.IsValid());
        Assert.False((result with { Events = [result.Events[0] with { InstanceId = "li-02" }] }).IsValid());
        Assert.False((result with { Events = [result.Events[0], result.Events[0]] }).IsValid());
        Assert.False((result with { Events = [result.Events[0] with { Sequence = 3 }] }).IsValid());
        Assert.False((result with { RequestedSequence = 3 }).IsValid());
        Assert.True(new FlHookEventReplayResultV1
        {
            CorrelationId = Guid.NewGuid(),
            InstanceId = "li-01",
            Events = []
        }.IsValid());
    }

    private static FlHookEventV1 Event(FlHookEventKind kind) => new()
    {
        InstanceId = "li-01",
        Sequence = 1,
        OccurredUtc = DateTimeOffset.UnixEpoch,
        Kind = kind,
        PlayerId = "player-01",
        ProducerEpoch = Guid.Parse("05b7f60d-01ba-4d16-99e2-43e373f99ad6"),
        GatewaySessionId = Guid.Parse("91fe0ff1-9853-4f98-9fbe-154878528aa2")
    };

}

[MessagePackObject]
public sealed record LegacyFlHookEventV1
{
    [Key(0)] public int SchemaVersion { get; init; }
    [Key(1)] public string? InstanceId { get; init; }
    [Key(2)] public long Sequence { get; init; }
    [Key(3)] public DateTimeOffset OccurredUtc { get; init; }
    [Key(4)] public FlHookEventKind Kind { get; init; }
    [Key(5)] public string? PlayerId { get; init; }
    [Key(6)] public string? SystemId { get; init; }
}

[MessagePackObject]
public sealed record PreviousFlHookEventV1
{
    [Key(0)] public int SchemaVersion { get; init; }
    [Key(1)] public string? InstanceId { get; init; }
    [Key(2)] public long Sequence { get; init; }
    [Key(3)] public DateTimeOffset OccurredUtc { get; init; }
    [Key(4)] public FlHookEventKind Kind { get; init; }
    [Key(5)] public string? PlayerId { get; init; }
    [Key(6)] public string? SystemId { get; init; }
    [Key(7)] public long? CharacterId { get; init; }
    [Key(8)] public Guid ProducerEpoch { get; init; }
}
