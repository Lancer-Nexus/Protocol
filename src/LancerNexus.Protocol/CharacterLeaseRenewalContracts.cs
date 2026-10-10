using MessagePack;

namespace LancerNexus.Protocol;

[MessagePackObject]
public sealed record CharacterLeaseRenewRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public long CharacterId { get; init; }
    [Key(3)] public string LeaseToken { get; init; } = "";
    [Key(4)] public long LeaseVersion { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty && CharacterId > 0 &&
        LeaseVersion > 0 && IsLeaseToken(LeaseToken);

    private static bool IsLeaseToken(string? value)
    {
        if (value is not { Length: 43 } || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))) return false;
        try { return Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + "=").Length == 32; }
        catch (FormatException) { return false; }
    }
}

[MessagePackObject]
public sealed record CharacterLeaseRenewResultV1
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
            ? ReasonCode == "renewed" && LeaseVersion is > 0 && ValidUntilUtc is { Offset: var offset } && offset == TimeSpan.Zero
            : LeaseVersion is null && ValidUntilUtc is null);
}
