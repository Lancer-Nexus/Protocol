using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>
/// Binds the coordinator's quote reservation to the exact fenced SaveGame snapshot
/// that Gateway must persist before it can report a successful market trade.
/// </summary>
[MessagePackObject]
public sealed record MarketTradeCommitRequestV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public MarketTradeQuoteReservationRequestV1 Reservation { get; init; } = new();
    [Key(3)] public Guid TradeId { get; init; }
    [Key(4)] public CharacterSaveSnapshotWriteRequestV1 Snapshot { get; init; } = new();

    public bool IsValid() => SchemaVersion == 1 && CorrelationId != Guid.Empty &&
        TradeId != Guid.Empty && Reservation is not null && Reservation.IsValid() &&
        Snapshot is not null && Snapshot.IsValid() && Reservation.TradeId == TradeId &&
        Reservation.CharacterId == Snapshot.CharacterId;
}

public enum MarketTradeCommitStatusV1 : byte
{
    Pending = 1,
    Committed = 2,
    Duplicate = 3,
    ReservationRejected = 4,
    SnapshotConflict = 5,
    LeaseRejected = 6,
    SessionRejected = 7,
    NotFound = 8,
    Conflict = 9,
    Unavailable = 10
}

/// <summary>Gateway's durable result for one quote-bound market trade.</summary>
[MessagePackObject]
public sealed record MarketTradeCommitResponseV1
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid CorrelationId { get; init; }
    [Key(2)] public Guid TradeId { get; init; }
    [Key(3)] public MarketTradeCommitStatusV1 Status { get; init; }
    [Key(4)] public long? SnapshotRevision { get; init; }

    public bool IsValidFor(MarketTradeCommitRequestV1 request)
    {
        if (request is null || !request.IsValid() || SchemaVersion != 1 ||
            CorrelationId != request.CorrelationId || TradeId != request.Reservation.TradeId ||
            !Enum.IsDefined(Status))
            return false;
        return Status switch
        {
            MarketTradeCommitStatusV1.Committed or MarketTradeCommitStatusV1.Duplicate =>
                SnapshotRevision == request.Snapshot.Revision,
            _ => SnapshotRevision is null
        };
    }
}
