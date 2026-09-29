using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>One bounded source-to-target runtime snapshot message carried over private mTLS QUIC.</summary>
[MessagePackObject]
public sealed class NpcPeerSnapshotTransfer
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public string SourceInstanceId { get; init; } = "";
    [Key(2)] public string TargetInstanceId { get; init; } = "";
    [Key(3)] public byte[] SnapshotMessagePack { get; init; } = [];
}

[MessagePackObject]
public sealed class NpcPeerSnapshotTransferAck
{
    [Key(0)] public Guid TransferId { get; init; }
    [Key(1)] public bool Accepted { get; init; }
    [Key(2)] public string ReasonCode { get; init; } = "";
}

public static class NpcPeerSnapshotTransferValidator
{
    public const int MaximumSnapshotBytes = 15 * 1024 * 1024;

    public static void Validate(NpcPeerSnapshotTransfer request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TransferId == Guid.Empty || string.IsNullOrWhiteSpace(request.SourceInstanceId) ||
            request.SourceInstanceId.Length > 96 || string.IsNullOrWhiteSpace(request.TargetInstanceId) ||
            request.TargetInstanceId.Length > 96 || request.SourceInstanceId == request.TargetInstanceId ||
            request.SnapshotMessagePack is null || request.SnapshotMessagePack.Length is 0 or > MaximumSnapshotBytes)
            throw new ProtocolViolationException("NPC peer snapshot transfer is invalid or exceeds its size limit.");
    }
}
