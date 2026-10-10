using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>
/// Requests the initial character lease after a player selects a character.
/// The Gateway derives account, session and target instance from the already
/// admitted join ticket. The authenticated server supplies the selected character
/// and the Gateway checks it against ticket binding and SQL ownership.
/// Retries must reuse the same short-lived lease token so they cannot rotate a
/// lease whose successful response was lost.
/// </summary>
[MessagePackObject]
public sealed record CharacterLeaseAcquireRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public string JoinTicket { get; init; } = "";
    [Key(3)] public string LeaseToken { get; init; } = "";
    [Key(4)] public long CharacterId { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        JoinTicket is { Length: > 0 and <= 16_384 } && IsLeaseToken(LeaseToken) && CharacterId > 0;

    private static bool IsLeaseToken(string? value)
    {
        if (value is not { Length: 43 } || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))) return false;
        try { return Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + "=").Length == 32; }
        catch (FormatException) { return false; }
    }
}

/// <summary>Result of an initial lease acquisition or an idempotent retry.</summary>
[MessagePackObject]
public sealed record CharacterLeaseAcquireResultV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public bool Accepted { get; init; }
    [Key(3)] public string ReasonCode { get; init; } = "";
    [Key(4)] public long? LeaseVersion { get; init; }
    [Key(5)] public DateTimeOffset? ValidUntilUtc { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        AdminQuery.ValidIdentifier(ReasonCode) &&
        (Accepted
            ? LeaseVersion is > 0 && ValidUntilUtc is { Offset: var offset } && offset == TimeSpan.Zero
            : LeaseVersion is null && ValidUntilUtc is null);
}
