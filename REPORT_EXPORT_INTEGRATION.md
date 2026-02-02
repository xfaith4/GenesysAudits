# Report Export Integration

This document describes the integrated report export functionality added to the GenesysAudits application.

## New Files

### 1. ExcelReportExporter.cs
- **Location**: `src/GcExtensionAuditMaui/ExcelReportExporter.cs`
- **Purpose**: Provides Excel export functionality using EPPlus library
- **Key Features**:
  - Exports API snapshots (Users, Extensions, etc.) to separate Excel worksheets
  - Flattens JSON data into tabular format using JsonTableBuilder
  - Exports issue rows with detailed tracking information
  - Auto-formats headers and auto-fits columns

### 2. JsonFlattening.cs
- **Location**: `src/GcExtensionAuditMaui/JsonFlattening.cs`
- **Purpose**: Provides JSON flattening utility for converting nested JSON objects to flat dictionary rows
- **Key Class**: `JsonTableBuilder`
- **Key Features**:
  - Flattens nested JSON objects with configurable depth
  - Handles arrays by preserving them as JSON strings
  - Converts all scalar values to strings for table display

### 3. ReportModels.cs
- **Location**: `src/GcExtensionAuditMaui/ReportModels.cs`
- **Purpose**: Defines data models for report exports
- **Key Classes**:
  - `ApiSnapshot`: Represents a snapshot of API data (e.g., Users, Extensions)
  - `IssueRow`: Represents a single issue found during audit with detailed metadata

### 4. ReportModule.cs
- **Location**: `src/GcExtensionAuditMaui/Services/ReportModule.cs`
- **Purpose**: Coordinates report exports in multiple formats (CSV and Excel)
- **Key Features**:
  - `ExportExcelReportAsync`: Exports Excel-only reports
  - `ExportFullAuditReportAsync`: Exports both CSV and Excel formats in one operation
  - Converts audit reports to IssueRow format for Excel export
  - Builds API snapshots from AuditContext

## Integration Points

### Service Registration
The new services are registered in `MauiProgram.cs`:
```csharp
builder.Services.AddSingleton<ReportModule>();
```

### Dependencies
- **EPPlus** (v7.5.2): Excel generation library (NonCommercial license)
- Integrates with existing services: `OutputPathService`, `LoggingService`, `ExportService`

### Usage in ViewModels
- **DryRunViewModel**: Updated to use `ReportModule.ExportFullAuditReportAsync()` for generating both CSV and Excel reports
- Other ViewModels continue to use `ExportService` for CSV-only exports

## Export Behavior

When a user exports a Dry Run report, the system now:
1. Creates a new output folder
2. Exports all CSV files (DryRun.csv, Missing.csv, Discrepancies.csv, etc.)
3. Exports a JSON snapshot (Snapshot.json)
4. **NEW**: Exports a comprehensive Excel file with:
   - Users worksheet (flattened user data)
   - Extensions worksheet (flattened extension data)
   - Issues worksheet (all detected issues with severity, recommendations, etc.)

## File Removals
- **ReportExportGlueExample.dart**: Removed as it was a Dart example file not needed in the C#/.NET MAUI application

## Notes
- The Excel export uses NonCommercial license context for EPPlus
- All existing CSV export functionality remains unchanged
- The Excel export is additive - it doesn't replace CSV exports
- ViewModels other than DryRunViewModel continue to use CSV-only exports
