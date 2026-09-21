using MessagePack;

namespace LancerNexus.Protocol;

[MessagePackObject]
public sealed class ClusterEnvelope
{
    [Key(0)] public ushort Magic { get; init; } = ProtocolConstants.Magic;
    [Key(1)] public byte ProtocolVersion { get; init; } = ProtocolConstants.ProtocolVersion;
    [Key(2)] public ushort MessageType { get; init; }
    [Key(3)] public ClusterFrameFlags Flags { get; init; }
    [Key(4)] public ushort SchemaVersion { get; init; } = ProtocolConstants.CurrentSchemaVersion;
    [Key(5)] public Guid CorrelationId { get; init; }
    [Key(6)] public ulong Sequence { get; init; }
    [Key(7)] public uint PayloadLength { get; init; }
    [Key(8)] public byte[] Payload { get; init; } = [];
}

public static class ClusterEnvelopeValidator
{
    public const uint MaxPayloadLength = 4 * 1024 * 1024;

    public static void Validate(ClusterEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Magic != ProtocolConstants.Magic)
            throw new ProtocolViolationException("Invalid protocol magic.");
        if (envelope.ProtocolVersion != ProtocolConstants.ProtocolVersion)
            throw new ProtocolViolationException("Unsupported protocol version.");
        if (envelope.SchemaVersion == 0)
            throw new ProtocolViolationException("Schema version must be positive.");
        if (envelope.CorrelationId == Guid.Empty)
            throw new ProtocolViolationException("Correlation ID is required.");
        if (envelope.PayloadLength != envelope.Payload.Length)
            throw new ProtocolViolationException("Payload length does not match the payload.");
        if (envelope.PayloadLength > MaxPayloadLength)
            throw new ProtocolViolationException("Payload exceeds the protocol limit.");
    }
}

public sealed class ProtocolViolationException(string message) : Exception(message);
