using MessagePack;

namespace LancerNexus.Protocol;

public enum AdminActionKind { Message = 1, Kick = 2, Ban = 3, Unban = 4, Drain = 5, Move = 6, Teleport = 7, Transfer = 8 }
public enum AdminActionTargetKind { Player = 1, Account = 2, Instance = 3, System = 4, All = 5, Ban = 6 }
public enum AdminActionStatus { Pending = 0, Completed = 1, Denied = 2, Invalid = 3, NotFound = 4, Conflict = 5, Unavailable = 6 }

/// <summary>
/// Versioned internal mutation command. Its actor identity must be attested by Gateway,
/// and each receiving service must still authorize, scope-check and deduplicate it.
/// </summary>
[MessagePackObject]
public sealed record AdminActionRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public Guid ActorAccountId { get; init; }
    [Key(4)] public string Source { get; init; } = "";
    [Key(5)] public string IdempotencyKey { get; init; } = "";
    [Key(6)] public AdminActionKind Kind { get; init; }
    [Key(7)] public AdminActionTargetKind TargetKind { get; init; }
    [Key(8)] public string? TargetId { get; init; }
    [Key(9)] public string? TargetInstanceId { get; init; }
    [Key(10)] public string? TargetSystemId { get; init; }
    [Key(11)] public string? TargetBaseId { get; init; }
    [Key(12)] public string? Message { get; init; }
    [Key(13)] public string? Reason { get; init; }
    [Key(14)] public DateTimeOffset? ExpiresUtc { get; init; }
    [Key(15)] public Guid? ConfirmationId { get; init; }

    public bool IsValid()
    {
        if (SchemaVersion != 1 || CommandId == Guid.Empty || CorrelationId == Guid.Empty ||
            ActorAccountId == Guid.Empty || Source is not { Length: > 0 and <= 128 } ||
            IdempotencyKey is not { Length: > 0 and <= 96 } || !IsValidSource(Source) || !IsValidIdempotencyKey(IdempotencyKey) ||
            !Enum.IsDefined(Kind) || !Enum.IsDefined(TargetKind) ||
            (TargetId is not null && !AdminQuery.ValidIdentifier(TargetId)) ||
            (TargetInstanceId is not null && !AdminQuery.ValidIdentifier(TargetInstanceId)) ||
            (TargetSystemId is not null && !AdminQuery.ValidIdentifier(TargetSystemId)) ||
            (TargetBaseId is not null && !AdminQuery.ValidIdentifier(TargetBaseId)) ||
            !IsBoundedText(Reason, 512) || !IsBoundedText(Message, 512) ||
            (ConfirmationId.HasValue && ConfirmationId.Value == Guid.Empty))
            return false;

        return Kind switch
        {
            AdminActionKind.Message => IsMessageValid(),
            AdminActionKind.Kick => TargetIs(AdminActionTargetKind.Player) && HasTarget() &&
                                    NoRouteOrMessage() && ExpiresUtc is null && ConfirmationId is null,
            AdminActionKind.Ban => (TargetIs(AdminActionTargetKind.Player) || TargetIs(AdminActionTargetKind.Account)) &&
                                   HasTarget() && RequiredReason() && NoRouteOrMessage() &&
                                   IsUtc(ExpiresUtc) && (ExpiresUtc is not null || ConfirmationId is not null),
            AdminActionKind.Unban => (TargetIs(AdminActionTargetKind.Account) || TargetIs(AdminActionTargetKind.Ban)) &&
                                     HasTarget() && NoRouteOrMessage() && Reason is null && ExpiresUtc is null && ConfirmationId is null,
            AdminActionKind.Drain => TargetIs(AdminActionTargetKind.Instance) && HasTarget() && RequiredReason() &&
                                     NoRouteOrMessage() && ExpiresUtc is null && ConfirmationId is not null,
            AdminActionKind.Move or AdminActionKind.Transfer => TargetIs(AdminActionTargetKind.Player) && HasTarget() &&
                                     AdminQuery.ValidIdentifier(TargetInstanceId) && TargetSystemId is null && TargetBaseId is null &&
                                     Message is null && ExpiresUtc is null && ConfirmationId is null,
            AdminActionKind.Teleport => TargetIs(AdminActionTargetKind.Player) && HasTarget() && TargetInstanceId is null &&
                                     AdminQuery.ValidIdentifier(TargetSystemId) && Message is null && ExpiresUtc is null && ConfirmationId is null,
            _ => false
        };
    }

    private bool IsMessageValid()
    {
        var targetValid = TargetKind == AdminActionTargetKind.All
            ? TargetId is null
            : (TargetKind is AdminActionTargetKind.Player or AdminActionTargetKind.Instance or AdminActionTargetKind.System) && HasTarget();
        return targetValid && TargetInstanceId is null && TargetSystemId is null && TargetBaseId is null &&
               Message is { Length: > 0 } && !string.IsNullOrWhiteSpace(Message) && Reason is null &&
               ExpiresUtc is null && ConfirmationId is null;
    }

    private bool TargetIs(AdminActionTargetKind kind) => TargetKind == kind;
    private bool HasTarget() => TargetId is { Length: > 0 };
    private bool RequiredReason() => Reason is { Length: > 0 } && !string.IsNullOrWhiteSpace(Reason);
    private bool NoRouteOrMessage() => TargetInstanceId is null && TargetSystemId is null && TargetBaseId is null && Message is null;
    private static bool IsUtc(DateTimeOffset? expiresUtc) => expiresUtc is null || expiresUtc.Value.Offset == TimeSpan.Zero;

    private static bool IsValidSource(string source) => source switch
    {
        "rest" or "cli" or "automation" => true,
        _ when source.StartsWith("ingame:", StringComparison.Ordinal) =>
            AdminQuery.ValidIdentifier(source[7..]),
        _ => false
    };

    private static bool IsValidIdempotencyKey(string value) => value is { Length: > 0 and <= 96 } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    private static bool IsBoundedText(string? value, int limit) => value is null ||
        value.Length <= limit && value.All(c => !char.IsControl(c));
}

[MessagePackObject]
public sealed class AdminActionResultV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CommandId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public AdminActionStatus Status { get; init; }
    [Key(4)] public string? ErrorCode { get; init; }
    [Key(5)] public string? TargetInstanceId { get; init; }
    [Key(6)] public Guid? OperationId { get; init; }
    [Key(7)] public long? Sequence { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CommandId != Guid.Empty && CorrelationId != Guid.Empty &&
        Enum.IsDefined(Status) &&
        (ErrorCode is null || AdminQuery.ValidIdentifier(ErrorCode)) &&
        (TargetInstanceId is null || AdminQuery.ValidIdentifier(TargetInstanceId)) &&
        (OperationId is null || OperationId != Guid.Empty) && Sequence is null or >= 0 &&
        (Status is AdminActionStatus.Pending or AdminActionStatus.Completed
            ? ErrorCode is null : ErrorCode is not null);
}
