using MessagePack;

namespace LancerNexus.Protocol;

[MessagePackObject]
public sealed class JoinTicketClaims
{
    [Key(0)] public Guid SessionId { get; init; }
    [Key(1)] public Guid AccountId { get; init; }
    [Key(2)] public long? CharacterId { get; init; }
    [Key(3)] public string InstanceId { get; init; } = "";
    [Key(4)] public string SystemId { get; init; } = "";
    [Key(5)] public string Endpoint { get; init; } = "";
    [Key(6)] public DateTime IssuedAtUtc { get; init; }
    [Key(7)] public DateTime ExpiresAtUtc { get; init; }
    [Key(8)] public string Nonce { get; init; } = "";
    [Key(9)] public string Audience { get; init; } = "game-server";
    [Key(10)] public string KeyId { get; init; } = "";
}

public static class JoinTicketClaimsValidator
{
    public static void Validate(JoinTicketClaims claims, DateTime nowUtc, string expectedAudience, TimeSpan allowedClockSkew)
    {
        if (claims.SessionId == Guid.Empty || claims.AccountId == Guid.Empty ||
            string.IsNullOrWhiteSpace(claims.InstanceId) || string.IsNullOrWhiteSpace(claims.SystemId) ||
            string.IsNullOrWhiteSpace(claims.Endpoint) || string.IsNullOrWhiteSpace(claims.Nonce) ||
            string.IsNullOrWhiteSpace(claims.KeyId))
            throw new ProtocolViolationException("Join ticket identity and target fields are required.");
        if (!string.Equals(claims.Audience, expectedAudience, StringComparison.Ordinal))
            throw new ProtocolViolationException("Join ticket audience is invalid.");
        if (claims.ExpiresAtUtc <= claims.IssuedAtUtc)
            throw new ProtocolViolationException("Join ticket expiry must be after issuance.");
        if (claims.IssuedAtUtc > nowUtc + allowedClockSkew || claims.ExpiresAtUtc < nowUtc - allowedClockSkew)
            throw new ProtocolViolationException("Join ticket is not currently valid.");
        if (allowedClockSkew < TimeSpan.Zero)
            throw new ProtocolViolationException("Allowed clock skew cannot be negative.");
    }
}
