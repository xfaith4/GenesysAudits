// ### BEGIN: ExcelReportExporter

using OfficeOpenXml;

namespace GcExtensionAuditMaui;

public static class ExcelReportExporter
{
    public static void Export(string outputPath, IReadOnlyList<ApiSnapshot> snapshots, IReadOnlyList<IssueRow> issues)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        
        using var package = new ExcelPackage();

        // Add Executive Summary as first sheet (if issues exist)
        if (issues.Count > 0)
        {
            AddExecutiveSummarySheet(package, snapshots, issues);
        }

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

    private static void AddExecutiveSummarySheet(ExcelPackage package, IReadOnlyList<ApiSnapshot> snapshots, IReadOnlyList<IssueRow> issues)
    {
        var worksheet = package.Workbook.Worksheets.Add("Executive Summary");
        worksheet.TabColor = System.Drawing.Color.DarkBlue;
        
        int row = 1;

        // Title
        worksheet.Cells[row, 1].Value = "Genesys Cloud Extension Audit - Executive Summary";
        worksheet.Cells[row, 1].Style.Font.Size = 16;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.DarkBlue);
        row += 2;

        // Report Date
        worksheet.Cells[row, 1].Value = "Report Date:";
        worksheet.Cells[row, 2].Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        row += 2;

        // Overall Health Score Section
        var totalIssues = issues.Count;
        var criticalIssues = issues.Count(i => i.Severity?.Equals("Critical", StringComparison.OrdinalIgnoreCase) == true);
        var highIssues = issues.Count(i => i.Severity?.Equals("High", StringComparison.OrdinalIgnoreCase) == true);
        var mediumIssues = issues.Count(i => i.Severity?.Equals("Medium", StringComparison.OrdinalIgnoreCase) == true);
        var lowIssues = issues.Count(i => i.Severity?.Equals("Low", StringComparison.OrdinalIgnoreCase) == true);

        // Calculate health score (100 - weighted penalty for issues)
        var healthScore = 100 - Math.Min(100, (criticalIssues * 10) + (highIssues * 5) + (mediumIssues * 2) + (lowIssues * 1));
        var healthStatus = healthScore >= 90 ? "Excellent" : healthScore >= 75 ? "Good" : healthScore >= 50 ? "Fair" : "Poor";
        var healthColor = healthScore >= 90 ? System.Drawing.Color.Green : 
                         healthScore >= 75 ? System.Drawing.Color.YellowGreen : 
                         healthScore >= 50 ? System.Drawing.Color.Orange : 
                         System.Drawing.Color.Red;

        worksheet.Cells[row, 1].Value = "Configuration Health Score:";
        worksheet.Cells[row, 2].Value = $"{healthScore}/100 - {healthStatus}";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Font.Size = 14;
        worksheet.Cells[row, 2].Style.Font.Bold = true;
        worksheet.Cells[row, 2].Style.Font.Size = 14;
        worksheet.Cells[row, 2].Style.Font.Color.SetColor(healthColor);
        row += 2;

        // Issue Summary by Severity
        worksheet.Cells[row, 1].Value = "Issue Summary by Severity";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Font.Size = 13;
        worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        worksheet.Cells[row, 1, row, 3].Merge = true;
        row++;

        var severityHeaders = new[] { "Severity Level", "Count", "% of Total" };
        for (int i = 0; i < severityHeaders.Length; i++)
        {
            worksheet.Cells[row, i + 1].Value = severityHeaders[i];
            worksheet.Cells[row, i + 1].Style.Font.Bold = true;
        }
        row++;

        var severityData = new[]
        {
            ("Critical", criticalIssues, System.Drawing.Color.Red),
            ("High", highIssues, System.Drawing.Color.OrangeRed),
            ("Medium", mediumIssues, System.Drawing.Color.Orange),
            ("Low", lowIssues, System.Drawing.Color.Gold)
        };

        foreach (var (severity, count, color) in severityData)
        {
            var percentage = totalIssues > 0 ? (count * 100.0 / totalIssues) : 0;
            worksheet.Cells[row, 1].Value = severity;
            worksheet.Cells[row, 2].Value = count;
            worksheet.Cells[row, 3].Value = $"{percentage:F1}%";
            
            if (count > 0)
            {
                worksheet.Cells[row, 1].Style.Font.Color.SetColor(color);
                worksheet.Cells[row, 2].Style.Font.Bold = true;
            }
            row++;
        }
        
        worksheet.Cells[row, 1].Value = "Total Issues";
        worksheet.Cells[row, 2].Value = totalIssues;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 2].Style.Font.Bold = true;
        row += 2;

        // Issue Summary by Category
        var issuesByCategory = issues
            .GroupBy(i => i.IssueFound ?? "Unknown")
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        if (issuesByCategory.Any())
        {
            worksheet.Cells[row, 1].Value = "Top Issue Categories";
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.Font.Size = 13;
            worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            worksheet.Cells[row, 1, row, 3].Merge = true;
            row++;

            var categoryHeaders = new[] { "Issue Type", "Count", "% of Total" };
            for (int i = 0; i < categoryHeaders.Length; i++)
            {
                worksheet.Cells[row, i + 1].Value = categoryHeaders[i];
                worksheet.Cells[row, i + 1].Style.Font.Bold = true;
            }
            row++;

            foreach (var item in issuesByCategory)
            {
                var percentage = totalIssues > 0 ? (item.Count * 100.0 / totalIssues) : 0;
                worksheet.Cells[row, 1].Value = item.Category;
                worksheet.Cells[row, 2].Value = item.Count;
                worksheet.Cells[row, 3].Value = $"{percentage:F1}%";
                row++;
            }
            row++;
        }

        // Entity Summary
        var userSnapshot = snapshots.FirstOrDefault(s => s.SheetName.Equals("Users", StringComparison.OrdinalIgnoreCase));
        var extSnapshot = snapshots.FirstOrDefault(s => s.SheetName.Equals("Extensions", StringComparison.OrdinalIgnoreCase));
        var didSnapshot = snapshots.FirstOrDefault(s => s.SheetName.Equals("DIDs", StringComparison.OrdinalIgnoreCase));

        worksheet.Cells[row, 1].Value = "Entity Inventory";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Font.Size = 13;
        worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        worksheet.Cells[row, 1, row, 2].Merge = true;
        row++;

        worksheet.Cells[row, 1].Value = "Entity Type";
        worksheet.Cells[row, 2].Value = "Count";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 2].Style.Font.Bold = true;
        row++;

        if (userSnapshot != null)
        {
            worksheet.Cells[row, 1].Value = "Users";
            worksheet.Cells[row, 2].Value = userSnapshot.Items.Count;
            row++;
        }

        if (extSnapshot != null)
        {
            worksheet.Cells[row, 1].Value = "Extensions";
            worksheet.Cells[row, 2].Value = extSnapshot.Items.Count;
            row++;
        }

        if (didSnapshot != null)
        {
            worksheet.Cells[row, 1].Value = "DIDs";
            worksheet.Cells[row, 2].Value = didSnapshot.Items.Count;
            row++;
        }
        row++;

        // Impact Analysis
        worksheet.Cells[row, 1].Value = "Impact Analysis";
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Font.Size = 13;
        worksheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
        worksheet.Cells[row, 1, row, 2].Merge = true;
        row++;

        var affectedUsers = issues.Select(i => i.EntityId).Distinct().Count();
        worksheet.Cells[row, 1].Value = "Unique Users/Entities Affected:";
        worksheet.Cells[row, 2].Value = affectedUsers;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        row++;

        var criticalRecommendations = issues
            .Where(i => i.Severity?.Equals("Critical", StringComparison.OrdinalIgnoreCase) == true || 
                       i.Severity?.Equals("High", StringComparison.OrdinalIgnoreCase) == true)
            .Select(i => i.Recommendation)
            .Distinct()
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Take(5)
            .ToList();

        if (criticalRecommendations.Any())
        {
            row++;
            worksheet.Cells[row, 1].Value = "Top Priority Recommendations:";
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.Font.Size = 12;
            row++;

            foreach (var rec in criticalRecommendations)
            {
                worksheet.Cells[row, 1].Value = $"• {rec}";
                worksheet.Cells[row, 1, row, 3].Merge = true;
                worksheet.Cells[row, 1].Style.WrapText = true;
                row++;
            }
        }

        // Format columns
        worksheet.Column(1).Width = 40;
        worksheet.Column(2).Width = 20;
        worksheet.Column(3).Width = 15;

        // Add note at bottom
        row += 2;
        worksheet.Cells[row, 1].Value = "Note: This executive summary provides a high-level overview of configuration issues. See 'Issues' sheet for detailed information.";
        worksheet.Cells[row, 1, row, 3].Merge = true;
        worksheet.Cells[row, 1].Style.Font.Size = 9;
        worksheet.Cells[row, 1].Style.Font.Italic = true;
        worksheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
        worksheet.Cells[row, 1].Style.WrapText = true;
    }
}

// ### END: ExcelReportExporter
