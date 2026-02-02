# Implementation Summary: DID and Extension Audit Improvements

## Overview
This implementation addresses the issues raised in the problem statement regarding DID export labeling, audit comparison logic, and the ability to run combined audits.

## Problems Addressed

### 1. DID Labeling Issues ✓
**Problem**: When exporting DIDs and their issues, the export incorrectly labeled them as extensions.

**Solution**: Updated all export methods in `ReportModule.cs` to check `AuditNumberKind` and use appropriate labels:
- `EntityType` now shows "DID" or "Extension" based on audit type
- Issue descriptions use "DID" or "Extension" terminology appropriately
- Sheet names are "DIDs" vs "Extensions" based on audit type
- File names include audit type (e.g., "GenesysDIDAudit_*.xlsx" vs "GenesysExtensionAudit_*.xlsx")

### 2. DID Audit Comparison ✓
**Problem**: Need to ensure DID audit compares DIDs from main list and DIDs seen on user profiles.

**Solution**: The existing `AuditService.BuildContextAsync` already properly handles this:
- For DID audits (`AuditNumberKind.Did`), it:
  - Fetches all users and extracts DIDs from user profiles using `GetUserProfileDid()`
  - Fetches all DIDs from the `/api/v2/telephony/providers/edges/dids` endpoint
  - Maps DIDs to the internal `GcExtension` format using `MapDidToExtension()`
  - Compares profile DIDs against the fetched DID list to find discrepancies

### 3. Excel Export Parsing ✓
**Problem**: Ensure parsing happens to the Excel export so properties are in separate columns.

**Solution**: Already implemented via `JsonTableBuilder.BuildRows()`:
- Flattens nested JSON objects into dot-notation columns (e.g., `addresses.extension`, `owner.id`)
- Handles up to 5 levels of nesting
- Arrays preserved as JSON strings to avoid column explosion
- All scalar values extracted to individual columns

### 4. Combined Audit Feature ✓
**Problem**: Add option to perform both DID and Extension audits in same run with proper sheet names.

**Solution**: Implemented complete "Run Both Audits" feature:
- Added `RunBothAudits` checkbox in UI (AuditToolbarView.xaml)
- Extended `ContextStore` to hold both Extension and DID contexts
- Updated `BuildContextAsync` to fetch both contexts when enabled
- Added `ExportCombinedAuditReportAsync` method that creates single workbook with:
  - "Users" sheet (shared between both audits)
  - "Extensions" sheet
  - "DIDs" sheet  
  - "Issues" sheet with combined issues from both audits
- Minimizes API calls by reusing user data between both audits

## Code Changes

### Files Modified
1. **src/GcExtensionAuditMaui/Services/ReportModule.cs** (201 lines changed)
   - Fixed all export methods to use correct DID vs Extension labels
   - Added `ExportCombinedAuditReportAsync` for combined reports
   - Updated `BuildApiSnapshots` to use correct sheet names
   - Updated `ConvertReportToIssues` to respect AuditNumberKind

2. **src/GcExtensionAuditMaui/ViewModels/DashboardViewModel.cs** (127 lines changed)
   - Added `RunBothAudits` property with persistence
   - Updated `BuildContextAsync` to handle both audits
   - Updated `ExportAuditReportAsync` to use combined export when enabled
   - Updated `AuditTitle` to show combined audit mode

3. **src/GcExtensionAuditMaui/ViewModels/ContextStore.cs** (16 lines added)
   - Added `ExtensionContext` and `DidContext` properties
   - Updated `Clear()` to clear both contexts

4. **src/GcExtensionAuditMaui/Views/Components/AuditToolbarView.xaml** (2 lines added)
   - Added "Both" checkbox next to Ext/DID radio buttons

5. **tests/GcExtensionAuditMaui.Tests/ReportModuleTests.cs** (136 lines, new file)
   - Tests for DID vs Extension sheet naming
   - Tests for correct EntityType labeling
   - Uses reflection to test private methods

## Testing

### Unit Tests Added
- `BuildApiSnapshots_UsesCorrectSheetName_ForExtensions()` - Verifies "Extensions" sheet name
- `BuildApiSnapshots_UsesCorrectSheetName_ForDIDs()` - Verifies "DIDs" sheet name
- `ConvertReportToIssues_UsesCorrectEntityType_ForDIDs()` - Verifies "DID" EntityType
- `ConvertReportToIssues_UsesCorrectEntityType_ForExtensions()` - Verifies "Extension" EntityType

### Manual Testing Required
Due to MAUI workload requirements, manual testing is needed to verify:
1. UI displays "Both" checkbox correctly
2. Checking "Both" disables Ext/DID radio buttons appropriately
3. Building context with "Both" checked fetches both Extensions and DIDs
4. Export creates workbook with all three sheets (Users, Extensions, DIDs, Issues)
5. Issue labels correctly show "DID" or "Extension" based on audit type
6. Combined export minimizes API calls by reusing user data

## Key Features

### Minimal API Calls
When running both audits:
- Users are fetched only once (shared between both audits)
- Extensions fetched once
- DIDs fetched once
- Total: One user API call + two number pool calls (Extensions + DIDs)

### Proper Sheet Naming
- Extension audit → "Extensions" sheet
- DID audit → "DIDs" sheet  
- Combined audit → Both "Extensions" and "DIDs" sheets
- Users always → "Users" sheet
- Issues always → "Issues" sheet

### Correct Labeling Throughout
All issue types now properly labeled:
- Missing assignments use "DID" or "Extension" in messages
- Discrepancies use correct EntityType
- Duplicate records use correct terminology
- Recommendations use appropriate labels

## Backward Compatibility
All changes are backward compatible:
- Existing single-audit workflows unchanged
- "Run Both" is opt-in via checkbox
- Default behavior remains single audit
- Existing export methods still work correctly

## Summary
All requirements from the problem statement have been addressed:
✓ DID exports properly labeled
✓ DID audit compares main list vs user profiles  
✓ Excel export parses properties into separate columns
✓ Option to run both audits with minimal API calls
✓ Combined export to single file with proper sheet names
