using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>Compare-and-swap revision within a specific NPC ownership fence.</summary>
[MessagePackObject]
public sealed record NpcCheckpointRevisionV1
{
    [Key(0)] public Guid NpcId { get; init; }
    [Key(1)] public long OwnershipVersion { get; init; }
    [Key(2)] public long Revision { get; init; }
}

[MessagePackObject]
public sealed record NpcMissionCheckpointV1
{
    [Key(0)] public Guid RuntimeId { get; init; }
    [Key(1)] public long CharacterId { get; init; }
    [Key(2)] public long CharacterLeaseVersion { get; init; }
    [Key(3)] public long ExpectedRevision { get; init; }
    [Key(4)] public byte[] RuntimeState { get; init; } = [];
}

/// <summary>
/// Atomically replaces survivor state and retires terminal members. Every member
/// carries its current ownership fence and expected checkpoint revision. No
/// partial acceptance is permitted. Unknown outcomes retry identical request bytes.
/// </summary>
[MessagePackObject]
public sealed record NpcCheckpointWriteRequestV1
{
    [Key(0)] public Guid RequestId { get; init; }
    [Key(1)] public string InstanceId { get; init; } = "";
    [Key(2)] public string SystemId { get; init; } = "";
    [Key(3)] public ulong SimulationTick { get; init; }
    [Key(4)] public NpcRuntimeSnapshot[] Npcs { get; init; } = [];
    [Key(5)] public NpcFormationStateV1[] Formations { get; init; } = [];
    [Key(6)] public NpcRetirementEntryV1[] Retirements { get; init; } = [];
    [Key(7)] public NpcCheckpointRevisionV1[] ExpectedRevisions { get; init; } = [];
    [Key(8)] public NpcMissionCheckpointV1? Mission { get; init; }
}

[MessagePackObject]
public sealed record NpcCheckpointWriteResponseV1
{
    [Key(0)] public Guid RequestId { get; init; }
    [Key(1)] public bool Accepted { get; init; }
    [Key(2)] public string ReasonCode { get; init; } = "";
    [Key(3)] public NpcCheckpointRevisionV1[] Revisions { get; init; } = [];
    [Key(4)] public long? MissionRevision { get; init; }
}

/// <summary>Structural validation; authentication and durable lease arbitration remain service responsibilities.</summary>
public static class NpcCheckpointContractValidator
{
    public const int MaximumPayloadBytes = 15 * 1024 * 1024;

    public static void Validate(NpcCheckpointWriteRequestV1 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId == Guid.Empty || !Identifier(request.InstanceId) || !Identifier(request.SystemId) ||
            request.Npcs is null || request.Retirements is null || request.ExpectedRevisions is null ||
            request.Npcs.Length + request.Retirements.Length > 256 ||
            request.ExpectedRevisions.Length != request.Npcs.Length + request.Retirements.Length ||
            request.ExpectedRevisions.Length == 0 && request.Mission is null)
            throw Invalid("NPC checkpoint has invalid identity or membership limits.");

        var members = new Dictionary<Guid, long>();
        foreach (var npc in request.Npcs)
        {
            if (npc is null) throw Invalid("NPC checkpoint contains a missing survivor.");
            NpcTransferContractValidator.Validate(npc);
            if (!string.Equals(npc.SystemId, request.SystemId, StringComparison.OrdinalIgnoreCase) ||
                !members.TryAdd(npc.NpcId, npc.OwnershipVersion))
                throw Invalid("NPC checkpoint survivor has a different system or duplicate ID.");
        }
        foreach (var retired in request.Retirements)
        {
            if (retired is null || retired.NpcId == Guid.Empty || retired.OwnershipVersion is <= 0 or >= long.MaxValue ||
                !Enum.IsDefined(retired.Reason) || !members.TryAdd(retired.NpcId, retired.OwnershipVersion))
                throw Invalid("NPC checkpoint contains an invalid or duplicate retirement.");
        }
        var revisions = new HashSet<Guid>();
        foreach (var expected in request.ExpectedRevisions)
        {
            if (expected is null || expected.Revision is < 0 or >= long.MaxValue ||
                !revisions.Add(expected.NpcId) || !members.TryGetValue(expected.NpcId, out var fence) ||
                fence != expected.OwnershipVersion)
                throw Invalid("NPC checkpoint revisions must exactly match current member fences.");
        }
        NpcTransferContractValidator.ValidateFormations(request.Formations, request.Npcs.Select(npc => npc.NpcId).ToHashSet());
        if (request.Formations.SelectMany(formation => formation.Members).Any(member =>
                member.CharacterId.HasValue && member.CharacterId != request.Mission?.CharacterId))
            throw Invalid("NPC checkpoint formation references a different mission owner.");
        if (request.Mission is { } mission)
        {
            if (mission.RuntimeId == Guid.Empty || mission.CharacterId <= 0 || mission.CharacterLeaseVersion <= 0 ||
                mission.ExpectedRevision is < 0 or >= long.MaxValue ||
                mission.RuntimeState is not { Length: > 0 and <= NpcTransferContractValidator.MaximumRuntimePayloadLength })
                throw Invalid("NPC checkpoint mission has invalid identity, fence or state.");
            try
            {
                NpcTransferContractValidator.ValidateMissionRuntimeState(
                    MessagePackSerializer.Deserialize<NpcMissionRuntimeStateV1>(mission.RuntimeState,
                        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData)));
            }
            catch (MessagePackSerializationException)
            {
                throw Invalid("NPC checkpoint mission payload is malformed.");
            }
        }
        if (MessagePackSerializer.Serialize(request).Length > MaximumPayloadBytes)
            throw Invalid("NPC checkpoint exceeds the durable payload limit.");
    }

    private static bool Identifier(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 96;
    private static ProtocolViolationException Invalid(string message) => new(message);
}
