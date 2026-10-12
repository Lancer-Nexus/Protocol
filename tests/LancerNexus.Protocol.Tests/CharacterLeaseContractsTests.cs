using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class CharacterLeaseContractsTests
{
    [Fact]
    public void AcquireRequestRoundTripsWithStableFieldOrder()
    {
        var request = Request();

        Assert.True(request.IsValid());
        var bytes = MessagePackSerializer.Serialize(request);
        var decoded = MessagePackSerializer.Deserialize<CharacterLeaseAcquireRequestV1>(bytes);
        Assert.Equal(request, decoded);
        Assert.Equal(5, new MessagePackReader(bytes).ReadArrayHeader());
    }

    [Fact]
    public void AcquireRequestRejectsMissingIdentityTicketAndMalformedLeaseToken()
    {
        var request = Request();

        Assert.False((request with { SchemaVersion = 2 }).IsValid());
        Assert.False((request with { CorrelationId = Guid.Empty }).IsValid());
        Assert.False((request with { CharacterId = 0 }).IsValid());
        Assert.False((request with { JoinTicket = "" }).IsValid());
        Assert.False((request with { JoinTicket = null! }).IsValid());
        Assert.False((request with { LeaseToken = "short" }).IsValid());
        Assert.False((request with { LeaseToken = null! }).IsValid());
        Assert.False((request with { LeaseToken = new string('!', 43) }).IsValid());
        Assert.False((request with { LeaseToken = new string('a', 42) + "+" }).IsValid());
    }

    [Fact]
    public void AcquireResultRequiresFenceOnlyForAcceptedLeases()
    {
        var accepted = new CharacterLeaseAcquireResultV1
        {
            CorrelationId = Guid.NewGuid(),
            Accepted = true,
            ReasonCode = "accepted",
            LeaseVersion = 1,
            ValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes(15)
        };

        Assert.True(accepted.IsValid());
        Assert.False((accepted with { LeaseVersion = null }).IsValid());
        Assert.False((accepted with { ValidUntilUtc = null }).IsValid());
        Assert.False((accepted with { ValidUntilUtc = DateTimeOffset.Now }).IsValid());
        Assert.True(new CharacterLeaseAcquireResultV1
        {
            CorrelationId = Guid.NewGuid(),
            Accepted = false,
            ReasonCode = "lease_owned"
        }.IsValid());
    }

    [Fact]
    public void ReleaseContractsRoundTripAndRequireExactLeaseIdentity()
    {
        var request = new CharacterLeaseReleaseRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            CharacterId = 73,
            SessionId = Guid.NewGuid(),
            LeaseToken = Convert.ToBase64String(new byte[32]).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
            LeaseVersion = 4
        };

        Assert.True(request.IsValid());
        var bytes = MessagePackSerializer.Serialize(request);
        Assert.Equal(request, MessagePackSerializer.Deserialize<CharacterLeaseReleaseRequestV1>(bytes));
        Assert.Equal(6, new MessagePackReader(bytes).ReadArrayHeader());
        Assert.False((request with { CharacterId = 0 }).IsValid());
        Assert.False((request with { SessionId = Guid.Empty }).IsValid());
        Assert.False((request with { LeaseVersion = 0 }).IsValid());
        Assert.False((request with { LeaseToken = "short" }).IsValid());
    }

    [Fact]
    public void ReleaseResultRequiresFenceOnlyForAcceptedRelease()
    {
        var accepted = new CharacterLeaseReleaseResultV1
        {
            CorrelationId = Guid.NewGuid(),
            Released = true,
            ReasonCode = "released",
            LeaseVersion = 4
        };

        Assert.True(accepted.IsValid());
        Assert.False((accepted with { LeaseVersion = null }).IsValid());
        Assert.True(new CharacterLeaseReleaseResultV1
        {
            CorrelationId = Guid.NewGuid(),
            Released = false,
            ReasonCode = "lease_mismatch"
        }.IsValid());
    }

    private static CharacterLeaseAcquireRequestV1 Request() => new()
    {
        CorrelationId = Guid.NewGuid(),
        JoinTicket = "signed-ticket",
        LeaseToken = Convert.ToBase64String(new byte[32]).TrimEnd('=').Replace('+', '-').Replace('/', '_'),
        CharacterId = 73
    };
}
