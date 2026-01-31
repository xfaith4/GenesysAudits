namespace GcExtensionAuditMaui.Models.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public LogLevel Level { get; init; }
    public required string Message { get; init; }
    public string? DataJson { get; init; }
}

