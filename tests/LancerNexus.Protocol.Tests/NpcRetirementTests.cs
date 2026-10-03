using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class NpcRetirementTests
{
    private static NpcRetirementRequestV1 Request(long version = 7) => new()
    {
        RequestId = Guid.NewGuid(), InstanceId = "source",
        Npcs = [new() { NpcId = Guid.NewGuid(), OwnershipVersion = version, Reason = NpcRetirementReasonV1.Destroyed }]
    };

    [Fact]
    public void ExplicitKeyContractsRoundTrip()
    {
        var request = Request();
        Assert.True(request.IsValid());
        var bytes = MessagePackSerializer.Serialize(request);
        Assert.Equal(3, new MessagePackReader(bytes).ReadArrayHeader());
        var restored = MessagePackSerializer.Deserialize<NpcRetirementRequestV1>(bytes);
        Assert.Equal(request.RequestId, restored.RequestId);
        Assert.Equal(request.Npcs, restored.Npcs);
        var response = new NpcRetirementResponseV1 { RequestId = request.RequestId,
            Npcs = [new() { NpcId = request.Npcs[0].NpcId, Accepted = true, OwnershipVersion = 8, ReasonCode = "retired" }] };
        Assert.Equal(response.Npcs, MessagePackSerializer.Deserialize<NpcRetirementResponseV1>(
            MessagePackSerializer.Serialize(response)).Npcs);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MaxValue)]
    public void InvalidFencesAreRejected(long version) => Assert.False(Request(version).IsValid());

    [Fact]
    public void DuplicateIdsAndUnknownReasonsAreRejected()
    {
        var request = Request();
        Assert.False((request with { Npcs = [request.Npcs[0], request.Npcs[0]] }).IsValid());
        Assert.False((request with { Npcs = [request.Npcs[0] with { Reason = (NpcRetirementReasonV1)99 }] }).IsValid());
        Assert.False((request with { Npcs = [] }).IsValid());
        Assert.False((request with { RequestId = Guid.Empty }).IsValid());
    }

    [Fact]
    public void PreviousLeaseDecodesAsActive()
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(4);
        writer.Write(Guid.NewGuid().ToString());
        writer.Write("source");
        writer.Write(7L);
        writer.WriteNil();
        writer.Flush();
        Assert.False(MessagePackSerializer.Deserialize<NpcOwnershipLease>(buffer.WrittenMemory).IsRetired);
        var lease = new NpcOwnershipLease { NpcId = Guid.NewGuid(), InstanceId = "source", OwnershipVersion = 8, IsRetired = true };
        Assert.True(MessagePackSerializer.Deserialize<NpcOwnershipLease>(MessagePackSerializer.Serialize(lease)).IsRetired);
    }
}
