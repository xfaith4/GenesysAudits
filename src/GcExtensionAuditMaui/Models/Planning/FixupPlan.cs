namespace GcExtensionAuditMaui.Models.Planning;

public sealed class FixupPlan
{
    public required IReadOnlyList<FixupItem> Items { get; init; }
    public required IReadOnlyList<string> AvailableExtensionNumbers { get; init; }

    public string SummaryText { get; init; } = "";
}

