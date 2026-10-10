using MessagePack;

namespace LancerNexus.Protocol;

public enum NexusEventPhase : byte
{
    Planned = 1,
    Registration = 2,
    Provisioning = 3,
    Ready = 4,
    Locked = 5,
    Running = 6,
    ResultExport = 7,
    Closed = 8,
    Cancelled = 9,
    Failed = 10
}

public enum EventRegistrationStatus : byte
{
    Accepted = 1,
    AlreadyRegistered = 2,
    Denied = 3,
    EventNotFound = 4,
    RegistrationClosed = 5,
    CapacityFull = 6,
    Conflict = 7,
    Unavailable = 8,
    Invalid = 9
}

public enum EventLifecycleStatus : byte
{
    Applied = 1,
    Conflict = 2,
    Denied = 3,
    EventNotFound = 4,
    InvalidTransition = 5,
    Unavailable = 6,
    Invalid = 7
}

public enum EventResultStatus : byte
{
    Accepted = 1,
    AlreadyAccepted = 2,
    Denied = 3,
    EventNotFound = 4,
    Conflict = 5,
    Invalid = 6,
    Unavailable = 7
}

/// <summary>Wire representation of a single-system, capacity-bounded scheduled event.</summary>
[MessagePackObject]
public sealed record NexusEventDefinitionV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public string EventId { get; init; } = "";
    [Key(2)] public long Revision { get; init; }
    [Key(3)] public NexusEventPhase Phase { get; init; }
    [Key(4)] public DateTimeOffset RegistrationOpensUtc { get; init; }
    [Key(5)] public DateTimeOffset RegistrationClosesUtc { get; init; }
    [Key(6)] public DateTimeOffset StartsUtc { get; init; }
    [Key(7)] public DateTimeOffset EndsUtc { get; init; }
    [Key(8)] public int MaximumParticipants { get; init; }
    [Key(9)] public string TargetSystemId { get; init; } = "";
    [Key(10)] public string RulesetId { get; init; } = "";

    public bool IsValid() => SchemaVersion == 1 && AdminQuery.ValidIdentifier(EventId) && Revision > 0 &&
        Enum.IsDefined(Phase) && IsUtc(RegistrationOpensUtc) && IsUtc(RegistrationClosesUtc) &&
        IsUtc(StartsUtc) && IsUtc(EndsUtc) && RegistrationClosesUtc > RegistrationOpensUtc &&
        StartsUtc >= RegistrationClosesUtc && EndsUtc > StartsUtc &&
        MaximumParticipants is >= 1 and <= 200 && AdminQuery.ValidIdentifier(TargetSystemId) &&
        AdminQuery.ValidIdentifier(RulesetId);

    private static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;
}

/// <summary>
/// Gateway-attested player registration request. The Events service still checks the
/// current account session, character lease, event phase, eligibility and capacity.
/// </summary>
[MessagePackObject]
public sealed record EventRegistrationRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid RequestId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public Guid AccountId { get; init; }
    [Key(4)] public Guid SessionId { get; init; }
    [Key(5)] public long CharacterId { get; init; }
    [Key(6)] public long LeaseVersion { get; init; }
    [Key(7)] public string EventId { get; init; } = "";
    [Key(8)] public string IdempotencyKey { get; init; } = "";

    public bool IsValid() => SchemaVersion == 1 && RequestId != Guid.Empty && CorrelationId != Guid.Empty &&
        AccountId != Guid.Empty && SessionId != Guid.Empty && CharacterId > 0 && LeaseVersion > 0 &&
        AdminQuery.ValidIdentifier(EventId) && IsValidIdempotencyKey(IdempotencyKey);

    private static bool IsValidIdempotencyKey(string value) => value is { Length: > 0 and <= 96 } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

[MessagePackObject]
public sealed record EventRegistrationDecisionV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid RequestId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public EventRegistrationStatus Status { get; init; }
    [Key(4)] public Guid? RegistrationId { get; init; }
    [Key(5)] public long? EventRevision { get; init; }
    [Key(6)] public string? ErrorCode { get; init; }

    public bool IsValid() => SchemaVersion == 1 && RequestId != Guid.Empty && CorrelationId != Guid.Empty &&
        Enum.IsDefined(Status) &&
        (Status is EventRegistrationStatus.Accepted or EventRegistrationStatus.AlreadyRegistered
            ? RegistrationId is { } id && id != Guid.Empty && EventRevision is > 0 && ErrorCode is null
            : RegistrationId is null && EventRevision is null && ErrorCode is { Length: > 0 and <= 96 } &&
              AdminQuery.ValidIdentifier(ErrorCode));
}

/// <summary>
/// Optimistic, idempotent control-plane transition. Authorization and the durable audit
/// record are enforced by the Events service, not by this structural contract. Exactly
/// one actor is recorded: an account for Gateway-attested administration or a service
/// name for an authenticated service-to-service transition.
/// </summary>
[MessagePackObject]
public sealed record EventLifecycleTransitionRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid RequestId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public Guid? ActorAccountId { get; init; }
    [Key(4)] public string EventId { get; init; } = "";
    [Key(5)] public long ExpectedRevision { get; init; }
    [Key(6)] public NexusEventPhase ExpectedPhase { get; init; }
    [Key(7)] public NexusEventPhase NextPhase { get; init; }
    [Key(8)] public string IdempotencyKey { get; init; } = "";
    [Key(9)] public string? ReasonCode { get; init; }
    [Key(10)] public string? ActorService { get; init; }

    public bool IsValid() => SchemaVersion == 1 && RequestId != Guid.Empty && CorrelationId != Guid.Empty &&
        ((ActorAccountId is { } accountId && accountId != Guid.Empty && ActorService is null) ||
         (ActorAccountId is null && AdminQuery.ValidIdentifier(ActorService))) &&
        AdminQuery.ValidIdentifier(EventId) && ExpectedRevision > 0 &&
        Enum.IsDefined(ExpectedPhase) && Enum.IsDefined(NextPhase) &&
        EventLifecycleRules.CanTransition(ExpectedPhase, NextPhase) && IsValidIdempotencyKey(IdempotencyKey) &&
        (ReasonCode is null || AdminQuery.ValidIdentifier(ReasonCode)) &&
        (NextPhase is NexusEventPhase.Cancelled or NexusEventPhase.Failed
            ? ReasonCode is not null : ReasonCode is null);

    private static bool IsValidIdempotencyKey(string value) => value is { Length: > 0 and <= 96 } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

[MessagePackObject]
public sealed record EventLifecycleTransitionDecisionV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid RequestId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public EventLifecycleStatus Status { get; init; }
    [Key(4)] public NexusEventPhase? CurrentPhase { get; init; }
    [Key(5)] public long? CurrentRevision { get; init; }
    [Key(6)] public string? ErrorCode { get; init; }

    public bool IsValid() => SchemaVersion == 1 && RequestId != Guid.Empty && CorrelationId != Guid.Empty &&
        Enum.IsDefined(Status) &&
        (Status == EventLifecycleStatus.Applied
            ? CurrentPhase is { } phase && Enum.IsDefined(phase) && CurrentRevision is > 0 && ErrorCode is null
            : (CurrentPhase is null || Enum.IsDefined(CurrentPhase.Value)) &&
              (CurrentRevision is null || CurrentRevision > 0) && ErrorCode is { Length: > 0 and <= 96 } &&
              AdminQuery.ValidIdentifier(ErrorCode));
}

[MessagePackObject]
public sealed record EventParticipantResultV1
{
    [Key(0)] public Guid AccountId { get; init; }
    [Key(1)] public long CharacterId { get; init; }
    [Key(2)] public int Placement { get; init; }
    [Key(3)] public long EventPoints { get; init; }
    [Key(4)] public int Eliminations { get; init; }
    [Key(5)] public int Deaths { get; init; }
    [Key(6)] public int ObjectivesCompleted { get; init; }

    public bool IsValid() => AccountId != Guid.Empty && CharacterId > 0 && Placement is >= 1 and <= 200 &&
        EventPoints >= 0 && Eliminations >= 0 && Deaths >= 0 && ObjectivesCompleted >= 0;
}

/// <summary>
/// Frozen result export from the event's authenticated authoritative instance.
/// Event points are isolated from normal credits, inventory and character persistence.
/// </summary>
[MessagePackObject]
public sealed record EventResultExportRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid RequestId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public string EventId { get; init; } = "";
    [Key(4)] public long EventRevision { get; init; }
    [Key(5)] public string InstanceId { get; init; } = "";
    [Key(6)] public DateTimeOffset FrozenAtUtc { get; init; }
    [Key(7)] public EventParticipantResultV1[] Participants { get; init; } = [];

    public bool IsValid() => SchemaVersion == 1 && RequestId != Guid.Empty && CorrelationId != Guid.Empty &&
        AdminQuery.ValidIdentifier(EventId) && EventRevision > 0 && AdminQuery.ValidIdentifier(InstanceId) &&
        FrozenAtUtc.Offset == TimeSpan.Zero && Participants is { Length: > 0 and <= 200 } &&
        Participants.All(participant => participant is not null && participant.IsValid()) &&
        Participants.Select(participant => participant.AccountId).Distinct().Count() == Participants.Length &&
        Participants.Select(participant => participant.CharacterId).Distinct().Count() == Participants.Length;
}

[MessagePackObject]
public sealed record EventResultExportDecisionV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid RequestId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public Guid? ResultId { get; init; }
    [Key(4)] public EventResultStatus Status { get; init; }
    [Key(5)] public string? ErrorCode { get; init; }

    public bool IsValid() => SchemaVersion == 1 && RequestId != Guid.Empty && CorrelationId != Guid.Empty &&
        Enum.IsDefined(Status) &&
        (Status is EventResultStatus.Accepted or EventResultStatus.AlreadyAccepted
            ? ResultId is { } resultId && resultId != Guid.Empty && ErrorCode is null
            : ResultId is null && ErrorCode is { Length: > 0 and <= 96 } && AdminQuery.ValidIdentifier(ErrorCode));
}

public static class EventLifecycleRules
{
    public static bool CanTransition(NexusEventPhase current, NexusEventPhase next) => (current, next) switch
    {
        (NexusEventPhase.Planned, NexusEventPhase.Registration or NexusEventPhase.Cancelled) => true,
        (NexusEventPhase.Registration, NexusEventPhase.Provisioning or NexusEventPhase.Cancelled) => true,
        (NexusEventPhase.Provisioning, NexusEventPhase.Ready or NexusEventPhase.Failed or NexusEventPhase.Cancelled) => true,
        (NexusEventPhase.Ready, NexusEventPhase.Locked or NexusEventPhase.Failed or NexusEventPhase.Cancelled) => true,
        (NexusEventPhase.Locked, NexusEventPhase.Running or NexusEventPhase.Failed or NexusEventPhase.Cancelled) => true,
        (NexusEventPhase.Running, NexusEventPhase.ResultExport or NexusEventPhase.Failed) => true,
        (NexusEventPhase.ResultExport, NexusEventPhase.Closed or NexusEventPhase.Failed) => true,
        _ => false
    };
}
