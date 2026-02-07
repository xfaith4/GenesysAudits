using System.Text.Json.Serialization;

namespace GcExtensionAuditMaui.Models.AuditLogs;

/// <summary>
/// Response from POST /api/v2/audits/query
/// </summary>
public class AuditQueryTransactionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("dateStart")]
    public DateTime? DateStart { get; set; }

    [JsonPropertyName("interval")]
    public string? Interval { get; set; }

    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; set; }

    [JsonPropertyName("filters")]
    public List<AuditQueryFilter>? Filters { get; set; }
}

/// <summary>
/// Response from GET /api/v2/audits/query/{transactionId}
/// </summary>
public class AuditTransactionStatusResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("dateStart")]
    public DateTime? DateStart { get; set; }

    [JsonPropertyName("dateEnd")]
    public DateTime? DateEnd { get; set; }

    [JsonPropertyName("interval")]
    public string? Interval { get; set; }

    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; set; }

    [JsonPropertyName("filters")]
    public List<AuditQueryFilter>? Filters { get; set; }

    [JsonPropertyName("sort")]
    public List<AuditQuerySort>? Sort { get; set; }
}

/// <summary>
/// Response from GET /api/v2/audits/query/{transactionId}/results
/// </summary>
public class AuditQueryResultsResponse
{
    [JsonPropertyName("entities")]
    public List<AuditLogEntity> Entities { get; set; } = new();

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; }

    [JsonPropertyName("total")]
    public long? Total { get; set; }

    [JsonPropertyName("pageCount")]
    public int? PageCount { get; set; }

    [JsonPropertyName("cursor")]
    public string? Cursor { get; set; }
}

/// <summary>
/// Individual audit log entity
/// </summary>
public class AuditLogEntity
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("entityId")]
    public string? EntityId { get; set; }

    [JsonPropertyName("entityName")]
    public string? EntityName { get; set; }

    [JsonPropertyName("entityType")]
    public string? EntityType { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }

    [JsonPropertyName("user")]
    public AuditUser? User { get; set; }

    [JsonPropertyName("client")]
    public AuditClient? Client { get; set; }

    [JsonPropertyName("serviceName")]
    public string? ServiceName { get; set; }

    [JsonPropertyName("propertyChanges")]
    public List<PropertyChange>? PropertyChanges { get; set; }
}

public class AuditUser
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("display")]
    public string? Display { get; set; }
}

public class AuditClient
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class PropertyChange
{
    [JsonPropertyName("property")]
    public string? Property { get; set; }

    [JsonPropertyName("oldValue")]
    public string? OldValue { get; set; }

    [JsonPropertyName("newValue")]
    public string? NewValue { get; set; }
}

/// <summary>
/// Service mapping response
/// </summary>
public class ServiceMappingResponse
{
    [JsonPropertyName("entities")]
    public List<ServiceMappingEntity> Entities { get; set; } = new();
}

public class ServiceMappingEntity
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}
