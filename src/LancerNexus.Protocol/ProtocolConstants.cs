namespace LancerNexus.Protocol;

public static class ProtocolConstants
{
    public const ushort Magic = 0x4C4E;
    public const byte ProtocolVersion = 1;
    public const ushort CurrentSchemaVersion = 1;
}

public enum ClusterMessageType : ushort
{
    Hello = 1,
    PlacementRequest = 10,
    PlacementDecision = 11,
    TransferPrepare = 100,
    TransferPrepared = 101,
    TransferCommit = 102,
    TransferAbort = 103,
    TransferComplete = 104,
    NpcTransferPrepare = 105,
    NpcTransferPrepared = 106,
    NpcTransferPhase = 107,
    AgentHeartbeat = 200,
    InstanceHeartbeat = 201,
    AgentHeartbeatResponse = 202,
    InstanceHeartbeatResponse = 203,
    PermissionRevisionChanged = 300,
    PermissionRevisionAcknowledged = 301
}

public static class ClusterCapabilities
{
    public const string NpcCheckpointV1 = "npc_checkpoint_v1";
    public const string NpcOwnershipV1 = "npc_ownership_v1";
    public const string NpcRetirementV1 = "npc_retirement_v1";
    public const string NpcTransferV1 = "npc_transfer_v1";
}

[Flags]
public enum ClusterFrameFlags : ushort
{
    None = 0,
    Request = 1,
    Response = 2,
    Error = 4,
    Idempotent = 8
}
