using LancerNexus.Protocol;
using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class ProtocolContractTests
{
    [Fact]
    public void Envelope_RoundTrips_WithExplicitFields()
    {
        var envelope = new ClusterEnvelope
        {
            MessageType = (ushort)ClusterMessageType.Hello,
            Flags = ClusterFrameFlags.Request,
            CorrelationId = Guid.NewGuid(),
            Sequence = 7,
            Payload = [1, 2, 3],
            PayloadLength = 3
        };

        var copy = MessagePackSerializer.Deserialize<ClusterEnvelope>(MessagePackSerializer.Serialize(envelope));

        Assert.Equal(envelope.Magic, copy.Magic);
        Assert.Equal(envelope.ProtocolVersion, copy.ProtocolVersion);
        Assert.Equal(envelope.MessageType, copy.MessageType);
        Assert.Equal(envelope.CorrelationId, copy.CorrelationId);
        Assert.Equal(envelope.Payload, copy.Payload);
        ClusterEnvelopeValidator.Validate(copy);
    }

    [Fact]
    public void Envelope_Rejects_MismatchedPayloadLength()
    {
        var envelope = new ClusterEnvelope
        {
            MessageType = (ushort)ClusterMessageType.Hello,
            CorrelationId = Guid.NewGuid(),
            Payload = [1],
            PayloadLength = 2
        };

        Assert.Throws<ProtocolViolationException>(() => ClusterEnvelopeValidator.Validate(envelope));
    }

    [Fact]
    public void TransferStates_ExposeAuthoritativeOrder()
    {
        Assert.True(TransferState.Requested < TransferState.Reserved);
        Assert.True(TransferState.Reserved < TransferState.Prepared);
        Assert.True(TransferState.Prepared < TransferState.SourceFrozen);
        Assert.True(TransferState.SourceFrozen < TransferState.TargetAccepted);
        Assert.True(TransferState.TargetAccepted < TransferState.Committed);
        Assert.True(TransferState.Committed < TransferState.SourceReleased);
    }
}
