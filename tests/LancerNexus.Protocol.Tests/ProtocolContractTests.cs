using LancerNexus.Protocol;
using MessagePack;
using System.Text.Json;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class ProtocolContractTests
{
    [Fact]
    public void MultiSystemHeartbeatPreservesLegacyTenFieldDecoding()
    {
        var value = new InstanceHeartbeat { AgentId = "agent", InstanceId = "li-01", SystemId = "li01",
            SystemIds = ["li01", "li03"], Sequence = 1, MaxPlayers = 200, Endpoint = "127.0.0.1:26005" };
        var bytes = MessagePackSerializer.Serialize(value);
        var copy = MessagePackSerializer.Deserialize<InstanceHeartbeat>(bytes);
        Assert.Equal(value.SystemIds, copy.SystemIds);
        var reader = new MessagePackReader(bytes);
        Assert.Equal(12, reader.ReadArrayHeader());
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(10);
        for (var i = 0; i < 10; i++) writer.WriteRaw(reader.ReadRaw());
        writer.Flush();
        var legacy = MessagePackSerializer.Deserialize<InstanceHeartbeat>(buffer.WrittenMemory);
        Assert.Equal("li01", legacy.SystemId);
        Assert.Empty(legacy.SystemIds);
        Assert.Null(legacy.NpcTransferEndpoint);
    }
    [Fact]
    public void NpcPeerEndpointRoundTripsAndLegacyElevenFieldHeartbeatOmitsIt()
    {
        var value = new InstanceHeartbeat { SystemId = "li03", NpcTransferEndpoint = "quic://127.0.0.3:26456" };
        var bytes = MessagePackSerializer.Serialize(value);
        Assert.Equal(value.NpcTransferEndpoint, MessagePackSerializer.Deserialize<InstanceHeartbeat>(bytes).NpcTransferEndpoint);
        var reader = new MessagePackReader(bytes);
        Assert.Equal(12, reader.ReadArrayHeader());
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(11);
        for (var i = 0; i < 11; i++) writer.WriteRaw(reader.ReadRaw());
        writer.Flush();
        var legacy = MessagePackSerializer.Deserialize<InstanceHeartbeat>(buffer.WrittenMemory);
        Assert.Equal("li03", legacy.SystemId);
        Assert.Null(legacy.NpcTransferEndpoint);
        var prepared = new NpcTransferPrepared { TargetEndpoint = "udp://127.0.0.3:25444",
            NpcTransferEndpoint = value.NpcTransferEndpoint };
        var restored = MessagePackSerializer.Deserialize<NpcTransferPrepared>(MessagePackSerializer.Serialize(prepared));
        Assert.Equal(prepared.TargetEndpoint, restored.TargetEndpoint);
        Assert.Equal(prepared.NpcTransferEndpoint, restored.NpcTransferEndpoint);
        var target = new NpcTransferTargetResolveResultV1 { TargetEndpoint = prepared.TargetEndpoint,
            NpcTransferEndpoint = prepared.NpcTransferEndpoint };
        Assert.Equal(target.NpcTransferEndpoint, MessagePackSerializer.Deserialize<NpcTransferTargetResolveResultV1>(
            MessagePackSerializer.Serialize(target)).NpcTransferEndpoint);
    }

    [Theory]
    [InlineData("quic://127.0.0.3:26456", true)]
    [InlineData("quic://[::1]:26456", true)]
    [InlineData("udp://127.0.0.3:26456", false)]
    [InlineData("quic://127.0.0.3", false)]
    [InlineData("quic://127.0.0.3:0", false)]
    [InlineData("quic://user@host:26456", false)]
    [InlineData("quic://host:26456/path", false)]
    [InlineData("quic://host:26456?key=value", false)]
    [InlineData("quic://host:26456#fragment", false)]
    [InlineData(null, false)]
    public void PrivateNpcEndpointRequiresAnExplicitQuicPort(string? endpoint, bool valid) =>
        Assert.Equal(valid, NpcTransferContractValidator.IsValidPeerEndpoint(endpoint));
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
    public void TransferSourceRelease_RoundTripsTransferIdentity()
    {
        var request = new TransferSourceReleaseRequest { TransferId = Guid.NewGuid() };

        var copy = MessagePackSerializer.Deserialize<TransferSourceReleaseRequest>(
            MessagePackSerializer.Serialize(request));

        Assert.Equal(request.TransferId, copy.TransferId);
    }

    [Fact]
    public void TransferStatus_RoundTripsLifecycleAndInstanceBinding()
    {
        var response = new TransferStatusResponse
        {
            TransferId = Guid.NewGuid(),
            SourceInstanceId = "li01-instance",
            TargetInstanceId = "li02-instance",
            TargetSystemId = "li02",
            State = TransferState.Committed,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(1),
            LeaseVersion = 15
        };

        var copy = MessagePackSerializer.Deserialize<TransferStatusResponse>(
            MessagePackSerializer.Serialize(response));

        Assert.Equal(response.TransferId, copy.TransferId);
        Assert.Equal(response.SourceInstanceId, copy.SourceInstanceId);
        Assert.Equal(response.State, copy.State);
        Assert.Equal(response.LeaseVersion, copy.LeaseVersion);
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

    [Fact]
    public void NpcTransferContracts_RoundTripOwnershipAndRuntimeSnapshot()
    {
        var transferId = Guid.NewGuid();
        var npcId = Guid.NewGuid();
        var formationId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var prepare = new NpcTransferPrepareRequest
        {
            TransferId = transferId,
            SourceInstanceId = "li-01",
            TargetInstanceId = "rh-01",
            TargetSystemId = "rh01",
            NpcIds = [npcId],
            FormationId = formationId,
            MissionRuntimeId = missionId,
            ExpiresUtc = DateTime.UtcNow.AddSeconds(30),
            IdempotencyKey = "npc-hop-01"
        };
        var runtimeState = new NpcRuntimeStateV1
        {
            Position = new NpcVector3 { X = 10, Y = 20, Z = 30 },
            Orientation = new NpcQuaternion { W = 1 },
            LinearVelocity = new NpcVector3 { Z = -12 },
            Health = 75,
            LoadoutArchetype = "npc_fighter",
            Cargo = [new NpcCargoState { ItemId = "commodity_food", Count = 4 }],
            Autopilot = new NpcAutopilotState
            {
                Behavior = "formation",
                TargetNpcId = Guid.NewGuid(),
                BehaviorElapsedSeconds = 3.5
            },
            Ai = new NpcAiState
            {
                StateId = "evade",
                PreviousStateId = "trail",
                StateElapsedSeconds = 2.25,
                Timers = [1.5, 5],
                RandomState = 1234
            },
            CurrentTargetNpcId = Guid.NewGuid(),
            MissionState = [4, 5, 6]
        };
        var runtimeBytes = MessagePackSerializer.Serialize(runtimeState);
        var snapshot = new NpcTransferSnapshot
        {
            TransferId = transferId,
            NpcIds = [npcId],
            FormationId = formationId,
            MissionRuntimeId = missionId,
            TargetSystemId = "li02",
            MissionRuntimeState = MessagePackSerializer.Serialize(new NpcMissionRuntimeStateV1
            {
                MissionNickname = "random-7e7c6f2e7a274db39e0bd3588e6ab333",
                RandomState = 1,
                GeneratedMission = new NpcGeneratedMissionState
                {
                    OfferBaseNickname = "li01_01_base",
                    OfferFactionNickname = "li_n_grp",
                    HostileFactionNickname = "pi_grp",
                    DestinationSystemNickname = "li01",
                    TargetZoneNickname = "Zone_Li01_Tradelane_1",
                    TargetShipArchNickname = "pi_fighter",
                    MissionType = "DestroyMission",
                    TargetLocationIds = 12345,
                    Reward = 2500,
                    Difficulty = 2.5f,
                    Seed = 42,
                    TargetPosition = new NpcVector3 { X = 100, Y = 200, Z = -300 },
                    Id = 81,
                    OfferText = "Generated contract test",
                    TargetName = "Test target"
                }
            }),
            Npcs = [new NpcRuntimeSnapshot
            {
                NpcId = npcId,
                OwnershipVersion = 9,
                SystemId = "rh01",
                RuntimeSchemaVersion = 3,
                RuntimeState = runtimeBytes
            }]
        };
        var phase = new NpcTransferPhaseRequest
        {
            TransferId = transferId,
            State = NpcTransferState.SourceFrozen,
            Snapshot = snapshot
        };

        NpcTransferContractValidator.Validate(prepare);
        NpcTransferContractValidator.Validate(snapshot);

        var invalidRandomMissionSnapshot = new NpcTransferSnapshot
        {
            TransferId = snapshot.TransferId,
            NpcIds = snapshot.NpcIds,
            MissionRuntimeId = snapshot.MissionRuntimeId,
            TargetSystemId = snapshot.TargetSystemId,
            Npcs = snapshot.Npcs,
            MissionRuntimeState = MessagePackSerializer.Serialize(new NpcMissionRuntimeStateV1
            {
                MissionNickname = "random-missing-descriptor",
                RandomState = 1
            })
        };
        Assert.Throws<ProtocolViolationException>(() => NpcTransferContractValidator.Validate(invalidRandomMissionSnapshot));

        var prepareCopy = MessagePackSerializer.Deserialize<NpcTransferPrepareRequest>(
            MessagePackSerializer.Serialize(prepare));
        var phaseCopy = MessagePackSerializer.Deserialize<NpcTransferPhaseRequest>(
            MessagePackSerializer.Serialize(phase));
        var lease = new NpcOwnershipLease
        {
            NpcId = npcId,
            InstanceId = "li-01",
            OwnershipVersion = 8,
            ActiveTransferId = transferId
        };
        var leaseCopy = MessagePackSerializer.Deserialize<NpcOwnershipLease>(
            MessagePackSerializer.Serialize(lease));

        Assert.Equal(prepare.NpcIds, prepareCopy.NpcIds);
        Assert.Equal(prepare.MissionRuntimeId, prepareCopy.MissionRuntimeId);
        Assert.Equal(NpcTransferState.SourceFrozen, phaseCopy.State);
        var runtimeCopy = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(
            phaseCopy.Snapshot!.Npcs[0].RuntimeState);
        Assert.Equal(runtimeState.Position.X, runtimeCopy.Position.X);
        Assert.Equal(runtimeState.Cargo[0].ItemId, runtimeCopy.Cargo[0].ItemId);
        Assert.Equal(runtimeState.Autopilot.Behavior, runtimeCopy.Autopilot.Behavior);
        Assert.Equal(runtimeState.Ai.StateId, runtimeCopy.Ai.StateId);
        Assert.Equal(runtimeState.Ai.RandomState, runtimeCopy.Ai.RandomState);
        Assert.Equal(runtimeState.MissionState, runtimeCopy.MissionState);
        var missionCopy = MessagePackSerializer.Deserialize<NpcMissionRuntimeStateV1>(
            phaseCopy.Snapshot!.MissionRuntimeState);
        Assert.Equal(snapshot.MissionRuntimeState, phaseCopy.Snapshot.MissionRuntimeState);
        Assert.Equal("random-7e7c6f2e7a274db39e0bd3588e6ab333", missionCopy.MissionNickname);
        Assert.Equal("Zone_Li01_Tradelane_1", missionCopy.GeneratedMission!.TargetZoneNickname);
        Assert.Equal(42, missionCopy.GeneratedMission.Seed);
        Assert.Equal(81, missionCopy.GeneratedMission.Id);
        Assert.Equal("Generated contract test", missionCopy.GeneratedMission.OfferText);
        Assert.Equal(lease.OwnershipVersion, leaseCopy.OwnershipVersion);
        Assert.Equal("npc_transfer_v1", ClusterCapabilities.NpcTransferV1);
    }

    [Fact]
    public void NpcTransferTargetResolution_UsesVersionedContractAndValidatesIdentifiers()
    {
        var request = new NpcTransferTargetResolveRequestV1
        {
            SourceInstanceId = "li-01",
            TargetSystemId = "rh01"
        };
        NpcTransferContractValidator.Validate(request);
        var copy = MessagePackSerializer.Deserialize<NpcTransferTargetResolveRequestV1>(
            MessagePackSerializer.Serialize(request));
        Assert.Equal(request.SourceInstanceId, copy.SourceInstanceId);
        Assert.Equal(request.TargetSystemId, copy.TargetSystemId);
        Assert.Throws<ProtocolViolationException>(() => NpcTransferContractValidator.Validate(
            new NpcTransferTargetResolveRequestV1 { SourceInstanceId = "li-01" }));
    }

    [Fact]
    public void NpcTransferFormations_RoundTripAndRejectPartialMembership()
    {
        var npcId = Guid.NewGuid();
        var formation = new NpcFormationStateV1
        {
            FormationId = Guid.NewGuid(),
            Members =
            [
                new NpcFormationMemberV1 { IsLeader = true, NpcId = npcId },
                new NpcFormationMemberV1 { CharacterId = 42, Offset = new NpcVector3 { X = 60 } }
            ],
            PlayerPosition = new NpcVector3 { Y = -60 },
            PlayerTargetPosition = new NpcVector3 { Z = 10 }
        };
        var snapshot = CreateNpcSnapshot(npcId, formation);
        var copy = MessagePackSerializer.Deserialize<NpcTransferSnapshot>(MessagePackSerializer.Serialize(snapshot));
        NpcTransferContractValidator.Validate(copy);
        Assert.Equal(formation.FormationId, copy.Formations[0].FormationId);
        Assert.Equal(42, copy.Formations[0].Members[1].CharacterId);
        Assert.Equal(60, copy.Formations[0].Members[1].Offset.X);

        var partial = CreateNpcSnapshot(npcId, new NpcFormationStateV1
        {
            FormationId = Guid.NewGuid(),
            Members =
            [
                new NpcFormationMemberV1 { IsLeader = true, NpcId = Guid.NewGuid() },
                new NpcFormationMemberV1 { NpcId = npcId }
            ]
        });
        Assert.Throws<ProtocolViolationException>(() => NpcTransferContractValidator.Validate(partial));
    }

    private static NpcTransferSnapshot CreateNpcSnapshot(Guid npcId, NpcFormationStateV1 formation) => new()
    {
        TransferId = Guid.NewGuid(),
        NpcIds = [npcId],
        Formations = [formation],
        TargetSystemId = "li02",
        Npcs =
        [
            new NpcRuntimeSnapshot
            {
                NpcId = npcId,
                OwnershipVersion = 1,
                SystemId = "li01",
                RuntimeState = MessagePackSerializer.Serialize(new NpcRuntimeStateV1
                {
                    Orientation = new NpcQuaternion { W = 1 },
                    LoadoutArchetype = "npc_fighter",
                    Autopilot = new NpcAutopilotState { Behavior = "None" },
                    Ai = new NpcAiState { StateId = "none", PreviousStateId = "none" }
                })
            }
        ]
    };

    [Fact]
    public void NpcTransferContractValidator_RejectsDuplicateIdsAndInvalidRuntimeNumbers()
    {
        var npcId = Guid.NewGuid();
        var request = new NpcTransferPrepareRequest
        {
            TransferId = Guid.NewGuid(),
            SourceInstanceId = "li-01",
            TargetInstanceId = "rh-01",
            TargetSystemId = "rh01",
            NpcIds = [npcId, npcId],
            ExpiresUtc = DateTime.UtcNow.AddSeconds(30),
            IdempotencyKey = "duplicate-npc"
        };
        Assert.Throws<ProtocolViolationException>(() => NpcTransferContractValidator.Validate(request));

        var invalidState = new NpcRuntimeStateV1
        {
            Position = new NpcVector3 { X = float.NaN },
            LoadoutArchetype = "npc_fighter",
            Ai = new NpcAiState { StateId = "idle", PreviousStateId = "idle" }
        };
        var snapshot = new NpcTransferSnapshot
        {
            TransferId = Guid.NewGuid(),
            NpcIds = [npcId],
            TargetSystemId = "rh01",
            Npcs = [new NpcRuntimeSnapshot
            {
                NpcId = npcId,
                OwnershipVersion = 1,
                SystemId = "rh01",
                RuntimeState = MessagePackSerializer.Serialize(invalidState)
            }]
        };
        Assert.Throws<ProtocolViolationException>(() => NpcTransferContractValidator.Validate(snapshot));
    }

    [Fact]
    public void NpcTransferCapability_IsRejectedWhenPeerDoesNotAdvertiseIt()
    {
        var local = Hello("local", [ClusterCapabilities.NpcTransferV1]);
        var peer = Hello("peer", ["heartbeat_v1"]);

        var result = ClusterHandshakeNegotiator.Negotiate(
            local, peer, [ClusterCapabilities.NpcTransferV1]);

        Assert.False(result.Accepted);
        Assert.Equal("missing_required_capability", result.ReasonCode);
    }

    [Fact]
    public void NpcOwnershipRegistrationContract_RoundTripsIdempotencyAndIdentity()
    {
        var request = new NpcOwnershipRegistrationRequest
        {
            NpcId = Guid.NewGuid(),
            InstanceId = "li-01",
            SystemId = "li01",
            IdempotencyKey = "spawn-42"
        };
        var copy = MessagePackSerializer.Deserialize<NpcOwnershipRegistrationRequest>(
            MessagePackSerializer.Serialize(request));
        Assert.Equal(request.NpcId, copy.NpcId);
        Assert.Equal(request.InstanceId, copy.InstanceId);
        Assert.Equal(request.SystemId, copy.SystemId);
        Assert.Equal(request.IdempotencyKey, copy.IdempotencyKey);
        NpcTransferContractValidator.Validate(request);
        Assert.Throws<ProtocolViolationException>(() => NpcTransferContractValidator.Validate(
            new NpcOwnershipRegistrationRequest { NpcId = Guid.Empty }));
    }

    [Fact]
    public void NpcIdBatchAllocation_RoundTripsCoordinatorIssuedLeases()
    {
        var request = new NpcIdBatchAllocationRequest
        {
            RequestId = Guid.NewGuid(),
            InstanceId = "li-01",
            SystemId = "li01",
            Count = 128
        };
        var requestCopy = MessagePackSerializer.Deserialize<NpcIdBatchAllocationRequest>(
            MessagePackSerializer.Serialize(request));
        NpcTransferContractValidator.Validate(requestCopy);
        Assert.Equal(request.RequestId, requestCopy.RequestId);
        Assert.Equal(request.Count, requestCopy.Count);

        var response = new NpcIdBatchAllocationResponse
        {
            RequestId = request.RequestId,
            Accepted = true,
            ReasonCode = "allocated",
            Npcs = [new NpcOwnershipLease
            {
                NpcId = Guid.NewGuid(),
                InstanceId = request.InstanceId,
                OwnershipVersion = 1
            }]
        };
        var responseCopy = MessagePackSerializer.Deserialize<NpcIdBatchAllocationResponse>(
            MessagePackSerializer.Serialize(response));
        Assert.Equal(response.RequestId, responseCopy.RequestId);
        Assert.Equal(response.Npcs[0].NpcId, responseCopy.Npcs[0].NpcId);
        Assert.Equal(response.Npcs[0].OwnershipVersion, responseCopy.Npcs[0].OwnershipVersion);
    }

    [Fact]
    public void NpcPeerSnapshotTransfer_RoundTripsAndRejectsOversizedPayloads()
    {
        var request = new NpcPeerSnapshotTransfer
        {
            TransferId = Guid.NewGuid(),
            SourceInstanceId = "li-01",
            TargetInstanceId = "br-01",
            SnapshotMessagePack = [1, 2, 3]
        };
        var copy = MessagePackSerializer.Deserialize<NpcPeerSnapshotTransfer>(MessagePackSerializer.Serialize(request));
        NpcPeerSnapshotTransferValidator.Validate(copy);
        Assert.Equal(request.TransferId, copy.TransferId);
        Assert.Equal(request.SnapshotMessagePack, copy.SnapshotMessagePack);
        Assert.Throws<ProtocolViolationException>(() => NpcPeerSnapshotTransferValidator.Validate(
            new NpcPeerSnapshotTransfer
            {
                TransferId = Guid.NewGuid(),
                SourceInstanceId = "li-01",
                TargetInstanceId = "br-01",
                SnapshotMessagePack = new byte[NpcPeerSnapshotTransferValidator.MaximumSnapshotBytes + 1]
            }));
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
