namespace LancerNexus.Protocol;

public sealed record ClusterHandshakeResult(
    bool Accepted,
    string ReasonCode,
    string[] NegotiatedCapabilities,
    string[] PeerOwnedSystems);

/// <summary>
/// Performs deterministic protocol and capability checks after the transport has authenticated
/// its peer. This does not replace QUIC TLS/mTLS authentication.
/// </summary>
public static class ClusterHandshakeNegotiator
{
    public static ClusterHandshakeResult Negotiate(
        ClusterHello localHello,
        ClusterHello peerHello,
        IEnumerable<string>? locallyRequiredCapabilities = null)
    {
        ArgumentNullException.ThrowIfNull(localHello);
        ArgumentNullException.ThrowIfNull(peerHello);

        if (!IsValid(localHello) || !IsValid(peerHello))
            return Rejected("invalid_hello");
        if (localHello.ProtocolVersion != ProtocolConstants.ProtocolVersion ||
            peerHello.ProtocolVersion != ProtocolConstants.ProtocolVersion)
            return Rejected("unsupported_protocol_version");

        var localCapabilities = localHello.Capabilities.ToHashSet(StringComparer.Ordinal);
        var peerCapabilities = peerHello.Capabilities.ToHashSet(StringComparer.Ordinal);
        var required = (locallyRequiredCapabilities ?? []).ToArray();
        if (required.Any(string.IsNullOrWhiteSpace) || required.Distinct(StringComparer.Ordinal).Count() != required.Length)
            return Rejected("invalid_required_capabilities");
        if (required.Any(capability => !peerCapabilities.Contains(capability)))
            return Rejected("missing_required_capability");

        return new ClusterHandshakeResult(
            true,
            "accepted",
            localCapabilities.Intersect(peerCapabilities, StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            peerHello.OwnedSystems.Order(StringComparer.Ordinal).ToArray());
    }

    private static bool IsValid(ClusterHello hello) =>
        !string.IsNullOrWhiteSpace(hello.NodeId) &&
        !string.IsNullOrWhiteSpace(hello.InstanceId) &&
        !string.IsNullOrWhiteSpace(hello.BuildVersion) &&
        hello.Capabilities is not null &&
        hello.OwnedSystems is not null &&
        hello.Capabilities.All(capability => !string.IsNullOrWhiteSpace(capability)) &&
        hello.Capabilities.Distinct(StringComparer.Ordinal).Count() == hello.Capabilities.Length;

    private static ClusterHandshakeResult Rejected(string reasonCode) => new(false, reasonCode, [], []);
}
