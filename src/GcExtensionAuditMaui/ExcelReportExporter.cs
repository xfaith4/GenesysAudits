// ### BEGIN: ExcelReportExporter

using OfficeOpenXml;

namespace GcExtensionAuditMaui;

public static class ExcelReportExporter
{
    public static void Export(string outputPath, IReadOnlyList<ApiSnapshot> snapshots, IReadOnlyList<IssueRow> issues)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        
        using var package = new ExcelPackage();

        // Add a sheet for each API snapshot
        foreach (var snapshot in snapshots)
        {
            AddSnapshotSheet(package, snapshot);
        }

        // Add issues sheet
        if (issues.Count > 0)
        {
            AddIssuesSheet(package, issues);
        }

        // Save the Excel file
        var file = new FileInfo(outputPath);
        package.SaveAs(file);
    }

    private static void AddSnapshotSheet(ExcelPackage package, ApiSnapshot snapshot)
    {
        var worksheet = package.Workbook.Worksheets.Add(snapshot.SheetName);
        
        // Flatten JSON items to rows
        var rows = JsonTableBuilder.BuildRows(snapshot.Items);
        if (rows.Count == 0)
        {
            worksheet.Cells[1, 1].Value = "No data";
            return;
        }

        // Get all unique column names
        var allColumns = rows
            .SelectMany(r => r.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Write header row
        for (int i = 0; i < allColumns.Count; i++)
        {
            worksheet.Cells[1, i + 1].Value = allColumns[i];
        }

        // Format header
        using (var headerRange = worksheet.Cells[1, 1, 1, allColumns.Count])
        {
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        }

        // Write data rows
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            for (int colIndex = 0; colIndex < allColumns.Count; colIndex++)
            {
                var columnName = allColumns[colIndex];
                if (row.TryGetValue(columnName, out var value))
                {
                    worksheet.Cells[rowIndex + 2, colIndex + 1].Value = value;
                }
            }
        }

        // Auto-fit columns
        worksheet.Cells.AutoFitColumns();
    }

    private static void AddIssuesSheet(ExcelPackage package, IReadOnlyList<IssueRow> issues)
    {
        var worksheet = package.Workbook.Worksheets.Add("Issues");

        // Write header
        var headers = new[] { "IssueFound", "CurrentState", "NewState", "Severity", "EntityType", "EntityId", "EntityName", "Field", "Recommendation", "SourceEndpoint" };
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cells[1, i + 1].Value = headers[i];
        }

        // Format header
        using (var headerRange = worksheet.Cells[1, 1, 1, headers.Length])
        {
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        }

        // Write issue rows
        for (int i = 0; i < issues.Count; i++)
        {
            var issue = issues[i];
            var row = i + 2;
            worksheet.Cells[row, 1].Value = issue.IssueFound;
            worksheet.Cells[row, 2].Value = issue.CurrentState;
            worksheet.Cells[row, 3].Value = issue.NewState;
            worksheet.Cells[row, 4].Value = issue.Severity;
            worksheet.Cells[row, 5].Value = issue.EntityType;
            worksheet.Cells[row, 6].Value = issue.EntityId;
            worksheet.Cells[row, 7].Value = issue.EntityName;
            worksheet.Cells[row, 8].Value = issue.Field;
            worksheet.Cells[row, 9].Value = issue.Recommendation;
            worksheet.Cells[row, 10].Value = issue.SourceEndpoint;
        }

        // Auto-fit columns
        worksheet.Cells.AutoFitColumns();
    }
}

// ### END: ExcelReportExporter
