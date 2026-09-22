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
    AgentHeartbeat = 200,
    InstanceHeartbeat = 201
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
