using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>Lists the active character identities for the authenticated Gateway session.</summary>
[MessagePackObject]
public sealed record CharacterRosterRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public Guid AccountId { get; init; }
    [Key(3)] public Guid SessionId { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        AccountId != Guid.Empty && SessionId != Guid.Empty;
}

[MessagePackObject]
public sealed record CharacterRosterEntryV1
{
    [Key(0)] public long CharacterId { get; init; }
    [Key(1)] public string DisplayName { get; init; } = "";

    public bool IsValid() => CharacterId > 0 && !string.IsNullOrWhiteSpace(DisplayName) &&
        DisplayName.Length <= 96 && DisplayName == DisplayName.Trim() && !DisplayName.Any(char.IsControl);
}

[MessagePackObject]
public sealed record CharacterRosterResultV1
{
    public const int MaxCharacters = 1000;

    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public bool Accepted { get; init; }
    [Key(3)] public string ReasonCode { get; init; } = "";
    [Key(4)] public CharacterRosterEntryV1[] Characters { get; init; } = [];

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        AdminQuery.ValidIdentifier(ReasonCode) && Characters is { Length: <= MaxCharacters } &&
        (Accepted
            ? ReasonCode == "listed" && Characters.All(character => character is not null && character.IsValid()) &&
              Characters.Select(character => character.CharacterId).Distinct().Count() == Characters.Length
            : ReasonCode == "session_rejected" && Characters.Length == 0);
}

/// <summary>Requests a history-preserving tombstone for a character owned by the active account.</summary>
[MessagePackObject]
public sealed record CharacterDeletionRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public Guid AccountId { get; init; }
    [Key(3)] public Guid SessionId { get; init; }
    [Key(4)] public long CharacterId { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        AccountId != Guid.Empty && SessionId != Guid.Empty && CharacterId > 0;
}

[MessagePackObject]
public sealed record CharacterDeletionResultV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public bool Accepted { get; init; }
    [Key(3)] public string ReasonCode { get; init; } = "";

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        AdminQuery.ValidIdentifier(ReasonCode) &&
        (Accepted ? ReasonCode is "deleted" or "duplicate" :
            ReasonCode is "session_rejected" or "character_not_found" or "character_in_use" or
                "transfer_in_progress" or "unavailable");
}

/// <summary>Stores the initial SaveGame before the new character can be selected on another instance.</summary>
[MessagePackObject]
public sealed record CharacterBootstrapRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public Guid AccountId { get; init; }
    [Key(3)] public Guid SessionId { get; init; }
    [Key(4)] public long CharacterId { get; init; }
    [Key(5)] public byte[] Payload { get; init; } = [];

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        AccountId != Guid.Empty && SessionId != Guid.Empty && CharacterId > 0 &&
        Payload is { Length: > 0 and <= CharacterSaveSnapshotWriteRequestV1.MaxPayloadBytes };
}

[MessagePackObject]
public sealed record CharacterBootstrapResultV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public bool Accepted { get; init; }
    [Key(3)] public string ReasonCode { get; init; } = "";

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        AdminQuery.ValidIdentifier(ReasonCode) &&
        (Accepted ? ReasonCode is "stored" or "duplicate" :
            ReasonCode is "session_rejected" or "character_not_found" or "character_in_use" or
                "snapshot_conflict" or "unavailable");
}

/// <summary>Reads an account-owned SaveGame preview for a cluster character menu before lease selection.</summary>
[MessagePackObject]
public sealed record CharacterSnapshotPreviewRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public Guid AccountId { get; init; }
    [Key(3)] public Guid SessionId { get; init; }
    [Key(4)] public long CharacterId { get; init; }

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        AccountId != Guid.Empty && SessionId != Guid.Empty && CharacterId > 0;
}
