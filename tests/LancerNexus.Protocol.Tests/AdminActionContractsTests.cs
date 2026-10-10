using MessagePack;
using Xunit;

namespace LancerNexus.Protocol.Tests;

public sealed class AdminActionContractsTests
{
    [Fact]
    public void MessageActionRoundTripsWithStableFieldOrder()
    {
        var request = BaseRequest() with
        {
            Kind = AdminActionKind.Message,
            TargetKind = AdminActionTargetKind.All,
            Message = "hi"
        };

        Assert.True(request.IsValid());
        var bytes = MessagePackSerializer.Serialize(request);
        var decoded = MessagePackSerializer.Deserialize<AdminActionRequestV1>(bytes);
        Assert.True(decoded.IsValid());
        Assert.Equal(request, decoded);
        Assert.Equal("DC001001D92431303030303030302D303030302D303030302D303030302D303030303030303030303031D92432303030303030302D303030302D303030302D303030302D303030303030303030303032D92433303030303030302D303030302D303030302D303030302D303030303030303030303033A472657374AF61646D696E2D616374696F6E2D30310105C0C0C0C0A26869C0C0C0", Convert.ToHexString(bytes));

        var reader = new MessagePackReader(bytes);
        Assert.Equal(16, reader.ReadArrayHeader());
        Assert.Equal(1, reader.ReadInt32());
        reader.ReadRaw();
        reader.ReadRaw();
        reader.ReadRaw();
    }

    [Fact]
    public void SupportsClosedSetOfTypedMutations()
    {
        var requests = new[]
        {
            BaseRequest() with { Kind = AdminActionKind.Message, TargetKind = AdminActionTargetKind.Player,
                TargetId = "player-01", Message = "maintenance starts soon" },
            BaseRequest() with { Kind = AdminActionKind.Kick, TargetKind = AdminActionTargetKind.Player,
                TargetId = "player-01", Reason = "policy" },
            BaseRequest() with { Kind = AdminActionKind.Ban, TargetKind = AdminActionTargetKind.Player,
                TargetId = "player-01", Reason = "abuse", ConfirmationId = Guid.NewGuid() },
            BaseRequest() with { Kind = AdminActionKind.Unban, TargetKind = AdminActionTargetKind.Ban,
                TargetId = "ban-01" },
            BaseRequest() with { Kind = AdminActionKind.Drain, TargetKind = AdminActionTargetKind.Instance,
                TargetId = "li-01", Reason = "maintenance", ConfirmationId = Guid.NewGuid() },
            BaseRequest() with { Kind = AdminActionKind.Move, TargetKind = AdminActionTargetKind.Player,
                TargetId = "player-01", TargetInstanceId = "li-02" },
            BaseRequest() with { Kind = AdminActionKind.Teleport, TargetKind = AdminActionTargetKind.Player,
                TargetId = "player-01", TargetSystemId = "li02", TargetBaseId = "li02_01_base" },
            BaseRequest() with { Kind = AdminActionKind.Transfer, TargetKind = AdminActionTargetKind.Player,
                TargetId = "player-01", TargetInstanceId = "li-02" }
        };

        Assert.All(requests, request => Assert.True(request.IsValid(), request.Kind.ToString()));
        Assert.All(requests, request => Assert.True(
            MessagePackSerializer.Deserialize<AdminActionRequestV1>(MessagePackSerializer.Serialize(request)).IsValid()));
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void RejectsInvalidMutationShape(AdminActionRequestV1 request) => Assert.False(request.IsValid());

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return [BaseRequest() with { SchemaVersion = 2, Kind = AdminActionKind.Kick,
            TargetKind = AdminActionTargetKind.Player, TargetId = "player-01" }];
        yield return [BaseRequest() with { Kind = (AdminActionKind)99, TargetKind = AdminActionTargetKind.Player,
            TargetId = "player-01" }];
        yield return [BaseRequest() with { Kind = AdminActionKind.Kick, TargetKind = AdminActionTargetKind.Player,
            TargetId = "../other" }];
        yield return [BaseRequest() with { Source = "rest-admin" }];
        yield return [BaseRequest() with { IdempotencyKey = "contains spaces" }];
        yield return [BaseRequest() with { Kind = AdminActionKind.Message, TargetKind = AdminActionTargetKind.All,
            TargetId = "all", Message = "hi" }];
        yield return [BaseRequest() with { Kind = AdminActionKind.Message, TargetKind = AdminActionTargetKind.Player,
            TargetId = "player-01", Message = "line\nfeed" }];
        yield return [BaseRequest() with { Kind = AdminActionKind.Ban, TargetKind = AdminActionTargetKind.Account,
            TargetId = "account-01", Reason = "permanent ban" }];
        yield return [BaseRequest() with { Kind = AdminActionKind.Ban, TargetKind = AdminActionTargetKind.Player,
            TargetId = "player-01", Reason = "timed ban", ExpiresUtc = new DateTimeOffset(2030, 1, 1, 0, 0, 0,
                TimeSpan.FromHours(1)) }];
        yield return [BaseRequest() with { Kind = AdminActionKind.Drain, TargetKind = AdminActionTargetKind.Instance,
            TargetId = "li-01", Reason = "maintenance" }];
        yield return [BaseRequest() with { Kind = AdminActionKind.Move, TargetKind = AdminActionTargetKind.Player,
            TargetId = "player-01" }];
        yield return [BaseRequest() with { Kind = AdminActionKind.Teleport, TargetKind = AdminActionTargetKind.Player,
            TargetId = "player-01", TargetSystemId = "li02", TargetInstanceId = "li-02" }];
    }

    [Theory]
    [InlineData(AdminActionStatus.Pending, null, true)]
    [InlineData(AdminActionStatus.Completed, null, true)]
    [InlineData(AdminActionStatus.Denied, "permission_denied", true)]
    [InlineData(AdminActionStatus.Conflict, null, false)]
    [InlineData((AdminActionStatus)99, null, false)]
    public void ValidatesCorrelatedActionResults(AdminActionStatus status, string? errorCode, bool valid)
    {
        var result = new AdminActionResultV1
        {
            CommandId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            Status = status,
            ErrorCode = errorCode,
            Sequence = 0
        };

        Assert.Equal(valid, result.IsValid());
    }

    private static AdminActionRequestV1 BaseRequest() => new()
    {
        CommandId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
        CorrelationId = Guid.Parse("20000000-0000-0000-0000-000000000002"),
        ActorAccountId = Guid.Parse("30000000-0000-0000-0000-000000000003"),
        Source = "rest",
        IdempotencyKey = "admin-action-01"
    };
}
