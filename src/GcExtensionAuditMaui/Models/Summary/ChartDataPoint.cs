namespace GcExtensionAuditMaui.Models.Summary;

/// <summary>
/// Represents a data point for the bar chart visualization.
/// </summary>
public sealed class ChartDataPoint
{
    public required string Category { get; init; }
    public int Count { get; init; }
    public string Color { get; init; } = "#3B82F6"; // Default blue color
    public bool IsHighPriority { get; init; } = false;
    public double BarWidth { get; init; } = 0; // Width in pixels for the bar visualization
}
