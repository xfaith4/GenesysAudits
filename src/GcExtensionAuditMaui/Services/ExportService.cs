using System.Reflection;
using System.Text;
using System.Text.Json;
using GcExtensionAuditMaui.Models.Audit;
using GcExtensionAuditMaui.Models.Patch;
using GcExtensionAuditMaui.Models.Observability;

namespace GcExtensionAuditMaui.Services;

public sealed class ExportService
{
    private readonly OutputPathService _paths;
    private readonly LoggingService _log;

    public ExportService(OutputPathService paths, LoggingService log)
    {
        _paths = paths;
        _log = log;
    }

    public async Task<string> ExportDryRunAsync(AuditContext context, DryRunReport report, ApiStats apiStats, CancellationToken ct)
    {
        var outDir = _paths.GetNewOutputFolder();

        await WriteCsvAsync(report.Rows, Path.Combine(outDir, "DryRun.csv"), ct);
        await WriteCsvAsync(report.MissingAssignments, Path.Combine(outDir, "Missing.csv"), ct);
        await WriteCsvAsync(report.Discrepancies, Path.Combine(outDir, "Discrepancies.csv"), ct);
        await WriteCsvAsync(report.DuplicateUserAssignments, Path.Combine(outDir, "DuplicatesUsers.csv"), ct);
        await WriteCsvAsync(report.DuplicateExtensionRecords, Path.Combine(outDir, "DuplicatesExtensions.csv"), ct);
        await WriteSnapshotAsync(context, apiStats, Path.Combine(outDir, "Snapshot.json"), ct);

        _log.Log(Models.Logging.LogLevel.Info, "Dry run exported", new { OutDir = outDir });
        return outDir;
    }

    internal async Task<string> ExportDryRunCsvOnlyAsync(AuditContext context, DryRunReport report, ApiStats apiStats, string outDir, CancellationToken ct)
    {
        // Internal method for ReportModule to call for CSV export only
        await WriteCsvAsync(report.Rows, Path.Combine(outDir, "DryRun.csv"), ct);
        await WriteCsvAsync(report.MissingAssignments, Path.Combine(outDir, "Missing.csv"), ct);
        await WriteCsvAsync(report.Discrepancies, Path.Combine(outDir, "Discrepancies.csv"), ct);
        await WriteCsvAsync(report.DuplicateUserAssignments, Path.Combine(outDir, "DuplicatesUsers.csv"), ct);
        await WriteCsvAsync(report.DuplicateExtensionRecords, Path.Combine(outDir, "DuplicatesExtensions.csv"), ct);
        await WriteSnapshotAsync(context, apiStats, Path.Combine(outDir, "Snapshot.json"), ct);
        return outDir;
    }

    internal static async Task WriteSnapshotAsync(AuditContext context, ApiStats apiStats, string path, CancellationToken ct)
    {
        var summary = new ContextSummary
        {
            AuditKind = context.AuditKind,
            UsersTotal = context.Users.Count,
            UsersWithProfileExtension = context.UsersWithProfileExtension.Count,
            DistinctProfileExtensions = context.ProfileExtensionNumbers.Count,
            ExtensionsLoaded = context.Extensions.Count,
            ExtensionMode = context.ExtensionMode,
        };

        var payload = new
        {
            GeneratedAt = DateTime.Now.ToString("O"),
            ContextSummary = summary,
            ApiStats = apiStats.ToSnapshotObject(),
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, Encoding.UTF8, ct);
    }

    public async Task<string> ExportRowsAsync<T>(AuditContext context, IEnumerable<T> rows, string fileName, ApiStats apiStats, CancellationToken ct)
    {
        var outDir = _paths.GetNewOutputFolder();
        await WriteCsvAsync(rows, Path.Combine(outDir, fileName), ct);
        await WriteSnapshotAsync(context, apiStats, Path.Combine(outDir, "Snapshot.json"), ct);
        _log.Log(Models.Logging.LogLevel.Info, "Exported CSV", new { OutDir = outDir, FileName = fileName });
        return outDir;
    }

    public async Task<string> ExportPatchAsync(AuditContext context, PatchResult result, ApiStats apiStats, CancellationToken ct)
    {
        var outDir = _paths.GetNewOutputFolder();

        await WriteCsvAsync(result.Updated, Path.Combine(outDir, "PatchUpdated.csv"), ct);
        await WriteCsvAsync(result.Skipped, Path.Combine(outDir, "PatchSkipped.csv"), ct);
        await WriteCsvAsync(result.Failed, Path.Combine(outDir, "PatchFailed.csv"), ct);
        await WriteCsvAsync(new[] { result.Summary }, Path.Combine(outDir, "PatchSummary.csv"), ct);

        await WriteSnapshotAsync(context, apiStats, Path.Combine(outDir, "Snapshot.json"), ct);

        _log.Log(Models.Logging.LogLevel.Info, "Patch exported", new { OutDir = outDir });
        return outDir;
    }

    internal static async Task WriteCsvAsync<T>(IEnumerable<T> rows, string path, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var props = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetMethod is not null)
            .ToArray();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", props.Select(p => Escape(p.Name))));

        foreach (var row in rows)
        {
            var values = props.Select(p => Escape(p.GetValue(row)?.ToString()));
            sb.AppendLine(string.Join(",", values));
        }

        await File.WriteAllTextAsync(path, sb.ToString(), Encoding.UTF8, ct);
    }

    private static string Escape(string? value)
    {
        if (value is null) { return ""; }
        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!needsQuotes) { return value; }
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
