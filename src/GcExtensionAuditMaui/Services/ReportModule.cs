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
        var fileName = GenerateExcelFileName(context.AuditKind);
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
        var fileName = GenerateExcelFileName(context.AuditKind);
        var excelPath = Path.Combine(outDir, fileName);

        await Task.Run(() =>
        {
            var snapshots = BuildApiSnapshots(context);
            var issues = ConvertReportToIssues(context, report);
            ExcelReportExporter.Export(excelPath, snapshots, issues);
        }, ct);

        _log.Log(Models.Logging.LogLevel.Info, "Full audit report exported (CSV + Excel)", new { OutDir = outDir });
        return outDir;
    }

    /// <summary>
    /// Exports discrepancies to Excel format.
    /// </summary>
    public async Task<string> ExportDiscrepanciesToExcelAsync(
        AuditContext context,
        ApiStats apiStats,
        IEnumerable<DiscrepancyRow> discrepancies,
        CancellationToken ct = default)
    {
        var entityType = context.AuditKind == AuditNumberKind.Did ? "DID" : "Extension";
        var numberLabel = context.AuditKind == AuditNumberKind.Did ? "DID" : "Extension";
        var endpoint = context.AuditKind == AuditNumberKind.Did ? "/api/v2/telephony/providers/edges/dids" : "/api/v2/telephony/providers/edges/extensions";

        var issues = discrepancies.Select(d => new IssueRow
        {
            IssueFound = d.Issue,
            CurrentState = $"{numberLabel} {d.ProfileExtension} has issue: {d.Issue}",
            NewState = $"{numberLabel} assignment needs correction",
            Severity = "Medium",
            EntityType = entityType,
            EntityId = d.ExtensionId ?? "",
            EntityName = d.ProfileExtension,
            Field = "Owner",
            Recommendation = $"Review {numberLabel.ToLower()} ownership and user profile",
            SourceEndpoint = endpoint
        }).ToList();

        return await ExportExcelReportAsync(context, apiStats, issues, ct);
    }

    /// <summary>
    /// Exports missing assignments to Excel format.
    /// </summary>
    public async Task<string> ExportMissingAssignmentsToExcelAsync(
        AuditContext context,
        ApiStats apiStats,
        IEnumerable<MissingAssignmentRow> missingAssignments,
        CancellationToken ct = default)
    {
        var numberLabel = context.AuditKind == AuditNumberKind.Did ? "DID" : "Extension";

        var issues = missingAssignments.Select(m => new IssueRow
        {
            IssueFound = m.Issue,
            CurrentState = $"User: {m.UserEmail}, {numberLabel}: {m.ProfileExtension}",
            NewState = $"{numberLabel} needs to be created or assigned",
            Severity = "High",
            EntityType = "User",
            EntityId = m.UserId,
            EntityName = m.UserName ?? "",
            Field = numberLabel,
            Recommendation = $"Create {numberLabel.ToLower()} record or verify {numberLabel.ToLower()} pool configuration",
            SourceEndpoint = "/api/v2/users"
        }).ToList();

        return await ExportExcelReportAsync(context, apiStats, issues, ct);
    }

    /// <summary>
    /// Exports duplicate user assignments to Excel format.
    /// </summary>
    public async Task<string> ExportDuplicateUsersToExcelAsync(
        AuditContext context,
        ApiStats apiStats,
        IEnumerable<DuplicateUserAssignmentRow> duplicateUsers,
        CancellationToken ct = default)
    {
        var numberLabel = context.AuditKind == AuditNumberKind.Did ? "DID" : "Extension";

        var issues = duplicateUsers.Select(d => new IssueRow
        {
            IssueFound = $"Duplicate {numberLabel} Assignment",
            CurrentState = $"Multiple users assigned to {numberLabel.ToLower()}: {d.ProfileExtension}",
            NewState = $"Only one user should have this {numberLabel.ToLower()}",
            Severity = "High",
            EntityType = "User",
            EntityId = d.UserId,
            EntityName = d.UserName ?? "",
            Field = numberLabel,
            Recommendation = $"Reassign {numberLabel.ToLower()}s to ensure unique user-{numberLabel.ToLower()} mappings",
            SourceEndpoint = "/api/v2/users"
        }).ToList();

        return await ExportExcelReportAsync(context, apiStats, issues, ct);
    }

    /// <summary>
    /// Exports duplicate extension records to Excel format.
    /// </summary>
    public async Task<string> ExportDuplicateExtensionsToExcelAsync(
        AuditContext context,
        ApiStats apiStats,
        IEnumerable<DuplicateExtensionRecordRow> duplicateExtensions,
        CancellationToken ct = default)
    {
        var entityType = context.AuditKind == AuditNumberKind.Did ? "DID" : "Extension";
        var numberLabel = context.AuditKind == AuditNumberKind.Did ? "DID" : "Extension";
        var endpoint = context.AuditKind == AuditNumberKind.Did ? "/api/v2/telephony/providers/edges/dids" : "/api/v2/telephony/providers/edges/extensions";

        var issues = duplicateExtensions.Select(d => new IssueRow
        {
            IssueFound = $"Duplicate {numberLabel} Record",
            CurrentState = $"Multiple records for {numberLabel.ToLower()}: {d.ExtensionNumber}",
            NewState = $"Only one {numberLabel.ToLower()} record should exist",
            Severity = "Critical",
            EntityType = entityType,
            EntityId = d.ExtensionId ?? "",
            EntityName = d.ExtensionNumber,
            Field = $"{numberLabel} Number",
            Recommendation = $"Remove duplicate {numberLabel.ToLower()} records",
            SourceEndpoint = endpoint
        }).ToList();

        return await ExportExcelReportAsync(context, apiStats, issues, ct);
    }

    private static string GenerateExcelFileName(AuditNumberKind auditKind)
    {
        var auditType = auditKind == AuditNumberKind.Did ? "DID" : "Extension";
        return $"Genesys{auditType}Audit_{DateTime.Now:yyyy-MM-dd_HHmm}.xlsx";
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

        // Add Extensions or DIDs snapshot based on audit type
        if (context.Extensions.Count > 0)
        {
            var extensionElements = context.Extensions
                .Select(e => JsonSerializer.SerializeToElement(e))
                .ToList();

            var sheetName = context.AuditKind == AuditNumberKind.Did ? "DIDs" : "Extensions";
            var endpoint = context.AuditKind == AuditNumberKind.Did ? "/api/v2/telephony/providers/edges/dids" : "/api/v2/telephony/providers/edges/extensions";

            snapshots.Add(new ApiSnapshot
            {
                SheetName = sheetName,
                Endpoint = endpoint,
                Items = extensionElements
            });
        }

        return snapshots;
    }

    private static List<IssueRow> ConvertReportToIssues(AuditContext context, DryRunReport report)
    {
        var issues = new List<IssueRow>();
        var numberLabel = context.AuditKind == AuditNumberKind.Did ? "DID" : "Extension";
        var entityType = context.AuditKind == AuditNumberKind.Did ? "DID" : "Extension";
        var endpoint = context.AuditKind == AuditNumberKind.Did ? "/api/v2/telephony/providers/edges/dids" : "/api/v2/telephony/providers/edges/extensions";

        // Convert missing assignments
        foreach (var missing in report.MissingAssignments)
        {
            issues.Add(new IssueRow
            {
                IssueFound = missing.Issue,
                CurrentState = $"User: {missing.UserEmail}, {numberLabel}: {missing.ProfileExtension}",
                NewState = $"{numberLabel} needs to be created or assigned",
                Severity = "High",
                EntityType = "User",
                EntityId = missing.UserId,
                EntityName = missing.UserName ?? "",
                Field = numberLabel,
                Recommendation = $"Create {numberLabel.ToLower()} record or verify {numberLabel.ToLower()} pool configuration",
                SourceEndpoint = "/api/v2/users"
            });
        }

        // Convert discrepancies
        foreach (var discrepancy in report.Discrepancies)
        {
            issues.Add(new IssueRow
            {
                IssueFound = discrepancy.Issue,
                CurrentState = $"{numberLabel} {discrepancy.ProfileExtension} has issue: {discrepancy.Issue}",
                NewState = $"{numberLabel} assignment needs correction",
                Severity = "Medium",
                EntityType = entityType,
                EntityId = discrepancy.ExtensionId ?? "",
                EntityName = discrepancy.ProfileExtension,
                Field = "Owner",
                Recommendation = $"Review {numberLabel.ToLower()} ownership and user profile",
                SourceEndpoint = endpoint
            });
        }

        // Convert duplicate user assignments
        foreach (var dup in report.DuplicateUserAssignments)
        {
            issues.Add(new IssueRow
            {
                IssueFound = $"Duplicate {numberLabel} Assignment",
                CurrentState = $"Multiple users assigned to {numberLabel.ToLower()}: {dup.ProfileExtension}",
                NewState = $"Only one user should have this {numberLabel.ToLower()}",
                Severity = "High",
                EntityType = "User",
                EntityId = dup.UserId,
                EntityName = dup.UserName ?? "",
                Field = numberLabel,
                Recommendation = $"Reassign {numberLabel.ToLower()}s to ensure unique user-{numberLabel.ToLower()} mappings",
                SourceEndpoint = "/api/v2/users"
            });
        }

        // Convert duplicate extension records
        foreach (var dup in report.DuplicateExtensionRecords)
        {
            issues.Add(new IssueRow
            {
                IssueFound = $"Duplicate {numberLabel} Record",
                CurrentState = $"Multiple records for {numberLabel.ToLower()}: {dup.ExtensionNumber}",
                NewState = $"Only one {numberLabel.ToLower()} record should exist",
                Severity = "Critical",
                EntityType = entityType,
                EntityId = dup.ExtensionId ?? "",
                EntityName = dup.ExtensionNumber,
                Field = $"{numberLabel} Number",
                Recommendation = $"Remove duplicate {numberLabel.ToLower()} records",
                SourceEndpoint = endpoint
            });
        }

        return issues;
    }
}
