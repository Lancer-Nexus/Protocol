using LancerNexus.Protocol;
using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class CharacterIdentityContractsTests
{
    private static readonly MessagePackSerializerOptions Untrusted = MessagePackSerializerOptions.Standard
        .WithSecurity(MessagePackSecurity.UntrustedData);

    [Fact]
    public void ResolveContractsValidateAndRoundTripWithStableKeys()
    {
        var request = new CharacterIdentityResolveRequestV1
        {
            CorrelationId = Guid.NewGuid(), AccountId = Guid.NewGuid(), SessionId = Guid.NewGuid(),
            DisplayName = "Pilot One"
        };
        var result = new CharacterIdentityResolveResultV1
        {
            CorrelationId = request.CorrelationId, Accepted = true, ReasonCode = "resolved", CharacterId = 918,
            LeaseTicket = "signed-ticket"
        };

        Assert.True(request.IsValid());
        Assert.True(result.IsValid());
        Assert.Equal(request, MessagePackSerializer.Deserialize<CharacterIdentityResolveRequestV1>(
            MessagePackSerializer.Serialize(request), Untrusted));
        Assert.Equal(result, MessagePackSerializer.Deserialize<CharacterIdentityResolveResultV1>(
            MessagePackSerializer.Serialize(result), Untrusted));
        Assert.False((request with { DisplayName = "  Pilot One " }).IsValid());
        Assert.False((request with { DisplayName = new string('x', 97) }).IsValid());
        Assert.False((result with { CharacterId = null }).IsValid());
    }
}
