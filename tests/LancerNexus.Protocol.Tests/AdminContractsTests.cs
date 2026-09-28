using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public class AdminContractsTests
{
    [Theory]
    [InlineData("/admin", AdminQueryKind.Help)]
    [InlineData(" /ADMIN status ", AdminQueryKind.Status)]
    [InlineData("/admin instances", AdminQueryKind.Instances)]
    [InlineData("/admin instance mixed-01", AdminQueryKind.Instance)]
    public void ParsesOnlyClosedCommands(string text, AdminQueryKind kind)
    {
        var query = Assert.IsType<AdminQuery>(AdminQuery.ParseChat(text));
        Assert.Equal(kind, query.Kind);
        var decoded = MessagePackSerializer.Deserialize<AdminQuery>(MessagePackSerializer.Serialize(query));
        Assert.True(decoded.IsValid());
        Assert.Equal(query.CorrelationId, decoded.CorrelationId);
    }

    [Theory]
    [InlineData("/admin kick Test")]
    [InlineData("/admin status secret")]
    [InlineData("/admin instance ../../etc")]
    [InlineData("/administrator status")]
    public void RejectsMalformedAndUnsupportedCommands(string text) => Assert.Null(AdminQuery.ParseChat(text));

    [Fact]
    public void UnknownEnumIsInvalid() => Assert.False(new AdminQuery
    { CorrelationId = Guid.NewGuid(), IdempotencyKey = "key", Kind = (AdminQueryKind)200 }.IsValid());
}
