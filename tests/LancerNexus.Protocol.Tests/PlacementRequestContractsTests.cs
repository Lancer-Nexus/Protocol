using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class PlacementRequestContractsTests
{
    [Fact]
    public void ValidRequestAllowsOptionalMetadataAndFieldsAtTheirLimits()
    {
        var request = Request(
            characterId: null,
            targetSystem: new string('s', PlacementRequestValidator.MaximumSystemIdLength),
            groupId: new string('g', PlacementRequestValidator.MaximumGroupIdLength),
            eventId: new string('e', PlacementRequestValidator.MaximumEventIdLength),
            clientBuild: new string('b', PlacementRequestValidator.MaximumClientBuildLength),
            region: new string('r', PlacementRequestValidator.MaximumRegionLength),
            idempotencyKey: new string('i', PlacementRequestValidator.MaximumIdempotencyKeyLength));

        Assert.True(request.IsValid());
        Assert.True(PlacementRequestValidator.IsValid(request));
    }

    [Fact]
    public void ValidationMethodDoesNotChangeNineFieldMessagePackContract()
    {
        var request = Request();
        var bytes = MessagePackSerializer.Serialize(request);
        var copy = MessagePackSerializer.Deserialize<PlacementRequest>(bytes);

        Assert.Equal(9, new MessagePackReader(bytes).ReadArrayHeader());
        Assert.Equal(request.RequestId, copy.RequestId);
        Assert.Equal(request.SessionId, copy.SessionId);
        Assert.Equal(request.IdempotencyKey, copy.IdempotencyKey);
        Assert.True(copy.IsValid());
    }

    [Fact]
    public void RejectsMissingRequiredIdentityOrTargetFields()
    {
        Assert.False(Request(requestId: Guid.Empty).IsValid());
        Assert.False(Request(sessionId: Guid.Empty).IsValid());
        Assert.False(Request(characterId: 0).IsValid());
        Assert.False(Request(characterId: -1).IsValid());
        Assert.False(Request(targetSystem: " ").IsValid());
        Assert.False(Request(idempotencyKey: " ").IsValid());
        Assert.False(PlacementRequestValidator.IsValid(null));
    }

    [Fact]
    public void RejectsOversizedPlacementFields()
    {
        Assert.False(Request(targetSystem: TooLong(PlacementRequestValidator.MaximumSystemIdLength)).IsValid());
        Assert.False(Request(groupId: TooLong(PlacementRequestValidator.MaximumGroupIdLength)).IsValid());
        Assert.False(Request(eventId: TooLong(PlacementRequestValidator.MaximumEventIdLength)).IsValid());
        Assert.False(Request(clientBuild: TooLong(PlacementRequestValidator.MaximumClientBuildLength)).IsValid());
        Assert.False(Request(region: TooLong(PlacementRequestValidator.MaximumRegionLength)).IsValid());
        Assert.False(Request(idempotencyKey: TooLong(PlacementRequestValidator.MaximumIdempotencyKeyLength)).IsValid());
    }

    private static PlacementRequest Request(
        Guid? requestId = null,
        Guid? sessionId = null,
        long? characterId = 42,
        string? targetSystem = "li01",
        string? groupId = null,
        string? eventId = null,
        string? clientBuild = "1.0.0",
        string? region = "eu-west",
        string? idempotencyKey = "placement-session-42") => new()
        {
            RequestId = requestId ?? Guid.NewGuid(),
            SessionId = sessionId ?? Guid.NewGuid(),
            CharacterId = characterId,
            TargetSystem = targetSystem!,
            GroupId = groupId,
            EventId = eventId,
            ClientBuild = clientBuild!,
            Region = region!,
            IdempotencyKey = idempotencyKey!
        };

    private static string TooLong(int maximumLength) => new('x', maximumLength + 1);
}
