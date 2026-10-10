using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class EventContractsTests
{
    [Fact]
    public void EventDefinitionRoundTripsAndKeepsExplicitFieldCount()
    {
        var definition = Definition();

        Assert.True(definition.IsValid());
        var bytes = MessagePackSerializer.Serialize(definition);
        Assert.Equal(11, new MessagePackReader(bytes).ReadArrayHeader());
        Assert.Equal(definition, MessagePackSerializer.Deserialize<NexusEventDefinitionV1>(bytes));
    }

    [Theory]
    [InlineData(NexusEventPhase.Planned, NexusEventPhase.Registration, true)]
    [InlineData(NexusEventPhase.Registration, NexusEventPhase.Provisioning, true)]
    [InlineData(NexusEventPhase.Provisioning, NexusEventPhase.Ready, true)]
    [InlineData(NexusEventPhase.Ready, NexusEventPhase.Locked, true)]
    [InlineData(NexusEventPhase.Locked, NexusEventPhase.Running, true)]
    [InlineData(NexusEventPhase.Running, NexusEventPhase.ResultExport, true)]
    [InlineData(NexusEventPhase.ResultExport, NexusEventPhase.Closed, true)]
    [InlineData(NexusEventPhase.Registration, NexusEventPhase.Running, false)]
    [InlineData(NexusEventPhase.Running, NexusEventPhase.Cancelled, false)]
    [InlineData(NexusEventPhase.Closed, NexusEventPhase.Registration, false)]
    public void EnforcesEventPhaseTransitions(NexusEventPhase current, NexusEventPhase next, bool valid) =>
        Assert.Equal(valid, EventLifecycleRules.CanTransition(current, next));

    [Fact]
    public void RegistrationAndLifecycleRequestsRequireIdentityCorrelationAndIdempotency()
    {
        var registration = new EventRegistrationRequestV1
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            CharacterId = 7,
            LeaseVersion = 12,
            EventId = "battle-01",
            IdempotencyKey = "event-registration-01"
        };
        Assert.True(registration.IsValid());
        Assert.False((registration with { LeaseVersion = 0 }).IsValid());
        Assert.False((registration with { IdempotencyKey = "two words" }).IsValid());
        var registrationBytes = MessagePackSerializer.Serialize(registration);
        Assert.Equal(9, new MessagePackReader(registrationBytes).ReadArrayHeader());
        Assert.True(MessagePackSerializer.Deserialize<EventRegistrationRequestV1>(registrationBytes).IsValid());

        var transition = new EventLifecycleTransitionRequestV1
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            ActorAccountId = Guid.NewGuid(),
            EventId = "battle-01",
            ExpectedRevision = 3,
            ExpectedPhase = NexusEventPhase.Registration,
            NextPhase = NexusEventPhase.Provisioning,
            IdempotencyKey = "event-start-01"
        };
        Assert.True(transition.IsValid());
        Assert.Equal(11, new MessagePackReader(MessagePackSerializer.Serialize(transition)).ReadArrayHeader());
        Assert.False((transition with { NextPhase = NexusEventPhase.Running }).IsValid());
        Assert.False((transition with { NextPhase = NexusEventPhase.Cancelled }).IsValid());
        Assert.True((transition with { NextPhase = NexusEventPhase.Cancelled, ReasonCode = "operator_cancelled" }).IsValid());
        Assert.True((transition with { ActorAccountId = null, ActorService = "coordinator" }).IsValid());
        Assert.False((transition with { ActorAccountId = Guid.Empty, ActorService = "coordinator" }).IsValid());
    }

    [Theory]
    [InlineData(EventRegistrationStatus.Accepted, true)]
    [InlineData(EventRegistrationStatus.CapacityFull, false)]
    public void RegistrationDecisionsRequireConsistentSuccessPayload(EventRegistrationStatus status, bool succeeds)
    {
        var result = new EventRegistrationDecisionV1
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            Status = status,
            RegistrationId = succeeds ? Guid.NewGuid() : null,
            EventRevision = succeeds ? 5 : null,
            ErrorCode = succeeds ? null : "capacity_full"
        };

        Assert.True(result.IsValid());
        var malformed = succeeds ? result with { RegistrationId = null } : result with { ErrorCode = null };
        Assert.False(malformed.IsValid());
    }

    [Fact]
    public void ResultExportIsBoundedUniqueAndCannotContainCharacterEconomyChanges()
    {
        var export = new EventResultExportRequestV1
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            EventId = "battle-01",
            EventRevision = 8,
            InstanceId = "event-01",
            FrozenAtUtc = DateTimeOffset.Parse("2026-10-02T15:00:00Z"),
            Participants = [
                new EventParticipantResultV1
                {
                    AccountId = Guid.NewGuid(), CharacterId = 10, Placement = 1, EventPoints = 120,
                    Eliminations = 4, Deaths = 1, ObjectivesCompleted = 2
                },
                new EventParticipantResultV1
                {
                    AccountId = Guid.NewGuid(), CharacterId = 11, Placement = 2, EventPoints = 80,
                    Eliminations = 2, Deaths = 2, ObjectivesCompleted = 1
                }
            ]
        };

        Assert.True(export.IsValid());
        var bytes = MessagePackSerializer.Serialize(export);
        Assert.Equal(8, new MessagePackReader(bytes).ReadArrayHeader());
        var decoded = MessagePackSerializer.Deserialize<EventResultExportRequestV1>(bytes);
        Assert.True(decoded.IsValid());
        Assert.Equal(export.Participants, decoded.Participants);
        Assert.False((export with
        {
            Participants = [export.Participants[0], export.Participants[1] with
            {
                AccountId = export.Participants[0].AccountId
            }]
        }).IsValid());
        Assert.False((export with { Participants = [export.Participants[0] with { EventPoints = -1 }] }).IsValid());
    }

    [Fact]
    public void EventServerReservationAndReleaseAreExplicitAndIdempotentContracts()
    {
        var request = new EventServerReservationRequestV1
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            EventId = "battle-01",
            EventRevision = 3,
            TargetSystemId = "li01",
            MaximumParticipants = 64,
            StartsUtc = DateTimeOffset.Parse("2026-11-02T13:00:00Z"),
            EndsUtc = DateTimeOffset.Parse("2026-11-02T15:00:00Z"),
            IdempotencyKey = "battle-01-server"
        };
        Assert.True(request.IsValid());
        Assert.False((request with { MaximumParticipants = 201 }).IsValid());
        Assert.False((request with { EndsUtc = request.StartsUtc }).IsValid());
        Assert.Equal(10, new MessagePackReader(MessagePackSerializer.Serialize(request)).ReadArrayHeader());

        var reservation = new EventServerReservationDecisionV1
        {
            RequestId = request.RequestId,
            CorrelationId = request.CorrelationId,
            EventId = request.EventId,
            ReservationId = Guid.NewGuid(),
            Status = EventServerReservationStatus.Reserved,
            InstanceId = "li-event-01",
            SystemId = "li01",
            Endpoint = "quic://li-event-01:7443",
            ReservedCapacity = 64
        };
        Assert.True(reservation.IsValid());
        Assert.Equal(reservation,
            MessagePackSerializer.Deserialize<EventServerReservationDecisionV1>(
                MessagePackSerializer.Serialize(reservation)));
        Assert.False((reservation with { Endpoint = null }).IsValid());

        var release = new EventServerReservationReleaseRequestV1
        {
            RequestId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            EventId = request.EventId,
            ReservationId = reservation.ReservationId!.Value,
            IdempotencyKey = "battle-01-release",
            ReasonCode = "event_closed"
        };
        Assert.True(release.IsValid());
        Assert.Equal(7, new MessagePackReader(MessagePackSerializer.Serialize(release)).ReadArrayHeader());

        var released = new EventServerReservationReleaseDecisionV1
        {
            RequestId = release.RequestId,
            CorrelationId = release.CorrelationId,
            EventId = release.EventId,
            ReservationId = release.ReservationId,
            Status = EventServerReservationReleaseStatus.Released
        };
        Assert.True(released.IsValid());
        Assert.False((released with { ReservationId = null }).IsValid());
    }

    private static NexusEventDefinitionV1 Definition() => new()
    {
        EventId = "battle-01",
        Revision = 3,
        Phase = NexusEventPhase.Registration,
        RegistrationOpensUtc = DateTimeOffset.Parse("2026-10-01T12:00:00Z"),
        RegistrationClosesUtc = DateTimeOffset.Parse("2026-10-02T12:00:00Z"),
        StartsUtc = DateTimeOffset.Parse("2026-10-02T13:00:00Z"),
        EndsUtc = DateTimeOffset.Parse("2026-10-02T15:00:00Z"),
        MaximumParticipants = 64,
        TargetSystemId = "event01",
        RulesetId = "arena-v1"
    };
}
