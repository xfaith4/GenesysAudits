namespace GcExtensionAuditMaui.Models.Audit;

public sealed class DiscrepancyRow
{
    public required string Issue { get; init; }
    public required string ProfileExtension { get; init; }
    public required string UserId { get; init; }
    public required string? UserName { get; init; }
    public required string? UserEmail { get; init; }
    public required string? ExtensionId { get; init; }
    public required string? ExtensionOwnerType { get; init; }
    public required string? ExtensionOwnerId { get; init; }
}

