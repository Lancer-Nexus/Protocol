using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>Stores the next versioned Freelancer SaveGame snapshot under an active character lease.</summary>
[MessagePackObject]
public sealed record CharacterSaveSnapshotWriteRequestV1
{
    public const int MaxPayloadBytes = 16 * 1024 * 1024;

    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public long CharacterId { get; init; }
    [Key(3)] public string LeaseToken { get; init; } = "";
    [Key(4)] public long LeaseVersion { get; init; }
    [Key(5)] public long Revision { get; init; }
    [Key(6)] public byte[] Payload { get; init; } = [];

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty && CharacterId > 0 &&
        IsLeaseToken(LeaseToken) && LeaseVersion > 0 && Revision > 0 &&
        Payload is { Length: > 0 and <= MaxPayloadBytes };

    internal static bool IsLeaseToken(string? value)
    {
        if (value is not { Length: 43 } || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))) return false;
        try { return Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + "=").Length == 32; }
        catch (FormatException) { return false; }
    }
}

/// <summary>Reads the latest SaveGame snapshot while presenting the current lease fence.</summary>
[MessagePackObject]
public sealed record CharacterSaveSnapshotReadRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public long CharacterId { get; init; }
    [Key(3)] public string LeaseToken { get; init; } = "";
    [Key(4)] public long LeaseVersion { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty && CharacterId > 0 &&
        CharacterSaveSnapshotWriteRequestV1.IsLeaseToken(LeaseToken) && LeaseVersion > 0;
}

/// <summary>The latest stored snapshot returned by Gateway after current-lease validation.</summary>
[MessagePackObject]
public sealed record CharacterSaveSnapshotV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public long CharacterId { get; init; }
    [Key(3)] public long LeaseVersion { get; init; }
    [Key(4)] public long Revision { get; init; }
    [Key(5)] public byte[] Payload { get; init; } = [];

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty && CharacterId > 0 &&
        LeaseVersion > 0 && Revision > 0 && Payload is { Length: > 0 and <= CharacterSaveSnapshotWriteRequestV1.MaxPayloadBytes };
}

/// <summary>Result of a fenced snapshot write or idempotent retry.</summary>
[MessagePackObject]
public sealed record CharacterSaveSnapshotWriteResultV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public bool Accepted { get; init; }
    [Key(3)] public string ReasonCode { get; init; } = "";
    [Key(4)] public long? Revision { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        AdminQuery.ValidIdentifier(ReasonCode) &&
        (Accepted
            ? ReasonCode is "stored" or "duplicate" && Revision is > 0
            : Revision is null);
}
