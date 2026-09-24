using MessagePack;
using System.Text.Json.Serialization;

namespace LancerNexus.Protocol;

[MessagePackObject]
public sealed class ClientVersionHello
{
    [Key(0)] public string ClientVersion { get; init; } = "";
    [Key(1)] public string BuildId { get; init; } = "";
    [Key(2)] public int ProtocolVersion { get; init; }
    [Key(3)] public string DataManifestId { get; init; } = "";
    [Key(4)] public string Platform { get; init; } = "";
    [Key(5)] public string Channel { get; init; } = "";
    [Key(6)] public string[] Capabilities { get; init; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter<ClientVersionStatus>))]
public enum ClientVersionStatus : byte
{
    [JsonStringEnumMemberName("supported")]
    Supported = 1,
    [JsonStringEnumMemberName("update_recommended")]
    UpdateRecommended = 2,
    [JsonStringEnumMemberName("update_required")]
    UpdateRequired = 3,
    [JsonStringEnumMemberName("protocol_unsupported")]
    ProtocolUnsupported = 4
}

[MessagePackObject]
public sealed class ClientVersionDecision
{
    [Key(0)] public ClientVersionStatus Status { get; init; }
    [Key(1)] public bool SessionAllowed { get; init; }
    [Key(2)] public int ServerProtocolVersion { get; init; }
    [Key(3)] public string? LatestClientVersion { get; init; }
    [Key(4)] public string? MinimumClientVersion { get; init; }
    [Key(5)] public string? RequiredDataManifestId { get; init; }
    [Key(6)] public string? UpdateChannel { get; init; }
    [Key(7)] public string? UpdateReason { get; init; }
    [Key(8)] public string MessageKey { get; init; } = "";
    [Key(9)] public string? HandshakeToken { get; init; }
}
