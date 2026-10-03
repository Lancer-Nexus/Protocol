using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class NpcCheckpointTests
{
    private static NpcCheckpointWriteRequestV1 Request()
    {
        var id = Guid.NewGuid();
        return new()
        {
            RequestId = Guid.NewGuid(), InstanceId = "li-01", SystemId = "li01", SimulationTick = 42,
            Npcs = [new() { NpcId = id, OwnershipVersion = 3, SystemId = "li01",
                RuntimeState = MessagePackSerializer.Serialize(new NpcRuntimeStateV1
                {
                    Orientation = new() { W = 1 }, LoadoutArchetype = "freighter",
                    Ai = new() { StateId = "idle", PreviousStateId = "idle" }
                }) }],
            ExpectedRevisions = [new() { NpcId = id, OwnershipVersion = 3, Revision = 7 }]
        };
    }

    [Fact]
    public void ExplicitKeysRoundTripWithoutChangingOwnership()
    {
        var request = Request();
        var bytes = MessagePackSerializer.Serialize(request);
        Assert.Equal(9, new MessagePackReader(bytes).ReadArrayHeader());
        var restored = MessagePackSerializer.Deserialize<NpcCheckpointWriteRequestV1>(bytes);
        NpcCheckpointContractValidator.Validate(restored);
        Assert.Equal(3, restored.Npcs[0].OwnershipVersion);
        Assert.Equal(7, restored.ExpectedRevisions[0].Revision);
        Assert.Equal(42UL, restored.SimulationTick);
        var response = new NpcCheckpointWriteResponseV1 { RequestId = request.RequestId, Accepted = true,
            ReasonCode = "checkpointed", Revisions = [request.ExpectedRevisions[0] with { Revision = 8 }] };
        var encoded = MessagePackSerializer.Serialize(response);
        Assert.Equal(5, new MessagePackReader(encoded).ReadArrayHeader());
        Assert.Equal(response.Revisions, MessagePackSerializer.Deserialize<NpcCheckpointWriteResponseV1>(encoded).Revisions);
    }

    [Fact]
    public void RetirementOnlyCheckpointIsValid()
    {
        var request = Request();
        NpcCheckpointContractValidator.Validate(request with { Npcs = [],
            Retirements = [new() { NpcId = request.Npcs[0].NpcId, OwnershipVersion = 3,
                Reason = NpcRetirementReasonV1.Docked }] });
    }

    [Fact]
    public void MissionOnlyCheckpointIsValid()
    {
        NpcCheckpointContractValidator.Validate(Request() with { Npcs = [], ExpectedRevisions = [],
            Mission = new() { RuntimeId = Guid.NewGuid(), CharacterId = 2, CharacterLeaseVersion = 4,
                RuntimeState = MessagePackSerializer.Serialize(new NpcMissionRuntimeStateV1
                { MissionNickname = "Mission_01a", RandomState = 123 }) } });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MaxValue)]
    public void InvalidRevisionIsRejected(long revision)
    {
        var request = Request();
        Assert.Throws<ProtocolViolationException>(() => NpcCheckpointContractValidator.Validate(request with {
            ExpectedRevisions = [request.ExpectedRevisions[0] with { Revision = revision }] }));
    }

    [Fact]
    public void MembershipSystemAndFencesMustMatch()
    {
        var request = Request();
        NpcCheckpointWriteRequestV1[] invalid = [
            request with { ExpectedRevisions = [] },
            request with { ExpectedRevisions = [request.ExpectedRevisions[0] with { OwnershipVersion = 2 }] },
            request with { ExpectedRevisions = [request.ExpectedRevisions[0] with { NpcId = Guid.NewGuid() }] },
            request with { Npcs = [request.Npcs[0], request.Npcs[0]], ExpectedRevisions = [request.ExpectedRevisions[0], request.ExpectedRevisions[0]] },
            request with { SystemId = "li03" },
            request with { Npcs = [], ExpectedRevisions = [] },
            request with { Retirements = [new() { NpcId = request.Npcs[0].NpcId, OwnershipVersion = 3, Reason = NpcRetirementReasonV1.Destroyed }],
                ExpectedRevisions = [request.ExpectedRevisions[0], request.ExpectedRevisions[0]] }
        ];
        foreach (var item in invalid)
            Assert.Throws<ProtocolViolationException>(() => NpcCheckpointContractValidator.Validate(item));
    }

    [Fact]
    public void RetiredFormationMemberCannotRemainInSnapshot()
    {
        var request = Request();
        var retired = Guid.NewGuid();
        Assert.Throws<ProtocolViolationException>(() => NpcCheckpointContractValidator.Validate(request with {
            Retirements = [new() { NpcId = retired, OwnershipVersion = 1, Reason = NpcRetirementReasonV1.Destroyed }],
            ExpectedRevisions = [request.ExpectedRevisions[0], new() { NpcId = retired, OwnershipVersion = 1 }],
            Formations = [new() { FormationId = Guid.NewGuid(), Members = [
                new() { NpcId = request.Npcs[0].NpcId, IsLeader = true }, new() { NpcId = retired }] }] }));
    }

    [Fact]
    public void MalformedAndUnfencedMissionStateIsRejected()
    {
        var request = Request();
        var mission = new NpcMissionCheckpointV1 { RuntimeId = Guid.NewGuid(), CharacterId = 2,
            CharacterLeaseVersion = 1, RuntimeState = MessagePackSerializer.Serialize(
                new NpcMissionRuntimeStateV1 { MissionNickname = "Mission_01a", RandomState = 123 }) };
        NpcMissionCheckpointV1[] invalid = [mission with { CharacterLeaseVersion = 0 },
            mission with { RuntimeId = Guid.Empty }, mission with { ExpectedRevision = long.MaxValue },
            mission with { RuntimeState = [0xc1] }, mission with { RuntimeState = [] }];
        foreach (var item in invalid)
            Assert.Throws<ProtocolViolationException>(() => NpcCheckpointContractValidator.Validate(request with { Mission = item }));
    }

    [Fact]
    public void FormationCharacterRequiresMatchingMissionOwner()
    {
        var request = Request();
        var formation = new NpcFormationStateV1 { FormationId = Guid.NewGuid(), Members = [
            new() { NpcId = request.Npcs[0].NpcId }, new() { CharacterId = 2, IsLeader = true }] };
        Assert.Throws<ProtocolViolationException>(() => NpcCheckpointContractValidator.Validate(request with { Formations = [formation] }));
        var mission = new NpcMissionCheckpointV1 { RuntimeId = Guid.NewGuid(), CharacterId = 2,
            CharacterLeaseVersion = 1, RuntimeState = MessagePackSerializer.Serialize(
                new NpcMissionRuntimeStateV1 { MissionNickname = "Mission_01a", RandomState = 123 }) };
        NpcCheckpointContractValidator.Validate(request with { Formations = [formation], Mission = mission });
        Assert.Throws<ProtocolViolationException>(() => NpcCheckpointContractValidator.Validate(request with {
            Formations = [formation], Mission = mission with { CharacterId = 3 } }));
    }

    [Fact]
    public void InvalidTerminalReasonIsRejected()
    {
        var request = Request();
        Assert.Throws<ProtocolViolationException>(() => NpcCheckpointContractValidator.Validate(request with {
            Npcs = [], Retirements = [new() { NpcId = request.Npcs[0].NpcId, OwnershipVersion = 3,
                Reason = (NpcRetirementReasonV1)99 }] }));
    }


    [Fact]
    public void TotalDurablePayloadLimitIsEnforced()
    {
        var request = Request();
        var state = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(request.Npcs[0].RuntimeState);
        var bytes = MessagePackSerializer.Serialize(new NpcRuntimeStateV1
        {
            Orientation = state.Orientation, LoadoutArchetype = state.LoadoutArchetype, Ai = state.Ai,
            MissionState = new byte[NpcTransferContractValidator.MaximumRuntimePayloadLength - 2048]
        });
        var npcs = Enumerable.Range(0, 4).Select(_ => new NpcRuntimeSnapshot
        { NpcId = Guid.NewGuid(), OwnershipVersion = 3, SystemId = "li01", RuntimeState = bytes }).ToArray();
        foreach (var npc in npcs) NpcTransferContractValidator.Validate(npc);
        Assert.Throws<ProtocolViolationException>(() => NpcCheckpointContractValidator.Validate(request with {
            Npcs = npcs, ExpectedRevisions = npcs.Select(npc => new NpcCheckpointRevisionV1
            { NpcId = npc.NpcId, OwnershipVersion = 3 }).ToArray() }));
    }

}
