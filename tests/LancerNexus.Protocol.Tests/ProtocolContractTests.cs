using LancerNexus.Protocol;
using MessagePack;
using System.Text.Json;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class ProtocolContractTests
{
    [Fact]
    public void ClientVersionContracts_RoundTripWithStableKeys()
    {
        var hello = new ClientVersionHello
        {
            ClientVersion = "1.0.1",
            BuildId = "20260923.1",
            ProtocolVersion = 1,
            DataManifestId = "data-2026-09-22",
            Platform = "linux-x64",
            Channel = "stable",
            Capabilities = ["transfer-v1"]
        };
        var copy = MessagePackSerializer.Deserialize<ClientVersionHello>(MessagePackSerializer.Serialize(hello));
        Assert.Equal(hello.ClientVersion, copy.ClientVersion);
        Assert.Equal(hello.DataManifestId, copy.DataManifestId);
        Assert.Equal(hello.Capabilities, copy.Capabilities);

        var decision = new ClientVersionDecision
        {
            Status = ClientVersionStatus.UpdateRequired,
            SessionAllowed = false,
            ServerProtocolVersion = 1,
            MinimumClientVersion = "1.0.1",
            RequiredDataManifestId = "data-2026-09-22",
            UpdateChannel = "stable",
            UpdateReason = "client_version_not_supported",
            MessageKey = "client_update_required"
        };
        var roundTrip = MessagePackSerializer.Deserialize<ClientVersionDecision>(MessagePackSerializer.Serialize(decision));
        Assert.Equal(decision.Status, roundTrip.Status);
        Assert.False(roundTrip.SessionAllowed);
        Assert.Equal(decision.UpdateReason, roundTrip.UpdateReason);
        Assert.Contains("\"status\":\"update_required\"", JsonSerializer.Serialize(decision,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

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
    public void TransferContracts_RoundTripWithStableFields()
    {
        var transferId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var prepare = new TransferPrepareRequest
        {
            TransferId = transferId,
            SessionId = sessionId,
            CharacterId = 42,
            SourceInstanceId = "new-york-01",
            TargetInstanceId = "california-01",
            TargetSystemId = "li02",
            GroupId = "group-01",
            ExpiresUtc = DateTime.UtcNow.AddSeconds(30),
            IdempotencyKey = "transfer-01"
        };
        var prepared = new TransferPrepared
        {
            TransferId = transferId,
            Accepted = true,
            TransferTicket = "short-lived-ticket",
            ExpiresUtc = prepare.ExpiresUtc,
            ReasonCode = "prepared"
        };
        var commit = new TransferCommit
        {
            TransferId = transferId,
            CharacterId = prepare.CharacterId,
            LeaseVersion = 8,
            Snapshot = [1, 2, 3]
        };
        var abort = new TransferAbort
        {
            TransferId = transferId,
            ReasonCode = "target_unavailable",
            Retryable = true
        };

        var prepareCopy = MessagePackSerializer.Deserialize<TransferPrepareRequest>(MessagePackSerializer.Serialize(prepare));
        var preparedCopy = MessagePackSerializer.Deserialize<TransferPrepared>(MessagePackSerializer.Serialize(prepared));
        var commitCopy = MessagePackSerializer.Deserialize<TransferCommit>(MessagePackSerializer.Serialize(commit));
        var abortCopy = MessagePackSerializer.Deserialize<TransferAbort>(MessagePackSerializer.Serialize(abort));

        Assert.Equal(prepare.TransferId, prepareCopy.TransferId);
        Assert.Equal(prepare.TargetInstanceId, prepareCopy.TargetInstanceId);
        Assert.Equal(prepare.IdempotencyKey, prepareCopy.IdempotencyKey);
        Assert.Equal(prepared.TransferTicket, preparedCopy.TransferTicket);
        Assert.Equal(prepared.ExpiresUtc, preparedCopy.ExpiresUtc);
        Assert.Equal(commit.LeaseVersion, commitCopy.LeaseVersion);
        Assert.Equal(commit.Snapshot, commitCopy.Snapshot);
        Assert.Equal(abort.ReasonCode, abortCopy.ReasonCode);
        Assert.True(abortCopy.Retryable);
    }

    [Fact]
    public void TransferTicketClaims_RoundTripWithTargetAndLeaseBinding()
    {
        var claims = new TransferTicketClaims
        {
            TransferId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            CharacterId = 73,
            SourceInstanceId = "new-york-01",
            TargetInstanceId = "california-01",
            TargetSystemId = "li02",
            LeaseVersion = 14,
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(60),
            Nonce = "transfer-nonce",
            Audience = "game-server-transfer",
            KeyId = "transfer-key-01"
        };

        var copy = MessagePackSerializer.Deserialize<TransferTicketClaims>(
            MessagePackSerializer.Serialize(claims));

        Assert.Equal(claims.TransferId, copy.TransferId);
        Assert.Equal(claims.TargetInstanceId, copy.TargetInstanceId);
        Assert.Equal(claims.LeaseVersion, copy.LeaseVersion);
        Assert.Equal(claims.Audience, copy.Audience);
    }

    [Fact]
    public void TransferStartContracts_RoundTripTargetAndIdempotency()
    {
        var request = new TransferStartRequest
        {
            TransferId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            CharacterId = 73,
            TargetInstanceId = "california-01",
            TargetSystemId = "li02",
            ExpiresUtc = DateTime.UtcNow.AddSeconds(45),
            IdempotencyKey = "jump-73-li02"
        };
        var result = new TransferStartResult
        {
            Prepared = new TransferPrepared
            {
                TransferId = request.TransferId,
                Accepted = true,
                TransferTicket = "signed-transfer-ticket",
                ExpiresUtc = request.ExpiresUtc,
                ReasonCode = "prepared"
            },
            SourceInstanceId = "new-york-01",
            TargetEndpoint = "10.0.0.2:2300",
            TargetSystemId = request.TargetSystemId,
            LeaseVersion = 14,
            Duplicate = false
        };

        var requestCopy = MessagePackSerializer.Deserialize<TransferStartRequest>(
            MessagePackSerializer.Serialize(request));
        var resultCopy = MessagePackSerializer.Deserialize<TransferStartResult>(
            MessagePackSerializer.Serialize(result));

        Assert.Equal(request.IdempotencyKey, requestCopy.IdempotencyKey);
        Assert.Equal(request.TargetInstanceId, requestCopy.TargetInstanceId);
        Assert.Equal(result.Prepared.TransferTicket, resultCopy.Prepared.TransferTicket);
        Assert.Equal(result.LeaseVersion, resultCopy.LeaseVersion);
    }

    [Fact]
    public void TransferTicketVerification_BindsActualTargetInstance()
    {
        var request = new TransferTicketVerificationRequest
        {
            Ticket = "opaque-ticket",
            TargetInstanceId = "california-01"
        };

        var copy = MessagePackSerializer.Deserialize<TransferTicketVerificationRequest>(
            MessagePackSerializer.Serialize(request));

        Assert.Equal(request.TargetInstanceId, copy.TargetInstanceId);
    }

    [Fact]
    public void TransferTargetAcceptance_RoundTripsTicketAndLeaseCredential()
    {
        var request = new TransferTargetAcceptanceRequest
        {
            Ticket = "transfer-ticket",
            TargetLeaseToken = "short-lived-target-lease-token"
        };

        var copy = MessagePackSerializer.Deserialize<TransferTargetAcceptanceRequest>(
            MessagePackSerializer.Serialize(request));

        Assert.Equal(request.Ticket, copy.Ticket);
        Assert.Equal(request.TargetLeaseToken, copy.TargetLeaseToken);
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
    public void SessionTokenClaims_RoundTripAndValidate()
    {
        var now = DateTime.UtcNow;
        var claims = new SessionTokenClaims
        {
            SessionId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Audience = "game-server",
            IssuedAtUtc = now.AddMinutes(-1),
            ExpiresAtUtc = now.AddMinutes(9),
            Nonce = "nonce-01",
            InstanceId = "liberty-01",
            KeyId = "gateway-key-01"
        };

        var copy = MessagePackSerializer.Deserialize<SessionTokenClaims>(
            MessagePackSerializer.Serialize(claims));

        SessionTokenClaimsValidator.Validate(copy, now, "game-server", TimeSpan.FromSeconds(5));
        Assert.Equal(claims.SessionId, copy.SessionId);
        Assert.Equal(claims.AccountId, copy.AccountId);
        Assert.Equal(claims.InstanceId, copy.InstanceId);
    }

    [Fact]
    public void SessionTokenClaims_RejectWrongAudienceAndExpiredToken()
    {
        var now = DateTime.UtcNow;
        var claims = new SessionTokenClaims
        {
            SessionId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            Audience = "game-server",
            IssuedAtUtc = now.AddMinutes(-10),
            ExpiresAtUtc = now.AddMinutes(-1),
            Nonce = "nonce-01",
            KeyId = "gateway-key-01"
        };

        Assert.Throws<ProtocolViolationException>(() =>
            SessionTokenClaimsValidator.Validate(claims, now, "coordinator", TimeSpan.Zero));
        Assert.Throws<ProtocolViolationException>(() =>
            SessionTokenClaimsValidator.Validate(claims, now, "game-server", TimeSpan.Zero));
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
