using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>Validates NPC transfer messages before they enter coordinator or simulation state.</summary>
public static class NpcTransferContractValidator
{
    public const int MaximumNpcsPerTransfer = 256;
    public const int MaximumRuntimePayloadLength = 4 * 1024 * 1024;

    public static void Validate(NpcIdBatchAllocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.InstanceId) ||
            request.InstanceId.Length > 96 || string.IsNullOrWhiteSpace(request.SystemId) ||
            request.SystemId.Length > 96 || request.Count is 0 or > 256)
            throw Invalid("NPC ID allocation request is invalid or exceeds the batch limit.");
    }

    public static void Validate(NpcOwnershipRegistrationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.NpcId == Guid.Empty || string.IsNullOrWhiteSpace(request.InstanceId) ||
            request.InstanceId.Length > 96 || string.IsNullOrWhiteSpace(request.SystemId) ||
            request.SystemId.Length > 96 || string.IsNullOrWhiteSpace(request.IdempotencyKey) ||
            request.IdempotencyKey.Length > 128)
            throw Invalid("NPC ownership registration has invalid identity or idempotency data.");
    }

    public static void Validate(NpcTransferPrepareRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TransferId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.SourceInstanceId) ||
            string.IsNullOrWhiteSpace(request.TargetInstanceId) ||
            string.Equals(request.SourceInstanceId, request.TargetInstanceId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.TargetSystemId) ||
            request.ExpiresUtc.Kind != DateTimeKind.Utc ||
            request.ExpiresUtc == DateTime.MinValue ||
            string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 128)
            throw Invalid("NPC transfer prepare request has invalid identifiers or expiry.");

        ValidateNpcIds(request.NpcIds);
        if (request.MissionRuntimeId == Guid.Empty || request.FormationId == Guid.Empty)
            throw Invalid("Optional NPC transfer identifiers cannot be empty GUIDs.");
    }

    public static void Validate(NpcTransferTargetResolveRequestV1 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.SourceInstanceId) || request.SourceInstanceId.Length > 96 ||
            string.IsNullOrWhiteSpace(request.TargetSystemId) || request.TargetSystemId.Length > 96)
            throw Invalid("NPC transfer target resolution has invalid instance or system identifiers.");
    }

    public static void Validate(NpcTransferSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.TransferId == Guid.Empty || snapshot.SnapshotSchemaVersion != 4 ||
            string.IsNullOrWhiteSpace(snapshot.TargetSystemId) || snapshot.TargetSystemId.Length > 96 ||
            snapshot.TargetArrivalObject is { Length: > 96 } ||
            snapshot.MissionRuntimeId is not null && snapshot.MissionRuntimeState is not { Length: > 0 and <= MaximumRuntimePayloadLength })
            throw Invalid("NPC transfer snapshot has an invalid transfer ID or unsupported schema.");
        ValidateNpcIds(snapshot.NpcIds);
        if (snapshot.MissionRuntimeId == Guid.Empty || snapshot.FormationId == Guid.Empty)
            throw Invalid("Optional NPC transfer identifiers cannot be empty GUIDs.");
        if (snapshot.Npcs is null || snapshot.Npcs.Length != snapshot.NpcIds.Length)
            throw Invalid("NPC snapshot entries must exactly match the declared NPC IDs.");
        if (snapshot.MissionRuntimeState is { Length: > 0 })
        {
            NpcMissionRuntimeStateV1 mission;
            try
            {
                mission = MessagePackSerializer.Deserialize<NpcMissionRuntimeStateV1>(snapshot.MissionRuntimeState,
                    MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData));
            }
            catch (MessagePackSerializationException)
            {
                throw Invalid("NPC mission runtime payload is not valid.");
            }
            ValidateMissionRuntimeState(mission);
        }

        var declaredIds = snapshot.NpcIds.ToHashSet();
        ValidateFormations(snapshot, declaredIds);
        var snapshotIds = new HashSet<Guid>();
        foreach (var npc in snapshot.Npcs)
        {
            if (npc is null || npc.NpcId == Guid.Empty || !snapshotIds.Add(npc.NpcId) ||
                !declaredIds.Contains(npc.NpcId) || npc.OwnershipVersion <= 0 ||
                string.IsNullOrWhiteSpace(npc.SystemId) || npc.RuntimeSchemaVersion != 3 ||
                npc.RuntimeState is null || npc.RuntimeState.Length == 0 ||
                npc.RuntimeState.Length > MaximumRuntimePayloadLength)
                throw Invalid("NPC runtime snapshot has invalid identity, ownership or payload metadata.");

            NpcRuntimeStateV1 state;
            try
            {
                state = MessagePackSerializer.Deserialize<NpcRuntimeStateV1>(npc.RuntimeState);
            }
            catch (MessagePackSerializationException)
            {
                throw Invalid("NPC runtime payload is not a valid RuntimeSchemaVersion 3 message.");
            }
            ValidateRuntimeState(state);
        }
    }

    private static void ValidateFormations(NpcTransferSnapshot snapshot, HashSet<Guid> declaredNpcIds)
    {
        if (snapshot.Formations is null || snapshot.Formations.Length > MaximumNpcsPerTransfer)
            throw Invalid("NPC formation collection is missing or exceeds its limit.");
        var formationIds = new HashSet<Guid>();
        foreach (var formation in snapshot.Formations)
        {
            if (formation is null || formation.FormationId == Guid.Empty || !formationIds.Add(formation.FormationId) ||
                formation.Members is null || formation.Members.Length is < 2 or > MaximumNpcsPerTransfer + 1 ||
                formation.Members.Count(member => member?.IsLeader == true) != 1 ||
                formation.PlayerPosition is not null && !Finite(formation.PlayerPosition) ||
                formation.PlayerTargetPosition is not null && !Finite(formation.PlayerTargetPosition))
                throw Invalid("NPC formation has invalid identity, membership or player offsets.");
            var members = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in formation.Members)
            {
                if (member is null || member.Offset is null || !Finite(member.Offset) ||
                    member.NpcId.HasValue == member.CharacterId.HasValue ||
                    member.NpcId is { } npcId && (!declaredNpcIds.Contains(npcId) || npcId == Guid.Empty) ||
                    member.CharacterId is <= 0)
                    throw Invalid("NPC formation member has an invalid stable reference or offset.");
                var key = member.NpcId.HasValue ? $"npc:{member.NpcId:D}" : $"character:{member.CharacterId}";
                if (!members.Add(key))
                    throw Invalid("NPC formation contains duplicate members.");
            }
        }
    }

    private static void ValidateMissionRuntimeState(NpcMissionRuntimeStateV1? state)
    {
        if (state is null || state.SchemaVersion != 4 || string.IsNullOrWhiteSpace(state.MissionNickname) ||
            state.MissionNickname.Length > 96 || state.RandomState == 0 ||
            state.LastSaveTrigger is null || state.CompletedTriggers is null ||
            state.CompletedTriggers.Length > 1024 || state.CompletedTriggers.Any(string.IsNullOrWhiteSpace) ||
            state.ActiveTriggers is null || state.ActiveTriggers.Length > 1024 ||
            state.PendingLines is null || state.PendingLines.Length > 256 || state.Labels is null || state.Labels.Length > 4096 ||
            state.MissionNickname.StartsWith("random-", StringComparison.OrdinalIgnoreCase) != (state.GeneratedMission is not null))
            throw Invalid("NPC mission runtime state has invalid identity or exceeds its limits.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var trigger in state.ActiveTriggers)
        {
            if (trigger is null || string.IsNullOrWhiteSpace(trigger.Nickname) || trigger.Nickname.Length > 96 ||
                !names.Add(trigger.Nickname) || !double.IsFinite(trigger.ActiveSeconds) || trigger.ActiveSeconds < 0 ||
                trigger.Satisfied is not { Length: 16 } || trigger.Conditions is null || trigger.Conditions.Length > 128 ||
                trigger.Conditions.Any(condition => condition is null ||
                    condition.Kind is not ("none" or "boolean" or "double" or "int32" or "strings") ||
                    !double.IsFinite(condition.NumberValue) || condition.StringValues is null ||
                    condition.StringValues.Length > 256 || condition.StringValues.Any(value => value is null || value.Length > 256)))
                throw Invalid("NPC mission trigger state is invalid.");
        }
        if (state.PendingLines.Any(line => line is null || string.IsNullOrWhiteSpace(line.Line) || line.Line.Length > 512))
            throw Invalid("NPC mission pending line state is invalid.");
        var labelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var label in state.Labels)
        {
            if (label is null || string.IsNullOrWhiteSpace(label.Nickname) || label.Nickname.Length > 96 ||
                !labelNames.Add(label.Nickname) || label.Alive is null || label.Destroyed is null ||
                label.Alive.Length > 4096 || label.Destroyed.Length > 4096 ||
                label.Alive.Any(string.IsNullOrWhiteSpace) || label.Destroyed.Any(string.IsNullOrWhiteSpace) ||
                label.Alive.Concat(label.Destroyed).Distinct(StringComparer.OrdinalIgnoreCase).Count() != label.Alive.Length + label.Destroyed.Length)
                throw Invalid("NPC mission label state is invalid.");
        }
        if (state.GeneratedMission is { } generated &&
            (string.IsNullOrWhiteSpace(generated.OfferBaseNickname) || generated.OfferBaseNickname.Length > 96 ||
             string.IsNullOrWhiteSpace(generated.OfferFactionNickname) || generated.OfferFactionNickname.Length > 96 ||
             string.IsNullOrWhiteSpace(generated.HostileFactionNickname) || generated.HostileFactionNickname.Length > 96 ||
             string.IsNullOrWhiteSpace(generated.DestinationSystemNickname) || generated.DestinationSystemNickname.Length > 96 ||
             string.IsNullOrWhiteSpace(generated.TargetZoneNickname) || generated.TargetZoneNickname.Length > 96 ||
             string.IsNullOrWhiteSpace(generated.TargetShipArchNickname) || generated.TargetShipArchNickname.Length > 96 ||
             generated.MissionType is not ("AssassinateMission" or "BountyMission" or "RetrieveMission" or
                 "DestroyContrabandMission" or "DestroyInstallationMission" or "DestroyMission") ||
             generated.TargetLocationIds <= 0 || generated.Reward < 0 || !float.IsFinite(generated.Difficulty) ||
             generated.Difficulty < 0 || generated.TargetPosition is null || !Finite(generated.TargetPosition) ||
             generated.Id < 0 || generated.OfferText is null || generated.OfferText.Length > 4096 ||
             generated.TargetName is null || generated.TargetName.Length > 256))
            throw Invalid("NPC generated mission descriptor is invalid.");
    }

    private static void ValidateNpcIds(Guid[]? ids)
    {
        if (ids is null || ids.Length is 0 or > MaximumNpcsPerTransfer ||
            ids.Any(id => id == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            throw Invalid("NPC transfer must contain a bounded set of unique, non-empty NPC IDs.");
    }

    private static void ValidateRuntimeState(NpcRuntimeStateV1? state)
    {
        if (state is null || !Finite(state.Position) || !Finite(state.Orientation) ||
            !Finite(state.LinearVelocity) || !Finite(state.AngularVelocity) ||
            !float.IsFinite(state.Health) || state.Health < 0 ||
            string.IsNullOrWhiteSpace(state.LoadoutArchetype) || state.Equipment is null ||
            state.Cargo is null || state.Autopilot is null || state.Ai is null || state.MissionState is null)
            throw Invalid("NPC runtime state contains invalid or missing required fields.");

        var q = state.Orientation;
        var quaternionLengthSquared = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
        if (!float.IsFinite(quaternionLengthSquared) || quaternionLengthSquared < 0.000001f)
            throw Invalid("NPC runtime orientation must be a finite, non-zero quaternion.");

        if (state.Equipment.Any(item => item is null || string.IsNullOrWhiteSpace(item.EquipmentId) ||
                string.IsNullOrWhiteSpace(item.Hardpoint) || !float.IsFinite(item.Health) || item.Health < 0 ||
                !float.IsFinite(item.Energy) || item.Energy < 0 || item.ExtensionData is null) ||
            state.Cargo.Any(item => item is null || string.IsNullOrWhiteSpace(item.ItemId) || item.Count <= 0) ||
            state.StructuralParts is null || state.StructuralParts.Length > 512 ||
            state.StructuralParts.Any(part => part is null || string.IsNullOrWhiteSpace(part.PartName) ||
                part.PartName.Length > 96 || !float.IsFinite(part.HealthFraction) ||
                part.HealthFraction is < 0 or > 1 || part.Destroyed && part.HealthFraction != 0) ||
            state.StructuralParts.Select(part => part.PartName).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
                state.StructuralParts.Length ||
            !float.IsFinite(state.Autopilot.Throttle) || state.Autopilot.Throttle is < 0 or > 1 ||
            !double.IsFinite(state.Autopilot.BehaviorElapsedSeconds) || state.Autopilot.BehaviorElapsedSeconds < 0 ||
            state.Autopilot.TargetPosition is not null && !Finite(state.Autopilot.TargetPosition) ||
            state.Autopilot.ExtensionData is null ||
            string.IsNullOrWhiteSpace(state.Ai.StateId) || string.IsNullOrWhiteSpace(state.Ai.PreviousStateId) ||
            !double.IsFinite(state.Ai.StateElapsedSeconds) || state.Ai.StateElapsedSeconds < 0 ||
            state.Ai.Timers is null || state.Ai.Timers.Any(timer => !double.IsFinite(timer) || timer < 0) ||
            state.Ai.ExtensionData is null || state.MissionState.Length > MaximumRuntimePayloadLength)
            throw Invalid("NPC runtime state contains invalid equipment, cargo, autopilot or AI data.");
    }

    private static bool Finite(NpcVector3 value) =>
        value is not null && float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool Finite(NpcQuaternion value) =>
        value is not null && float.IsFinite(value.X) && float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) && float.IsFinite(value.W);

    private static ProtocolViolationException Invalid(string message) => new(message);
}
