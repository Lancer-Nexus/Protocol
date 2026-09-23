using MessagePack;

namespace LancerNexus.Protocol;

/// <summary>
/// Claims carried by a short-lived Gateway access token. The token signature and
/// key storage stay outside the shared protocol library.
/// </summary>
[MessagePackObject]
public sealed class SessionTokenClaims
{
    [Key(0)] public Guid SessionId { get; init; }
    [Key(1)] public Guid AccountId { get; init; }
    [Key(2)] public string Audience { get; init; } = "";
    [Key(3)] public DateTime IssuedAtUtc { get; init; }
    [Key(4)] public DateTime ExpiresAtUtc { get; init; }
    [Key(5)] public string Nonce { get; init; } = "";
    [Key(6)] public string? InstanceId { get; init; }
    [Key(7)] public string KeyId { get; init; } = "";
}

public static class SessionTokenClaimsValidator
{
    public static void Validate(
        SessionTokenClaims claims,
        DateTime nowUtc,
        string expectedAudience,
        TimeSpan allowedClockSkew)
    {
        if (claims.SessionId == Guid.Empty || claims.AccountId == Guid.Empty)
            throw new ProtocolViolationException("Session token must contain session and account identifiers.");
        if (string.IsNullOrWhiteSpace(claims.Audience) ||
            !string.Equals(claims.Audience, expectedAudience, StringComparison.Ordinal))
            throw new ProtocolViolationException("Session token audience is invalid.");
        if (string.IsNullOrWhiteSpace(claims.Nonce) || string.IsNullOrWhiteSpace(claims.KeyId))
            throw new ProtocolViolationException("Session token nonce and key identifier are required.");
        if (claims.ExpiresAtUtc <= claims.IssuedAtUtc)
            throw new ProtocolViolationException("Session token expiry must be after issuance.");
        if (claims.IssuedAtUtc > nowUtc + allowedClockSkew ||
            claims.ExpiresAtUtc < nowUtc - allowedClockSkew)
            throw new ProtocolViolationException("Session token is not currently valid.");
        if (allowedClockSkew < TimeSpan.Zero)
            throw new ProtocolViolationException("Allowed clock skew cannot be negative.");
    }
}
