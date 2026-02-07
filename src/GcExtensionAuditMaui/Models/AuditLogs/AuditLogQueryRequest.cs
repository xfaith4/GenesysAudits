using System.Text.Json.Serialization;

namespace GcExtensionAuditMaui.Models.AuditLogs;

/// <summary>
/// Request model for audit log query builder
/// </summary>
public class AuditLogQueryRequest
{
    public DateTime IntervalStart { get; set; }
    public DateTime IntervalEnd { get; set; }
    public string? ServiceName { get; set; }
    public string? UserId { get; set; }
    public string? ClientId { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public bool ExpandUser { get; set; } = true;

    /// <summary>
    /// Formats the interval as ISO-8601 interval string: "{start}/{end}"
    /// </summary>
    public string GetIntervalString()
    {
        return $"{IntervalStart:O}/{IntervalEnd:O}";
    }
}

/// <summary>
/// API request payload for POST /api/v2/audits/query
/// </summary>
public class AuditQueryApiRequest
{
    [JsonPropertyName("interval")]
    public string Interval { get; set; } = "";

    [JsonPropertyName("serviceName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ServiceName { get; set; }

    [JsonPropertyName("filters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AuditQueryFilter>? Filters { get; set; }

    [JsonPropertyName("sort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<AuditQuerySort>? Sort { get; set; }
}

public class AuditQueryFilter
{
    [JsonPropertyName("property")]
    public string Property { get; set; } = "";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

public class AuditQuerySort
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("sortOrder")]
    public string SortOrder { get; set; } = "DESC";
}

/// <summary>
/// Request for realtime related audit query
/// </summary>
public class RealtimeRelatedQueryRequest
{
    [JsonPropertyName("auditId")]
    public string AuditId { get; set; } = "";

    [JsonPropertyName("trustorOrgId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TrustorOrgId { get; set; }
}
