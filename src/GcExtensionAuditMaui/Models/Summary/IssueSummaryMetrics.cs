namespace GcExtensionAuditMaui.Models.Summary;

/// <summary>
/// High-level metrics for the issue summary page.
/// </summary>
public sealed class IssueSummaryMetrics
{
    public int TotalIssues { get; init; }
    public int ResolvedIssues { get; init; }
    public int UnresolvedIssues { get; init; }
    public double ResolvedPercentage { get; init; }
    public double UnresolvedPercentage { get; init; }
    
    // Note: Average time to resolution would require tracking timestamps
    // which the current data model doesn't support. This can be added later.
    public string AverageTimeToResolution { get; init; } = "N/A";
}
