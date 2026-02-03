namespace GcExtensionAuditMaui.Models.Summary;

/// <summary>
/// Represents a single row in the pivot table showing issue breakdown by category.
/// </summary>
public sealed class PivotTableRow
{
    public required string Category { get; init; }
    public int OpenCount { get; init; }
    public int ClosedCount { get; init; }
    public int TotalCount { get; init; }
    public string Severity { get; init; } = "Medium";
}
