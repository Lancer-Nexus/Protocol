using MessagePack;

namespace LancerNexus.Protocol;

public enum EventServerReservationStatus : byte
{
    Reserved = 1,
    AlreadyReserved = 2,
    NoCapacity = 3,
    Conflict = 4,
    Invalid = 5,
    Unavailable = 6
}

public enum EventServerReservationReleaseStatus : byte
{
    Released = 1,
    AlreadyReleased = 2,
    NotFound = 3,
    Conflict = 4,
    Invalid = 5,
    Unavailable = 6
}

/// <summary>Idempotent request to reserve one ready instance for an event's scheduled capacity.</summary>
[MessagePackObject]
public sealed record EventServerReservationRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid RequestId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public string EventId { get; init; } = "";
    [Key(4)] public long EventRevision { get; init; }
    [Key(5)] public string TargetSystemId { get; init; } = "";
    [Key(6)] public int MaximumParticipants { get; init; }
    [Key(7)] public DateTimeOffset StartsUtc { get; init; }
    [Key(8)] public DateTimeOffset EndsUtc { get; init; }
    [Key(9)] public string IdempotencyKey { get; init; } = "";

    public bool IsValid() => SchemaVersion == 1 && RequestId != Guid.Empty && CorrelationId != Guid.Empty &&
        AdminQuery.ValidIdentifier(EventId) && EventRevision > 0 && AdminQuery.ValidIdentifier(TargetSystemId) &&
        MaximumParticipants is >= 1 and <= 200 && StartsUtc.Offset == TimeSpan.Zero &&
        EndsUtc.Offset == TimeSpan.Zero && EndsUtc > StartsUtc && IsValidIdempotencyKey(IdempotencyKey);

    private static bool IsValidIdempotencyKey(string value) => value is { Length: > 0 and <= 96 } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

[MessagePackObject]
public sealed record EventServerReservationDecisionV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid RequestId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public string EventId { get; init; } = "";
    [Key(4)] public Guid? ReservationId { get; init; }
    [Key(5)] public EventServerReservationStatus Status { get; init; }
    [Key(6)] public string? InstanceId { get; init; }
    [Key(7)] public string? SystemId { get; init; }
    [Key(8)] public string? Endpoint { get; init; }
    [Key(9)] public int? ReservedCapacity { get; init; }
    [Key(10)] public string? ErrorCode { get; init; }

    public bool IsValid() => SchemaVersion == 1 && RequestId != Guid.Empty && CorrelationId != Guid.Empty &&
        AdminQuery.ValidIdentifier(EventId) && Enum.IsDefined(Status) &&
        (Status is EventServerReservationStatus.Reserved or EventServerReservationStatus.AlreadyReserved
            ? ReservationId is { } id && id != Guid.Empty && AdminQuery.ValidIdentifier(InstanceId) &&
              AdminQuery.ValidIdentifier(SystemId) && Endpoint is { Length: > 0 and <= 512 } endpoint &&
              !endpoint.Any(char.IsControl) && ReservedCapacity is >= 1 and <= 200 && ErrorCode is null
            : ReservationId is null && InstanceId is null && SystemId is null && Endpoint is null &&
              ReservedCapacity is null && ErrorCode is { Length: > 0 and <= 96 } &&
              AdminQuery.ValidIdentifier(ErrorCode));
}

/// <summary>Releases an event's reserved capacity after an authorized terminal lifecycle decision.</summary>
[MessagePackObject]
public sealed record EventServerReservationReleaseRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid RequestId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public string EventId { get; init; } = "";
    [Key(4)] public Guid ReservationId { get; init; }
    [Key(5)] public string IdempotencyKey { get; init; } = "";
    [Key(6)] public string ReasonCode { get; init; } = "";

    public bool IsValid() => SchemaVersion == 1 && RequestId != Guid.Empty && CorrelationId != Guid.Empty &&
        AdminQuery.ValidIdentifier(EventId) && ReservationId != Guid.Empty &&
        IsValidIdempotencyKey(IdempotencyKey) && AdminQuery.ValidIdentifier(ReasonCode);

    private static bool IsValidIdempotencyKey(string value) => value is { Length: > 0 and <= 96 } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

[MessagePackObject]
public sealed record EventServerReservationReleaseDecisionV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid RequestId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public string EventId { get; init; } = "";
    [Key(4)] public Guid? ReservationId { get; init; }
    [Key(5)] public EventServerReservationReleaseStatus Status { get; init; }
    [Key(6)] public string? ErrorCode { get; init; }

    public bool IsValid() => SchemaVersion == 1 && RequestId != Guid.Empty && CorrelationId != Guid.Empty &&
        AdminQuery.ValidIdentifier(EventId) && Enum.IsDefined(Status) &&
        (Status is EventServerReservationReleaseStatus.Released or
            EventServerReservationReleaseStatus.AlreadyReleased
            ? ReservationId is { } id && id != Guid.Empty && ErrorCode is null
            : ReservationId is null && ErrorCode is { Length: > 0 and <= 96 } &&
              AdminQuery.ValidIdentifier(ErrorCode));
}
