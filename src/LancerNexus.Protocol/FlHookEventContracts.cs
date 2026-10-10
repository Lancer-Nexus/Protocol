using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>Closed set of lifecycle events that a game hook may publish.</summary>
public enum FlHookEventKind { PlayerConnected = 1, PlayerDisconnected = 2, PlayerSystemChanged = 3 }

/// <summary>
/// Versioned event emitted by one game instance. Sequence is strictly increasing per instance;
/// consumers must detect gaps and request replay before assuming a complete history.
/// </summary>
[MessagePackObject]
public sealed record FlHookEventV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public string InstanceId { get; init; } = "";
    [Key(2)] public long Sequence { get; init; }
    [Key(3)] public DateTimeOffset OccurredUtc { get; init; }
    [Key(4)] public FlHookEventKind Kind { get; init; }
    [Key(5)] public string PlayerId { get; init; } = "";
    [Key(6)] public string? SystemId { get; init; }

    public bool IsValid() => SchemaVersion == 1 && AdminQuery.ValidIdentifier(InstanceId) &&
        Sequence > 0 && OccurredUtc.Offset == TimeSpan.Zero && Enum.IsDefined(Kind) &&
        AdminQuery.ValidIdentifier(PlayerId) &&
        (Kind == FlHookEventKind.PlayerSystemChanged
            ? AdminQuery.ValidIdentifier(SystemId)
            : SystemId is null);
}

/// <summary>Requests a bounded replay strictly after the supplied per-instance sequence.</summary>
[MessagePackObject]
public sealed record FlHookEventReplayRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public string InstanceId { get; init; } = "";
    [Key(3)] public long AfterSequence { get; init; }
    [Key(4)] public int MaximumEvents { get; init; } = 100;

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        AdminQuery.ValidIdentifier(InstanceId) && AfterSequence >= 0 && MaximumEvents is >= 1 and <= 500;
}

/// <summary>
/// Replay result. Gap is true when requested history has already expired; callers must
/// reconcile against a fresh authoritative snapshot before consuming these events.
/// </summary>
[MessagePackObject]
public sealed record FlHookEventReplayResultV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public string InstanceId { get; init; } = "";
    [Key(3)] public long OldestAvailableSequence { get; init; }
    [Key(4)] public long LatestSequence { get; init; }
    [Key(5)] public bool Gap { get; init; }
    [Key(6)] public FlHookEventV1[] Events { get; init; } = [];
    [Key(7)] public long RequestedSequence { get; init; }

    public bool IsValid()
    {
        if (SchemaVersion != 1 || CorrelationId == Guid.Empty || !AdminQuery.ValidIdentifier(InstanceId) ||
            OldestAvailableSequence < 0 || LatestSequence < 0 || OldestAvailableSequence > LatestSequence ||
            RequestedSequence < 0 || RequestedSequence > LatestSequence || Events is null || Events.Length > 500 ||
            Events.Any(x => x is null || !x.IsValid() || x.InstanceId != InstanceId) ||
            Gap != (RequestedSequence < OldestAvailableSequence - 1))
            return false;

        for (var i = 0; i < Events.Length; i++)
        {
            if (Events[i].Sequence <= RequestedSequence || Events[i].Sequence < OldestAvailableSequence ||
                Events[i].Sequence > LatestSequence ||
                (i > 0 && Events[i].Sequence <= Events[i - 1].Sequence)) return false;
        }
        return !Gap || Events.Length == 0 || Events[0].Sequence == OldestAvailableSequence;
    }
}
