using System.Text.Json;
using GcExtensionAuditMaui.Models.Audit;
using GcExtensionAuditMaui.Models.Observability;

namespace GcExtensionAuditMaui.Services;

/// <summary>
/// Service that coordinates report exports in multiple formats (CSV and Excel).
/// </summary>
public sealed class ReportModule
{
    private readonly OutputPathService _paths;
    private readonly LoggingService _log;
    private readonly ExportService _csvExporter;

    public ReportModule(OutputPathService paths, LoggingService log, ExportService csvExporter)
    {
        _paths = paths;
        _log = log;
        _csvExporter = csvExporter;
    }

    /// <summary>
    /// Exports audit data in Excel format with API snapshots and issue tracking.
    /// </summary>
    public async Task<string> ExportExcelReportAsync(
        AuditContext context,
        ApiStats apiStats,
        IReadOnlyList<IssueRow> issues,
        CancellationToken ct = default)
    {
        var outDir = _paths.GetNewOutputFolder();
        var fileName = GenerateExcelFileName();
        var outputPath = Path.Combine(outDir, fileName);

        await Task.Run(() =>
        {
            var snapshots = BuildApiSnapshots(context);
            ExcelReportExporter.Export(outputPath, snapshots, issues);
        }, ct);

        _log.Log(Models.Logging.LogLevel.Info, "Excel report exported", new { OutDir = outDir, FileName = fileName });
        return outDir;
    }

    /// <summary>
    /// Exports a full audit report with both CSV and Excel formats.
    /// </summary>
    public async Task<string> ExportFullAuditReportAsync(
        AuditContext context,
        DryRunReport report,
        ApiStats apiStats,
        CancellationToken ct = default)
    {
        var outDir = _paths.GetNewOutputFolder();

        // Export CSV files
        await _csvExporter.ExportDryRunCsvOnlyAsync(context, report, apiStats, outDir, ct);

        // Also export Excel version
        var fileName = GenerateExcelFileName();
        var excelPath = Path.Combine(outDir, fileName);

        await Task.Run(() =>
        {
            var snapshots = BuildApiSnapshots(context);
            var issues = ConvertReportToIssues(report);
            ExcelReportExporter.Export(excelPath, snapshots, issues);
        }, ct);

        _log.Log(Models.Logging.LogLevel.Info, "Full audit report exported (CSV + Excel)", new { OutDir = outDir });
        return outDir;
    }

    private static string GenerateExcelFileName()
    {
        return $"GenesysExtensionAudit_{DateTime.Now:yyyy-MM-dd_HHmm}.xlsx";
    }

    private static List<ApiSnapshot> BuildApiSnapshots(AuditContext context)
    {
        var snapshots = new List<ApiSnapshot>();

        // Add Users snapshot
        if (context.Users.Count > 0)
        {
            var userElements = context.Users
                .Select(u => JsonSerializer.SerializeToElement(u))
                .ToList();
            
            snapshots.Add(new ApiSnapshot
            {
                SheetName = "Users",
                Endpoint = "/api/v2/users",
                Items = userElements
            });
        }

        // Add Extensions snapshot if available
        if (context.Extensions.Count > 0)
        {
            var extensionElements = context.Extensions
                .Select(e => JsonSerializer.SerializeToElement(e))
                .ToList();

            snapshots.Add(new ApiSnapshot
            {
                SheetName = "Extensions",
                Endpoint = "/api/v2/telephony/providers/edges/extensions",
                Items = extensionElements
            });
        }

        return snapshots;
    }

    private static List<IssueRow> ConvertReportToIssues(DryRunReport report)
    {
        var issues = new List<IssueRow>();

        // Convert missing assignments
        foreach (var missing in report.MissingAssignments)
        {
            issues.Add(new IssueRow
            {
                IssueFound = missing.Issue,
                CurrentState = $"User: {missing.UserEmail}, Extension: {missing.ProfileExtension}",
                NewState = "Extension needs to be created or assigned",
                Severity = "High",
                EntityType = "User",
                EntityId = missing.UserId,
                EntityName = missing.UserName ?? "",
                Field = "Extension",
                Recommendation = "Create extension record or verify extension pool configuration",
                SourceEndpoint = "/api/v2/users"
            });
        }

        // Convert discrepancies
        foreach (var discrepancy in report.Discrepancies)
        {
            issues.Add(new IssueRow
            {
                IssueFound = discrepancy.Issue,
                CurrentState = $"Extension {discrepancy.ProfileExtension} has issue: {discrepancy.Issue}",
                NewState = "Extension assignment needs correction",
                Severity = "Medium",
                EntityType = "Extension",
                EntityId = discrepancy.ExtensionId ?? "",
                EntityName = discrepancy.ProfileExtension,
                Field = "Owner",
                Recommendation = "Review extension ownership and user profile",
                SourceEndpoint = "/api/v2/telephony/providers/edges/extensions"
            });
        }

        // Convert duplicate user assignments
        foreach (var dup in report.DuplicateUserAssignments)
        {
            issues.Add(new IssueRow
            {
                IssueFound = "Duplicate Extension Assignment",
                CurrentState = $"Multiple users assigned to extension: {dup.ProfileExtension}",
                NewState = "Only one user should have this extension",
                Severity = "High",
                EntityType = "User",
                EntityId = dup.UserId,
                EntityName = dup.UserName ?? "",
                Field = "Extension",
                Recommendation = "Reassign extensions to ensure unique user-extension mappings",
                SourceEndpoint = "/api/v2/users"
            });
        }

        // Convert duplicate extension records
        foreach (var dup in report.DuplicateExtensionRecords)
        {
            issues.Add(new IssueRow
            {
                IssueFound = "Duplicate Extension Record",
                CurrentState = $"Multiple records for extension: {dup.ExtensionNumber}",
                NewState = "Only one extension record should exist",
                Severity = "Critical",
                EntityType = "Extension",
                EntityId = dup.ExtensionId ?? "",
                EntityName = dup.ExtensionNumber,
                Field = "Extension Number",
                Recommendation = "Remove duplicate extension records",
                SourceEndpoint = "/api/v2/telephony/providers/edges/extensions"
            });
        }

        return issues;
    }
}
