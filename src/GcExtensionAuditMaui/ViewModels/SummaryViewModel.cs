using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcExtensionAuditMaui.Models.Audit;
using GcExtensionAuditMaui.Models.Summary;
using GcExtensionAuditMaui.Services;
using GcExtensionAuditMaui.Utilities;

namespace GcExtensionAuditMaui.ViewModels;

public sealed partial class SummaryViewModel : ObservableObject
{
    private readonly ContextStore _store;
    private readonly AuditService _audit;

    private DryRunReport? _report;

    public SummaryViewModel(ContextStore store, AuditService audit)
    {
        _store = store;
        _audit = audit;
    }

    public ObservableRangeCollection<PivotTableRow> PivotTableRows { get; } = new();
    public ObservableRangeCollection<ChartDataPoint> ChartDataPoints { get; } = new();

    private string _executiveOverview = "";
    public string ExecutiveOverview
    {
        get => _executiveOverview;
        set => SetProperty(ref _executiveOverview, value ?? "");
    }

    private string _keyMetrics = "";
    public string KeyMetrics
    {
        get => _keyMetrics;
        set => SetProperty(ref _keyMetrics, value ?? "");
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                GenerateSummaryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateSummaryAsync()
    {
        if (_store.Context is null)
        {
            ExecutiveOverview = "Context not built. Go to Home and click Build Context.";
            return;
        }

        IsBusy = true;
        try
        {
            // Generate the dry run report
            _report = await Task.Run(() => _audit.NewDryRunReport(_store.Context));

            // Calculate metrics
            var metrics = CalculateMetrics(_report);
            
            // Generate executive overview
            ExecutiveOverview = GenerateExecutiveOverview(_report, metrics);
            
            // Generate key metrics display
            KeyMetrics = GenerateKeyMetrics(metrics);

            // Build pivot table data
            var pivotData = BuildPivotTable(_report);
            PivotTableRows.ReplaceRange(pivotData);

            // Build chart data (sorted by count descending)
            var chartData = BuildChartData(pivotData);
            ChartDataPoints.ReplaceRange(chartData);
        }
        finally { IsBusy = false; }
    }

    private bool CanGenerate() => !IsBusy;

    private IssueSummaryMetrics CalculateMetrics(DryRunReport report)
    {
        var total = report.Summary.MissingAssignments 
                    + report.Summary.Discrepancies 
                    + report.Summary.DuplicateUserRows;

        // For this implementation, we consider all issues as "open" since we don't have
        // a resolution tracking system yet. In future, this could be enhanced with
        // patch execution tracking.
        var unresolved = total;
        var resolved = 0;

        return new IssueSummaryMetrics
        {
            TotalIssues = total,
            ResolvedIssues = resolved,
            UnresolvedIssues = unresolved,
            ResolvedPercentage = total > 0 ? (resolved * 100.0 / total) : 0,
            UnresolvedPercentage = total > 0 ? (unresolved * 100.0 / total) : 100,
            AverageTimeToResolution = "N/A" // Not tracked yet
        };
    }

    private string GenerateExecutiveOverview(DryRunReport report, IssueSummaryMetrics metrics)
    {
        var total = metrics.TotalIssues;
        if (total == 0)
        {
            return "No issues detected. All user extensions are properly assigned and synchronized.";
        }

        // Identify the most prevalent issue type
        var categories = new[]
        {
            ("Missing Assignments", report.Summary.MissingAssignments),
            ("Discrepancies", report.Summary.Discrepancies),
            ("Duplicate User Assignments", report.Summary.DuplicateUserRows)
        };

        var topIssue = categories.OrderByDescending(c => c.Item2).First();
        var topPercentage = total > 0 ? (topIssue.Item2 * 100.0 / total) : 0;

        return $"Total {total} issue{(total == 1 ? "" : "s")} detected in the extension audit. " +
               $"{topIssue.Item2} ({topPercentage:F0}%) related to {topIssue.Item1}. " +
               $"{metrics.UnresolvedIssues} issue{(metrics.UnresolvedIssues == 1 ? "" : "s")} require{(metrics.UnresolvedIssues == 1 ? "s" : "")} attention.";
    }

    private string GenerateKeyMetrics(IssueSummaryMetrics metrics)
    {
        return $"Total Issues: {metrics.TotalIssues}\n" +
               $"Resolved: {metrics.ResolvedIssues} ({metrics.ResolvedPercentage:F1}%)\n" +
               $"Unresolved: {metrics.UnresolvedIssues} ({metrics.UnresolvedPercentage:F1}%)\n" +
               $"Avg Time to Resolution: {metrics.AverageTimeToResolution}";
    }

    private List<PivotTableRow> BuildPivotTable(DryRunReport report)
    {
        var rows = new List<PivotTableRow>();

        // For now, all issues are considered "open" since we don't track resolution status
        // In future, this could be enhanced by tracking which issues have been patched

        if (report.Summary.MissingAssignments > 0)
        {
            rows.Add(new PivotTableRow
            {
                Category = "Missing Assignments",
                OpenCount = report.Summary.MissingAssignments,
                ClosedCount = 0,
                TotalCount = report.Summary.MissingAssignments,
                Severity = "High"
            });
        }

        if (report.Summary.Discrepancies > 0)
        {
            rows.Add(new PivotTableRow
            {
                Category = "Discrepancies",
                OpenCount = report.Summary.Discrepancies,
                ClosedCount = 0,
                TotalCount = report.Summary.Discrepancies,
                Severity = "Medium"
            });
        }

        if (report.Summary.DuplicateUserRows > 0)
        {
            rows.Add(new PivotTableRow
            {
                Category = "Duplicate User Assignments",
                OpenCount = report.Summary.DuplicateUserRows,
                ClosedCount = 0,
                TotalCount = report.Summary.DuplicateUserRows,
                Severity = "High"
            });
        }

        if (report.Summary.DuplicateExtensionRows > 0)
        {
            rows.Add(new PivotTableRow
            {
                Category = "Duplicate Extension Records",
                OpenCount = report.Summary.DuplicateExtensionRows,
                ClosedCount = 0,
                TotalCount = report.Summary.DuplicateExtensionRows,
                Severity = "Low"
            });
        }

        return rows;
    }

    private List<ChartDataPoint> BuildChartData(List<PivotTableRow> pivotData)
    {
        // Sort by count descending to highlight most critical issues
        var sorted = pivotData
            .OrderByDescending(r => r.TotalCount)
            .ToList();

        if (sorted.Count == 0)
            return new List<ChartDataPoint>();

        // Find max count for scaling
        var maxCount = sorted.Max(r => r.TotalCount);
        const double maxBarWidth = 400.0; // Maximum bar width in pixels

        var chartData = new List<ChartDataPoint>();
        
        for (int i = 0; i < sorted.Count; i++)
        {
            var row = sorted[i];
            var isTopIssue = i < 2; // Highlight top 2 issues with different color
            
            // Calculate bar width proportionally
            var barWidth = maxCount > 0 ? (row.TotalCount / (double)maxCount) * maxBarWidth : 0;
            
            chartData.Add(new ChartDataPoint
            {
                Category = row.Category,
                Count = row.TotalCount,
                Color = isTopIssue ? "#EF4444" : "#3B82F6", // Red for top issues, blue for others
                IsHighPriority = isTopIssue,
                BarWidth = Math.Max(barWidth, 20) // Minimum width of 20 for visibility
            });
        }

        return chartData;
    }
}
