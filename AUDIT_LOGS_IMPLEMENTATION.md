# Audit Logs Implementation Summary

## Overview
This implementation adds a third audit workstream "Audit Logs" to the Genesys Audits MAUI application, powered by Genesys Cloud Audit Query endpoints. The implementation includes transaction-based queries with cursor pagination and realtime related queries.

## Files Changed/Added

### New Models (`src/GcExtensionAuditMaui/Models/`)
- **Audit/AuditWorkstreamKind.cs**: Enum for workstream selection (Numbers, Logs)
- **AuditLogs/AuditLogQueryRequest.cs**: Query builder model and API request models
- **AuditLogs/AuditLogQueryResponse.cs**: API response models for audit queries, transactions, and results
- **AuditLogs/AuditLogRow.cs**: Display row model for UI presentation
- **AuditLogs/AuditLogState.cs**: State container holding query results and summary aggregates

### Updated Services
- **Services/GenesysCloudApiClient.cs**: Added 6 new API methods:
  - `GetAuditQueryServiceMappingAsync`: GET /api/v2/audits/query/servicemapping
  - `PostAuditQueryAsync`: POST /api/v2/audits/query
  - `GetAuditQueryTransactionAsync`: GET /api/v2/audits/query/{transactionId}
  - `GetAuditQueryResultsAsync`: GET /api/v2/audits/query/{transactionId}/results (with cursor support)
  - `GetAuditQueryRealtimeServiceMappingAsync`: GET /api/v2/audits/query/realtime/servicemapping
  - `PostAuditQueryRealtimeRelatedAsync`: POST /api/v2/audits/query/realtime/related

- **Services/AuditLogsService.cs** (NEW): Service layer for audit logs
  - `RunStandardQueryAsync`: Full query with transaction polling and cursor-based pagination
  - `RunRealtimeRelatedAsync`: Realtime related audit query
  - Handles transaction state polling (up to 2 minutes)
  - Accumulates all result pages (500 per page)

- **Services/ReportModule.cs**: Added `ExportAuditLogsReportAsync` method

- **MauiProgram.cs**: Registered `AuditLogsService` singleton

### Updated ExcelReportExporter
- **ExcelReportExporter.cs**: Added audit logs export functionality
  - `ExportAuditLogs`: Main export method
  - `AddAuditLogsExecutiveSummarySheet`: Executive summary with top actions, entity types, and actors
  - `AddAuditResultsSheet`: Raw audit log results
  - `AddQueryInfoSheet`: Query parameters and filters
  - `AddTransactionStatusSheet`: Transaction metadata
  - `AddServiceMappingSheet`: Service mapping data

### Updated ViewModel
- **ViewModels/DashboardViewModel.cs**: Major updates
  - Added `SelectedWorkstream` property with IsNumbersMode/IsLogsMode helpers
  - Added 11 new properties for audit log query builder (intervals, filters, service mapping)
  - Added `AuditLogState` and `AuditLogRows` collection
  - Updated `AuditTitle` to reflect current mode
  - Modified `RunAuditAsync` to route by workstream
  - Added `RunAuditLogsQueryAsync`: Main query execution method
  - Added `LoadServiceMappingAsync` command
  - Added `RunRealtimeRelatedQueryAsync` command
  - Updated `ExportAuditReportAsync` to handle Logs mode

### Updated UI Components
- **Views/Components/AuditToolbarView.xaml**:
  - Added workstream selector (Numbers/Logs radio buttons)
  - Added conditional UI sections for each mode
  - Added Logs mode query builder with:
    - Interval Start/End date pickers
    - Service name entry
    - Filter fields (User ID, Action, etc.)
    - Expand User checkbox
    - "Load Services" button
    - "Run Query" button

- **Views/Components/ResultsPanelView.xaml**:
  - Added conditional rendering based on workstream
  - Added audit logs result grid with columns:
    - Timestamp
    - Action
    - Entity Type
    - Entity ID
    - Service Name
    - User Display
  - Preserved existing Numbers mode results table

## How to Use the New UI

### Switching to Audit Logs Mode
1. At the top of the toolbar, find the **Mode** selector
2. Select the **"Audit Logs"** radio button
3. The UI will switch to show the audit logs query builder

### Running an Audit Logs Query

#### Standard Query (Interval-based)
1. **Set Time Interval**:
   - Use "Interval Start" date picker to select start date
   - Use "Interval End" date picker to select end date
   - Default is last 1 hour

2. **(Optional) Load Service Mapping**:
   - Click "Load Services" to fetch available service names from the API
   - This populates the service mapping for use in the query

3. **(Optional) Add Filters**:
   - **Service**: Enter service name (e.g., "Platform")
   - **User ID**: Filter by specific user GUID
   - **Action**: Filter by action type (e.g., "create", "update", "delete")
   - **Entity Type**: Filter by entity type
   - **Entity ID**: Filter by specific entity GUID
   - **Expand User**: Check to include user details in results (default: checked)

4. **Run Query**:
   - Click "Run Query" button
   - The app will:
     - POST the query to create a transaction
     - Poll transaction status until complete (max 2 min)
     - Fetch all result pages using cursor pagination (500 per page)
     - Display results in the grid below

5. **View Results**:
   - Results appear in the table with columns for timestamp, action, entity type, etc.
   - The result count is shown in the badge next to "Audit Logs"

6. **Export Results**:
   - Click "Export" to generate an Excel report with:
     - Executive Summary (aggregates)
     - Audit Results (all rows)
     - Audit Query (parameters used)
     - Audit Transaction (transaction metadata)
     - Audit Service Mapping (if loaded)

7. **Open Output Folder**:
   - Click "Open Out" to open the folder containing exported reports

#### Realtime Related Query (NOT IMPLEMENTED IN UI YET)
The backend service supports realtime related queries via `RunRealtimeRelatedQueryAsync`, but the UI does not yet expose this functionality. To add it:
- Add a collapsible "Advanced" section in the Logs query builder
- Add entry fields for Audit ID and (optional) Trustor Org ID
- Add "Fetch Related" button that calls `RunRealtimeRelatedQueryAsync`

### Switching Back to Numbers Mode
1. Select **"Numbers (Ext/DID)"** radio button
2. The UI returns to the original Extension/DID audit interface
3. All existing functionality remains unchanged

## Technical Details

### Filter Payload Shape
Filters are sent as an array of objects:
```json
{
  "interval": "2024-01-01T00:00:00Z/2024-01-02T00:00:00Z",
  "serviceName": "Platform",
  "filters": [
    { "property": "userId", "value": "user-guid" },
    { "property": "action", "value": "create" }
  ],
  "sort": [
    { "name": "Timestamp", "sortOrder": "DESC" }
  ]
}
```

Only non-empty filters are included in the request.

### Cursor Pagination
- Initial request: `/results?pageSize=500&expand=user`
- Subsequent requests: `/results?cursor={cursor}&pageSize=500&expand=user`
- Pagination continues until `cursor` field is absent or empty in response

### Transaction Polling
- Polls every 2 seconds
- Max duration: 120 seconds
- Terminal states: "Fulfilled", "Failed", "Expired"
- If timeout occurs, attempts to fetch results anyway

## Known Limitations & Future Improvements

### Current Limitations
1. **Date/Time Picker**: Only date selection is supported (no time picker). Users must manually adjust times in code or accept default interval.
2. **Service Name Dropdown**: Service name is a free-text entry field rather than a dropdown populated from service mapping.
3. **Realtime Related Query**: Backend is implemented but not exposed in UI.
4. **Summary Page**: Audit Logs don't have a dedicated summary page (summary is included in Excel export).
5. **Result Filtering**: No client-side filtering of audit log results (unlike Numbers mode which has category filters).
6. **Virtualization**: Large result sets (10,000+ events) may cause performance issues. Consider implementing virtualization.

### Recommended Future Enhancements

1. **Enhanced Time Selection**:
   - Add time pickers alongside date pickers
   - Add quick-select buttons (Last Hour, Last 24h, Last Week, etc.)

2. **Service Name Picker**:
   - Auto-load service mapping on mode switch
   - Convert service name entry to a Picker/ComboBox populated from loaded services
   - Add display names for better UX

3. **Advanced Filters Section**:
   - Create collapsible "Advanced" expander
   - Add all remaining filter options (EntityType, EntityId, ClientId)
   - Add realtime related query section

4. **Client-Side Filtering**:
   - Add filter box to search loaded results
   - Add category/facet buttons for quick filtering (like Numbers mode)

5. **Result Enhancements**:
   - Add "Details" column or expandable rows to show property changes
   - Add column sorting
   - Add column selection/customization

6. **Streaming UI**:
   - Update UI incrementally as pages are fetched (instead of waiting for all pages)
   - Show progress indicator with page count

7. **Result Virtualization**:
   - Implement virtualized collection view for better performance with large result sets

8. **Dedicated Summary Page**:
   - Create AuditLogsSummaryPage similar to existing SummaryPage
   - Show charts/graphs for top aggregates
   - Wire up "Summary" button for Logs mode

9. **Export Enhancements**:
   - Add CSV export option
   - Add PDF export for executive summary
   - Include property changes in detail sheet

10. **Query History**:
    - Save recent queries for quick re-run
    - Add "Load Query" / "Save Query" functionality

## Testing Notes

Since this is a Windows-only MAUI application and cannot be built on Linux, the implementation has not been compiled or tested. The following should be verified on Windows:

### Manual Testing Checklist
1. **Build**: Verify solution builds without errors
2. **Mode Switching**: 
   - Switch between Numbers and Logs modes
   - Verify UI updates correctly
   - Verify title changes
3. **Numbers Mode (Regression Testing)**:
   - Build Context still works
   - Run Audit still works for Ext/DID
   - Export still works
   - Summary still works
   - No broken UI elements
4. **Logs Mode - Happy Path**:
   - Set valid interval (e.g., last hour)
   - Run query without filters
   - Verify results appear in grid
   - Verify result count badge updates
   - Export report
   - Verify Excel has all expected sheets
   - Open output folder
5. **Logs Mode - With Filters**:
   - Run query with serviceName filter
   - Run query with userId filter
   - Run query with multiple filters
   - Verify results are filtered correctly
6. **Service Mapping**:
   - Click "Load Services" button
   - Verify no errors
   - (Future: verify picker populated)
7. **Error Handling**:
   - Run query with invalid interval (end before start)
   - Verify validation error shows
   - Run query with missing token
   - Verify error message
   - Test with failed transaction
   - Test with timeout scenario
8. **Cancellation**:
   - Start a long-running query
   - Click Cancel
   - Verify query stops gracefully

### CodeQL Security Scan
Run CodeQL on the changes to identify any security issues:
- SQL injection risks (N/A - no SQL)
- Input validation issues
- Exception handling
- Resource leaks

### Performance Testing
- Test with large result sets (10k+ events)
- Monitor memory usage
- Test pagination with many pages (20+)
- Verify UI remains responsive

## Assumptions

1. **Filter Property Names**: The API expects exact property names like "userId", "clientId", "action", "entityType", "entityId". These are hardcoded based on API documentation assumptions.

2. **Expand Parameter**: The `expand=user` parameter is assumed to populate the `User` object in results. The implementation handles cases where this is null or empty.

3. **ISO-8601 Interval Format**: The interval is formatted as `{startUtc:O}/{endUtc:O}` which produces ISO-8601 format. This may need adjustment based on actual API requirements.

4. **Cursor Behavior**: The implementation assumes cursor is either present (more pages) or absent/null/empty (no more pages). Edge cases with empty strings are handled.

5. **Transaction Terminal States**: The implementation assumes "Fulfilled", "Failed", and "Expired" are the only terminal states. Other states trigger continued polling.

6. **Page Size**: Fixed at 500 per page, which is assumed to be the maximum allowed by the API.

7. **Genesys Cloud Permissions**: The user's OAuth token must have permissions for audit query endpoints. No explicit permission checking is done in the app.

## Conclusion

This implementation provides a complete end-to-end solution for querying and exporting Genesys Cloud audit logs, while preserving all existing Extension/DID functionality. The architecture is extensible and follows the existing patterns in the codebase.

The implementation is ready for Windows compilation and testing. Once tested, the recommended enhancements can be prioritized based on user feedback and requirements.
