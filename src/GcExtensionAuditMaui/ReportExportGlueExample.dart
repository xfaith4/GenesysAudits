// ### BEGIN: ReportExportGlueExample

using System.Text.Json;

// Suppose these come from your API calls:
IReadOnlyList<JsonElement> users = ...;
IReadOnlyList<JsonElement> pools = ...;
IReadOnlyList<JsonElement> extensions = ...;
IReadOnlyList<JsonElement> dids = ...;

// Your existing issue detection should populate these:
List<IssueRow> issues = ...;

var snapshots = new List<ApiSnapshot>
{
    new() { SheetName = "Users",          Endpoint = "/api/v2/users", Items = users },
    new() { SheetName = "ExtensionPools", Endpoint = "/api/v2/telephony/providers/edges/extensionpools", Items = pools },
    new() { SheetName = "Extensions",     Endpoint = "/api/v2/telephony/providers/edges/extensions", Items = extensions },
    new() { SheetName = "DIDs",           Endpoint = "/api/v2/telephony/providers/edges/dids", Items = dids },
};

var fileName = $"GenesysExtensionAudit_{DateTime.Now:yyyy-MM-dd_HHmm}.xlsx";
var outputPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

ExcelReportExporter.Export(outputPath, snapshots, issues);

// Then: surface outputPath in your UI and/or use a file save/share mechanism.
// ### END: ReportExportGlueExample
