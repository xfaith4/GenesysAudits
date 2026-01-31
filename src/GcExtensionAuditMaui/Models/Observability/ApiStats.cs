using System.Collections.Concurrent;

namespace GcExtensionAuditMaui.Models.Observability;

public sealed class ApiStats
{
    private readonly object _gate = new();

    public long TotalCalls { get; private set; }
    public ConcurrentDictionary<string, long> ByMethod { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentDictionary<string, long> ByPath { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? LastError { get; private set; }
    public RateLimitSnapshot? RateLimit { get; private set; }
    public int? LastStatusCode { get; private set; }
    public string? LastRequestId { get; private set; }
    public string? LastCorrelationId { get; private set; }

    public void RecordCall(string method, string pathKey)
    {
        lock (_gate) { TotalCalls++; }
        ByMethod.AddOrUpdate(method, 1, static (_, prev) => prev + 1);
        ByPath.AddOrUpdate(pathKey, 1, static (_, prev) => prev + 1);
    }

    public void RecordError(string message)
    {
        lock (_gate) { LastError = message; }
    }

    public void RecordRateLimit(RateLimitSnapshot snapshot)
    {
        lock (_gate) { RateLimit = snapshot; }
    }

    public void RecordLastResponse(int? statusCode, string? requestId, string? correlationId)
    {
        lock (_gate)
        {
            LastStatusCode = statusCode;
            if (!string.IsNullOrWhiteSpace(requestId)) { LastRequestId = requestId; }
            if (!string.IsNullOrWhiteSpace(correlationId)) { LastCorrelationId = correlationId; }
        }
    }

    public object ToSnapshotObject()
        => new
        {
            TotalCalls,
            ByMethod,
            ByPath,
            LastError,
            RateLimit,
            LastStatusCode,
            LastRequestId,
            LastCorrelationId,
        };
}
