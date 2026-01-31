namespace GcExtensionAuditMaui.Models.Observability;

public sealed class RateLimitSnapshot
{
    public int? Limit { get; init; }
    public int? Remaining { get; init; }
    public DateTime? ResetUtc { get; init; }
    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;
}

