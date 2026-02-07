namespace GcExtensionAuditMaui.Models.AuditLogs;

/// <summary>
/// State container for audit log query results
/// </summary>
public class AuditLogState
{
    public AuditLogQueryRequest? QueryRequest { get; set; }
    public string? TransactionId { get; set; }
    public AuditTransactionStatusResponse? TransactionStatus { get; set; }
    public List<AuditLogEntity> RawEntities { get; set; } = new();
    public ServiceMappingResponse? ServiceMapping { get; set; }
    public DateTime QueryExecutedAt { get; set; }
    public int TotalPages { get; set; }
    public long? TotalResults { get; set; }

    /// <summary>
    /// Executive summary aggregates
    /// </summary>
    public AuditLogSummary GetSummary()
    {
        var summary = new AuditLogSummary
        {
            TotalEvents = RawEntities.Count,
            TopActions = RawEntities
                .Where(e => !string.IsNullOrEmpty(e.Action))
                .GroupBy(e => e.Action!)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new CountAggregate { Name = g.Key, Count = g.Count() })
                .ToList(),
            TopEntityTypes = RawEntities
                .Where(e => !string.IsNullOrEmpty(e.EntityType))
                .GroupBy(e => e.EntityType!)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new CountAggregate { Name = g.Key, Count = g.Count() })
                .ToList(),
            TopActors = RawEntities
                .Where(e => e.User != null && !string.IsNullOrEmpty(e.User.Display ?? e.User.Name ?? e.User.Email))
                .GroupBy(e => e.User!.Display ?? e.User!.Name ?? e.User!.Email ?? "Unknown")
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new CountAggregate { Name = g.Key, Count = g.Count() })
                .ToList()
        };

        return summary;
    }
}

public class AuditLogSummary
{
    public int TotalEvents { get; set; }
    public List<CountAggregate> TopActions { get; set; } = new();
    public List<CountAggregate> TopEntityTypes { get; set; } = new();
    public List<CountAggregate> TopActors { get; set; } = new();
}

public class CountAggregate
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
}
