using System.Text;
using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class CharacterSaveSnapshotContractsTests
{
    private static readonly MessagePackSerializerOptions Untrusted =
        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);
    private static readonly string LeaseToken = Convert.ToBase64String(new byte[32]).TrimEnd('=')
        .Replace('+', '-').Replace('/', '_');

    [Fact]
    public void SnapshotReadAndWriteContractsRoundTrip()
    {
        var payload = Encoding.UTF8.GetBytes("[Player]\nname = Pilot\n");
        var write = new CharacterSaveSnapshotWriteRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            CharacterId = 42,
            LeaseToken = LeaseToken,
            LeaseVersion = 3,
            Revision = 8,
            Payload = payload
        };
        var read = new CharacterSaveSnapshotReadRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            CharacterId = write.CharacterId,
            LeaseToken = write.LeaseToken,
            LeaseVersion = write.LeaseVersion
        };
        var snapshot = new CharacterSaveSnapshotV1
        {
            CorrelationId = read.CorrelationId,
            CharacterId = read.CharacterId,
            LeaseVersion = read.LeaseVersion,
            Revision = write.Revision,
            Payload = payload
        };
        var result = new CharacterSaveSnapshotWriteResultV1
        {
            CorrelationId = write.CorrelationId,
            Accepted = true,
            ReasonCode = "stored",
            Revision = write.Revision
        };

        Assert.True(write.IsValid());
        Assert.True(read.IsValid());
        Assert.True(snapshot.IsValid());
        Assert.True(result.IsValid());
        var decodedWrite = MessagePackSerializer.Deserialize<CharacterSaveSnapshotWriteRequestV1>(
            MessagePackSerializer.Serialize(write), Untrusted);
        Assert.Equal(write.CorrelationId, decodedWrite.CorrelationId);
        Assert.Equal(write.CharacterId, decodedWrite.CharacterId);
        Assert.Equal(write.LeaseVersion, decodedWrite.LeaseVersion);
        Assert.Equal(write.Revision, decodedWrite.Revision);
        Assert.Equal(write.Payload, decodedWrite.Payload);
        Assert.Equal(read, MessagePackSerializer.Deserialize<CharacterSaveSnapshotReadRequestV1>(
            MessagePackSerializer.Serialize(read), Untrusted));
        var decodedSnapshot = MessagePackSerializer.Deserialize<CharacterSaveSnapshotV1>(
            MessagePackSerializer.Serialize(snapshot), Untrusted);
        Assert.Equal(snapshot.CharacterId, decodedSnapshot.CharacterId);
        Assert.Equal(snapshot.LeaseVersion, decodedSnapshot.LeaseVersion);
        Assert.Equal(snapshot.Revision, decodedSnapshot.Revision);
        Assert.Equal(snapshot.Payload, decodedSnapshot.Payload);
        Assert.Equal(result, MessagePackSerializer.Deserialize<CharacterSaveSnapshotWriteResultV1>(
            MessagePackSerializer.Serialize(result), Untrusted));
    }

    [Fact]
    public void SnapshotContractsRejectMissingFenceAndEmptyPayload()
    {
        var write = new CharacterSaveSnapshotWriteRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            CharacterId = 42,
            LeaseToken = LeaseToken,
            LeaseVersion = 3,
            Revision = 1,
            Payload = [1]
        };

        Assert.False((write with { LeaseVersion = 0 }).IsValid());
        Assert.False((write with { Revision = 0 }).IsValid());
        Assert.False((write with { LeaseToken = "" }).IsValid());
        Assert.False((write with { Payload = [] }).IsValid());
        Assert.False((new CharacterSaveSnapshotReadRequestV1
        {
            CorrelationId = Guid.NewGuid(),
            CharacterId = 42,
            LeaseToken = LeaseToken,
            LeaseVersion = 0
        }).IsValid());
        Assert.False(new CharacterSaveSnapshotWriteResultV1
        {
            CorrelationId = Guid.NewGuid(),
            Accepted = true,
            ReasonCode = "stored"
        }.IsValid());
    }
}
