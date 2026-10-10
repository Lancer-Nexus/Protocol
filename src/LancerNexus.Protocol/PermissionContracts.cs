using MessagePack;
namespace LancerNexus.Protocol;
[MessagePackObject]
public sealed class PermissionRevisionChanged
{
    [Key(0)] public Guid EventId { get; init; }
    [Key(1)] public long Revision { get; init; }
    [Key(2)] public DateTimeOffset CreatedUtc { get; init; }
}
[MessagePackObject]
public sealed class PermissionRevisionAcknowledged
{
    [Key(0)] public string InstanceId { get; init; } = "";
    [Key(1)] public long Revision { get; init; }
    [Key(2)] public DateTimeOffset AppliedUtc { get; init; }
}
