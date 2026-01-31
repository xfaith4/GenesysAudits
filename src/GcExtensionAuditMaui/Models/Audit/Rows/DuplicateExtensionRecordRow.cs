namespace GcExtensionAuditMaui.Models.Audit;

public sealed class DuplicateExtensionRecordRow
{
    public required string ExtensionNumber { get; init; }
    public required string? ExtensionId { get; init; }
    public required string? OwnerType { get; init; }
    public required string? OwnerId { get; init; }
    public required string? ExtensionPoolId { get; init; }
}

