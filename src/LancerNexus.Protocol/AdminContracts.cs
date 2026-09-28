using MessagePack;

namespace LancerNexus.Protocol;

public enum AdminQueryKind { Help = 0, Status = 1, Instances = 2, Instance = 3 }

/// <summary>Read-only, bounded commands. No executable chat text crosses the service boundary.</summary>
[MessagePackObject]
public sealed class AdminQuery
{
    [Key(0)] public Guid CorrelationId { get; init; }
    [Key(1)] public string IdempotencyKey { get; init; } = "";
    [Key(2)] public AdminQueryKind Kind { get; init; }
    [Key(3)] public string? TargetInstanceId { get; init; }

    public bool IsValid() => CorrelationId != Guid.Empty && IdempotencyKey is { Length: > 0 and <= 96 } &&
        Enum.IsDefined(Kind) && (Kind == AdminQueryKind.Instance
            ? ValidIdentifier(TargetInstanceId) : TargetInstanceId is null);

    public static bool ValidIdentifier(string? value) => value is { Length: > 0 and <= 96 } &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    public static bool IsAdminChat(string text) => text.TrimStart().StartsWith("/admin", StringComparison.OrdinalIgnoreCase) &&
        (text.TrimStart().Length == 6 || char.IsWhiteSpace(text.TrimStart()[6]));

    public static AdminQuery? ParseChat(string text)
    {
        if (text.Length > 256 || !IsAdminChat(text)) return null;
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var kind = parts.Length == 1 ? "help" : parts[1].ToLowerInvariant();
        AdminQueryKind? parsed = kind switch
        {
            "help" => AdminQueryKind.Help, "status" => AdminQueryKind.Status,
            "instances" => AdminQueryKind.Instances, "instance" => AdminQueryKind.Instance, _ => null
        };
        if (parsed is null || (parsed == AdminQueryKind.Instance ? parts.Length != 3 : parts.Length > 2)) return null;
        var id = Guid.NewGuid();
        var query = new AdminQuery { CorrelationId = id, IdempotencyKey = id.ToString("N"),
            Kind = parsed.Value, TargetInstanceId = parsed == AdminQueryKind.Instance ? parts[2] : null };
        return query.IsValid() ? query : null;
    }
}

[MessagePackObject]
public sealed class GameAdminQueryRequest
{
    [Key(0)] public Guid AccountId { get; init; }
    [Key(1)] public Guid SessionId { get; init; }
    [Key(2)] public long CharacterId { get; init; }
    [Key(3)] public Guid TransferId { get; init; }
    [Key(4)] public AdminQuery Query { get; init; } = new();
}

/// <summary>Identity attested by Gateway after checking the active session and character lease.</summary>
[MessagePackObject]
public sealed class AuthorizedAdminQueryRequest
{
    [Key(0)] public Guid AccountId { get; init; }
    [Key(1)] public string Source { get; init; } = "";
    [Key(2)] public AdminQuery Query { get; init; } = new();
}

[MessagePackObject]
public sealed class AdminQueryResponse
{
    [Key(0)] public Guid CorrelationId { get; init; }
    [Key(1)] public string Status { get; init; } = "";
    [Key(2)] public string[] Lines { get; init; } = [];
}
