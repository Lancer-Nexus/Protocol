using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class JoinTicketContractsTests
{
    [Fact]
    public void ValidClaimsAcceptOptionalCharacterAndExpectedAudience()
    {
        var now = DateTime.UtcNow;
        var claims = Claims(now, characterId: null);

        JoinTicketClaimsValidator.Validate(claims, now, "game-server", TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveBoundCharacterId(long characterId)
    {
        var now = DateTime.UtcNow;
        var claims = Claims(now, characterId: characterId);

        Assert.Throws<ProtocolViolationException>(() =>
            JoinTicketClaimsValidator.Validate(claims, now, "game-server", TimeSpan.Zero));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void RejectsNonUtcClaimTimestamps(DateTimeKind kind)
    {
        var now = DateTime.UtcNow;
        var claims = Claims(now);

        Assert.Throws<ProtocolViolationException>(() =>
            JoinTicketClaimsValidator.Validate(
                Claims(now, issuedAtUtc: DateTime.SpecifyKind(claims.IssuedAtUtc, kind)),
                now, "game-server", TimeSpan.Zero));
        Assert.Throws<ProtocolViolationException>(() =>
            JoinTicketClaimsValidator.Validate(
                Claims(now, expiresAtUtc: DateTime.SpecifyKind(claims.ExpiresAtUtc, kind)),
                now, "game-server", TimeSpan.Zero));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void RejectsNonUtcValidationClock(DateTimeKind kind)
    {
        var now = DateTime.SpecifyKind(DateTime.UtcNow, kind);

        Assert.Throws<ProtocolViolationException>(() =>
            JoinTicketClaimsValidator.Validate(Claims(DateTime.UtcNow), now, "game-server", TimeSpan.Zero));
    }

    [Fact]
    public void RejectsNegativeClockSkewAndMissingExpectedAudience()
    {
        var now = DateTime.UtcNow;
        var claims = Claims(now);

        Assert.Throws<ProtocolViolationException>(() =>
            JoinTicketClaimsValidator.Validate(claims, now, "game-server", TimeSpan.FromTicks(-1)));
        Assert.Throws<ProtocolViolationException>(() =>
            JoinTicketClaimsValidator.Validate(claims, now, " ", TimeSpan.Zero));
    }

    [Fact]
    public void ClockSkewAppliesSymmetricallyToIssueAndExpiry()
    {
        var now = DateTime.UtcNow;
        var skew = TimeSpan.FromSeconds(5);
        var validAtFutureBoundary = Claims(
            now, issuedAtUtc: now + skew, expiresAtUtc: now + skew + TimeSpan.FromMinutes(1));
        var validAtExpiryBoundary = Claims(
            now, issuedAtUtc: now - TimeSpan.FromMinutes(1), expiresAtUtc: now - skew);

        JoinTicketClaimsValidator.Validate(validAtFutureBoundary, now, "game-server", skew);
        JoinTicketClaimsValidator.Validate(validAtExpiryBoundary, now, "game-server", skew);

        Assert.Throws<ProtocolViolationException>(() =>
            JoinTicketClaimsValidator.Validate(
                Claims(now, issuedAtUtc: now + skew + TimeSpan.FromTicks(1),
                    expiresAtUtc: now + skew + TimeSpan.FromMinutes(1)),
                now, "game-server", skew));
        Assert.Throws<ProtocolViolationException>(() =>
            JoinTicketClaimsValidator.Validate(
                Claims(now, issuedAtUtc: now - TimeSpan.FromMinutes(1),
                    expiresAtUtc: now - skew - TimeSpan.FromTicks(1)),
                now, "game-server", skew));
    }

    [Fact]
    public void RejectsNullClaims()
    {
        var now = DateTime.UtcNow;

        Assert.Throws<ProtocolViolationException>(() =>
            JoinTicketClaimsValidator.Validate(null!, now, "game-server", TimeSpan.Zero));
    }

    private static JoinTicketClaims Claims(
        DateTime now,
        long? characterId = 73,
        DateTime? issuedAtUtc = null,
        DateTime? expiresAtUtc = null) => new()
        {
            SessionId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            CharacterId = characterId,
            InstanceId = "liberty-01",
            SystemId = "li01",
            Endpoint = "10.20.0.31:2300",
            IssuedAtUtc = issuedAtUtc ?? now,
            ExpiresAtUtc = expiresAtUtc ?? now.AddMinutes(2),
            Nonce = "nonce-01",
            Audience = "game-server",
            KeyId = "gateway-join-01"
        };
}
