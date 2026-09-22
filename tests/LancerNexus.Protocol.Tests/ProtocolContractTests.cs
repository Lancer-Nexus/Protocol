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

    [Fact]
    public void HeartbeatContracts_RoundTripWithStableFields()
    {
        var heartbeat = new InstanceHeartbeat
        {
            AgentId = "agent-01",
            InstanceId = "liberty-01",
            SystemId = "li01",
            Sequence = 42,
            IsReady = true,
            MaxPlayers = 100,
            Endpoint = "quic://10.0.0.1:7443",
            Capabilities = ["cluster_transfer_v1"]
        };

        var copy = MessagePackSerializer.Deserialize<InstanceHeartbeat>(
            MessagePackSerializer.Serialize(heartbeat));

        Assert.Equal(heartbeat.AgentId, copy.AgentId);
        Assert.Equal(heartbeat.InstanceId, copy.InstanceId);
        Assert.Equal(heartbeat.Sequence, copy.Sequence);
        Assert.Equal(heartbeat.Endpoint, copy.Endpoint);
    }

    [Fact]
    public void AgentHeartbeatResponse_RoundTripsWithStableFields()
    {
        var response = new AgentHeartbeatResponse
        {
            Accepted = true,
            ReasonCode = "accepted",
            Sequence = 17
        };

        var copy = MessagePackSerializer.Deserialize<AgentHeartbeatResponse>(
            MessagePackSerializer.Serialize(response));

        Assert.True(copy.Accepted);
        Assert.Equal("accepted", copy.ReasonCode);
        Assert.Equal(17UL, copy.Sequence);
    }

    [Fact]
    public void InstanceHeartbeatResponse_RoundTripsWithStableFields()
    {
        var response = new InstanceHeartbeatResponse
        {
            Accepted = false,
            ReasonCode = "agent_certificate_mismatch",
            Sequence = 23
        };

        var copy = MessagePackSerializer.Deserialize<InstanceHeartbeatResponse>(
            MessagePackSerializer.Serialize(response));

        Assert.False(copy.Accepted);
        Assert.Equal("agent_certificate_mismatch", copy.ReasonCode);
        Assert.Equal(23UL, copy.Sequence);
    }

    [Fact]
    public void HandshakeNegotiatesCommonCapabilitiesDeterministically()
    {
        var local = Hello("local", ["transfer_v1", "heartbeat_v1", "placement_v1"]);
        var peer = Hello("peer", ["placement_v1", "transfer_v1"], ["li02", "li01"]);

        var result = ClusterHandshakeNegotiator.Negotiate(local, peer, ["transfer_v1"]);

        Assert.True(result.Accepted);
        Assert.Equal("accepted", result.ReasonCode);
        Assert.Equal(["placement_v1", "transfer_v1"], result.NegotiatedCapabilities);
        Assert.Equal(["li01", "li02"], result.PeerOwnedSystems);
    }

    [Fact]
    public void HandshakeRejectsUnsupportedVersionAndMissingRequiredCapability()
    {
        var local = Hello("local", ["heartbeat_v1"]);
        var peer = Hello("peer", ["heartbeat_v1"]);

        var missing = ClusterHandshakeNegotiator.Negotiate(local, peer, ["transfer_v1"]);
        var unsupported = ClusterHandshakeNegotiator.Negotiate(
            local,
            new ClusterHello
            {
                NodeId = "peer",
                InstanceId = "peer-instance",
                BuildVersion = "test",
                ProtocolVersion = 2
            });

        Assert.False(missing.Accepted);
        Assert.Equal("missing_required_capability", missing.ReasonCode);
        Assert.False(unsupported.Accepted);
        Assert.Equal("unsupported_protocol_version", unsupported.ReasonCode);
    }

    [Fact]
    public void HandshakeResponse_RoundTripsWithExplicitFields()
    {
        var response = new ClusterHandshakeResponse
        {
            Accepted = true,
            ReasonCode = "accepted",
            NegotiatedCapabilities = ["cluster_handshake_v1"]
        };

        var copy = MessagePackSerializer.Deserialize<ClusterHandshakeResponse>(
            MessagePackSerializer.Serialize(response));

        Assert.True(copy.Accepted);
        Assert.Equal("accepted", copy.ReasonCode);
        Assert.Equal(["cluster_handshake_v1"], copy.NegotiatedCapabilities);
    }

    private static ClusterHello Hello(string nodeId, string[] capabilities, string[]? ownedSystems = null) => new()
    {
        NodeId = nodeId,
        InstanceId = $"{nodeId}-instance",
        BuildVersion = "test",
        Capabilities = capabilities,
        OwnedSystems = ownedSystems ?? []
    };
}
