namespace GcExtensionAuditMaui.Models.AuditLogs;

/// <summary>
/// Display row model for audit log results
/// </summary>
public class AuditLogRow
{
    public string? Id { get; set; }
    public DateTime? Timestamp { get; set; }
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? EntityName { get; set; }
    public string? ServiceName { get; set; }
    public string? UserDisplay { get; set; }
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? ClientId { get; set; }
    public string? ClientName { get; set; }
    public int PropertyChangesCount { get; set; }

    /// <summary>
    /// Creates a display row from an API entity
    /// </summary>
    public static AuditLogRow FromEntity(AuditLogEntity entity)
    {
        return new AuditLogRow
        {
            Id = entity.Id,
            Timestamp = entity.Timestamp,
            Action = entity.Action,
            EntityType = entity.EntityType,
            EntityId = entity.EntityId,
            EntityName = entity.EntityName,
            ServiceName = entity.ServiceName,
            UserDisplay = entity.User?.Display ?? entity.User?.Name ?? entity.User?.Email,
            UserId = entity.User?.Id,
            UserEmail = entity.User?.Email,
            ClientId = entity.Client?.Id,
            ClientName = entity.Client?.Name,
            PropertyChangesCount = entity.PropertyChanges?.Count ?? 0
        };
    }

    public string TimestampFormatted => Timestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
}
