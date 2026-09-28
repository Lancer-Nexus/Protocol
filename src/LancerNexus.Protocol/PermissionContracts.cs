using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>Invalidates an instance's local permission snapshot; payloads never carry authoritative grants.</summary>
[MessagePackObject]
public sealed class PermissionRevisionChanged
{
    [Key(0)] public Guid EventId { get; init; }
    [Key(1)] public long Revision { get; init; }
    [Key(2)] public DateTimeOffset CreatedUtc { get; init; }
}

/// <summary>Sent only after the instance atomically loaded and activated the indicated SQL revision.</summary>
[MessagePackObject]
public sealed class PermissionRevisionAcknowledged
{
    [Key(0)] public string InstanceId { get; init; } = "";
    [Key(1)] public long Revision { get; init; }
    [Key(2)] public DateTimeOffset AppliedUtc { get; init; }
}
