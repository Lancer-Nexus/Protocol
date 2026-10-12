using LancerNexus.Protocol;
using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class CharacterRosterContractsTests
{
    private static readonly MessagePackSerializerOptions Untrusted = MessagePackSerializerOptions.Standard
        .WithSecurity(MessagePackSecurity.UntrustedData);

    [Fact]
    public void RosterContractsValidateAndRoundTripWithStableKeys()
    {
        var request = new CharacterRosterRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            SessionId = Guid.NewGuid()
        };
        var result = new CharacterRosterResultV1
        {
            CorrelationId = request.CorrelationId,
            Accepted = true,
            ReasonCode = "listed",
            Characters = [new CharacterRosterEntryV1 { CharacterId = 42, DisplayName = "Pilot One", SystemId = "li03" }]
        };

        Assert.True(request.IsValid());
        Assert.True(result.IsValid());
        Assert.Equal(request, MessagePackSerializer.Deserialize<CharacterRosterRequestV1>(
            MessagePackSerializer.Serialize(request), Untrusted));
        var decoded = MessagePackSerializer.Deserialize<CharacterRosterResultV1>(
            MessagePackSerializer.Serialize(result), Untrusted);
        Assert.Equal(result.SchemaVersion, decoded.SchemaVersion);
        Assert.Equal(result.CorrelationId, decoded.CorrelationId);
        Assert.Equal(result.Accepted, decoded.Accepted);
        Assert.Equal(result.ReasonCode, decoded.ReasonCode);
        Assert.Equal(result.Characters, decoded.Characters);
        Assert.False((result with { Characters = [result.Characters[0], result.Characters[0]] }).IsValid());
        Assert.False((result with { Characters = [result.Characters[0] with { CharacterId = 0 }] }).IsValid());
        Assert.False((result with { Characters = [result.Characters[0] with { SystemId = "bad system" }] }).IsValid());
        Assert.False((request with { SessionId = Guid.Empty }).IsValid());
    }

    [Fact]
    public void DeletionContractsRoundTripAndRejectMalformedOrUnrecognizedResults()
    {
        var request = new CharacterDeletionRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            CharacterId = 12
        };
        var result = new CharacterDeletionResultV1
        {
            CorrelationId = request.CorrelationId,
            Accepted = true,
            ReasonCode = "deleted"
        };

        Assert.True(request.IsValid());
        Assert.True(result.IsValid());
        Assert.Equal(request, MessagePackSerializer.Deserialize<CharacterDeletionRequestV1>(
            MessagePackSerializer.Serialize(request), Untrusted));
        Assert.Equal(result, MessagePackSerializer.Deserialize<CharacterDeletionResultV1>(
            MessagePackSerializer.Serialize(result), Untrusted));
        Assert.False((request with { CharacterId = 0 }).IsValid());
        Assert.False((result with { Accepted = false, ReasonCode = "deleted" }).IsValid());
        Assert.False((result with { Accepted = false, ReasonCode = "unknown" }).IsValid());
    }

    [Fact]
    public void SystemMetadataRecoveryContractsRoundTripAndRejectInvalidResults()
    {
        var request = new CharacterSystemMetadataRecoveryRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            CharacterId = 314
        };
        var response = new CharacterSystemMetadataRecoveryResultV1
        {
            CorrelationId = request.CorrelationId,
            CharacterId = request.CharacterId,
            Accepted = true,
            ReasonCode = "recovered",
            SystemId = "li03"
        };

        Assert.True(request.IsValid());
        Assert.True(response.IsValid());
        Assert.Equal(request, MessagePackSerializer.Deserialize<CharacterSystemMetadataRecoveryRequestV1>(
            MessagePackSerializer.Serialize(request), Untrusted));
        Assert.Equal(response, MessagePackSerializer.Deserialize<CharacterSystemMetadataRecoveryResultV1>(
            MessagePackSerializer.Serialize(response), Untrusted));
        Assert.True((response with { ReasonCode = "already_known" }).IsValid());
        Assert.False((response with { Accepted = false, ReasonCode = "recovered" }).IsValid());
        Assert.False((response with { SystemId = null }).IsValid());
        Assert.False((response with { ReasonCode = "unknown" }).IsValid());
        Assert.False((request with { CharacterId = 0 }).IsValid());
    }

    [Fact]
    public void BootstrapAndPreviewContractsCarryBoundedSaveGameAndAccountIdentity()
    {
        var bootstrap = new CharacterBootstrapRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            CharacterId = 73,
            Payload = [1, 2, 3],
            SystemId = "li03"
        };
        var result = new CharacterBootstrapResultV1
        {
            CorrelationId = bootstrap.CorrelationId,
            Accepted = true,
            ReasonCode = "stored"
        };
        var preview = new CharacterSnapshotPreviewRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            AccountId = bootstrap.AccountId,
            SessionId = bootstrap.SessionId,
            CharacterId = bootstrap.CharacterId
        };

        Assert.True(bootstrap.IsValid());
        Assert.True(result.IsValid());
        Assert.True(preview.IsValid());
        var decodedBootstrap = MessagePackSerializer.Deserialize<CharacterBootstrapRequestV1>(
            MessagePackSerializer.Serialize(bootstrap), Untrusted);
        Assert.Equal(bootstrap.SchemaVersion, decodedBootstrap.SchemaVersion);
        Assert.Equal(bootstrap.CorrelationId, decodedBootstrap.CorrelationId);
        Assert.Equal(bootstrap.AccountId, decodedBootstrap.AccountId);
        Assert.Equal(bootstrap.SessionId, decodedBootstrap.SessionId);
        Assert.Equal(bootstrap.CharacterId, decodedBootstrap.CharacterId);
        Assert.Equal(bootstrap.Payload, decodedBootstrap.Payload);
        Assert.Equal(bootstrap.SystemId, decodedBootstrap.SystemId);
        Assert.Equal(result, MessagePackSerializer.Deserialize<CharacterBootstrapResultV1>(
            MessagePackSerializer.Serialize(result), Untrusted));
        Assert.Equal(preview, MessagePackSerializer.Deserialize<CharacterSnapshotPreviewRequestV1>(
            MessagePackSerializer.Serialize(preview), Untrusted));
        Assert.False((bootstrap with { Payload = [] }).IsValid());
        Assert.False((preview with { AccountId = Guid.Empty }).IsValid());
    }
}
