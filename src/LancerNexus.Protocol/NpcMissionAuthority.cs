using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>Private Coordinator-to-Gateway decision request. TransferId is also the mission runtime ID.</summary>
[MessagePackObject]
public sealed record NpcMissionAuthorityRequestV1
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public string SourceInstanceId { get; init; } = "";
    [Key(2)] public string TargetInstanceId { get; init; } = "";
    [Key(3)] public string TargetSystemId { get; init; } = "";
    [Key(4)] public NpcTransferState Decision { get; init; }
    [Key(5)] public ushort SchemaVersion { get; init; } = 1;

    public bool IsValid() => SchemaVersion == 1 && TransferId != Guid.Empty &&
        ValidName(SourceInstanceId) && ValidName(TargetInstanceId) && ValidName(TargetSystemId) &&
        SourceInstanceId != TargetInstanceId && Decision is NpcTransferState.Committed or NpcTransferState.Aborted;

    private static bool ValidName(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 96;
}

[MessagePackObject]
public sealed record NpcMissionAuthorityResultV1
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public bool Accepted { get; init; }
    [Key(2)] public string ReasonCode { get; init; } = "";
    [Key(3)] public string SourceInstanceId { get; init; } = "";
    [Key(4)] public string TargetInstanceId { get; init; } = "";
    [Key(5)] public string TargetSystemId { get; init; } = "";
    [Key(6)] public NpcTransferState Decision { get; init; }
    [Key(7)] public long? CommittedLeaseVersion { get; init; }
    [Key(8)] public ushort SchemaVersion { get; init; } = 1;

    public bool Authorizes(NpcMissionAuthorityRequestV1 request) => request.IsValid() && SchemaVersion == 1 &&
        Accepted && TransferId == request.TransferId && SourceInstanceId == request.SourceInstanceId &&
        TargetInstanceId == request.TargetInstanceId &&
        string.Equals(TargetSystemId, request.TargetSystemId, StringComparison.OrdinalIgnoreCase) &&
        Decision == request.Decision && (Decision == NpcTransferState.Committed
            ? CommittedLeaseVersion is > 0 : CommittedLeaseVersion is null);
}
