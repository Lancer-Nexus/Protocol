using LancerNexus.Protocol;
using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class CharacterLeaseRenewalContractsTests
{
    private static readonly MessagePackSerializerOptions Untrusted = MessagePackSerializerOptions.Standard
        .WithSecurity(MessagePackSecurity.UntrustedData);

    [Fact]
    public void RenewalContractsValidateFenceAndRoundTrip()
    {
        var bytes = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var request = new CharacterLeaseRenewRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            CharacterId = 99,
            LeaseToken = token,
            LeaseVersion = 4
        };
        var result = new CharacterLeaseRenewResultV1
        {
            CorrelationId = request.CorrelationId,
            Accepted = true,
            ReasonCode = "renewed",
            LeaseVersion = 4,
            ValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes(15)
        };

        Assert.True(request.IsValid());
        Assert.True(result.IsValid());
        Assert.Equal(request, MessagePackSerializer.Deserialize<CharacterLeaseRenewRequestV1>(
            MessagePackSerializer.Serialize(request), Untrusted));
        Assert.Equal(result, MessagePackSerializer.Deserialize<CharacterLeaseRenewResultV1>(
            MessagePackSerializer.Serialize(result), Untrusted));
        Assert.False((request with { LeaseVersion = 0 }).IsValid());
        Assert.False((request with { LeaseToken = "bad" }).IsValid());
        Assert.False((result with { LeaseVersion = null }).IsValid());
    }
}
