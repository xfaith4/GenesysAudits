# UI Changes - Audit Logs Feature

## Visual Changes to the Application

### 1. Toolbar - Workstream Selector (NEW)
**Location**: Top of toolbar, below the title  
**Purpose**: Switch between Numbers mode (Extensions/DIDs) and Audit Logs mode

```
┌─────────────────────────────────────────────────────────────┐
│ Mode: ⦿ Numbers (Ext/DID)  ○ Audit Logs                     │
└─────────────────────────────────────────────────────────────┘
```

### 2. Toolbar - Numbers Mode (UNCHANGED)
When "Numbers (Ext/DID)" is selected, the toolbar shows:

```
┌─────────────────────────────────────────────────────────────┐
│ [Build Context] [Run Audit] [Rebuild Plan] [Cancel]         │
│                                                               │
│ Audit: ⦿ Ext ○ DID  ☑ Both  [Summary] [Export] [Open Out]  │
└─────────────────────────────────────────────────────────────┘
```

### 3. Toolbar - Audit Logs Mode (NEW)
When "Audit Logs" is selected, the toolbar transforms to:

```
┌─────────────────────────────────────────────────────────────┐
│ Interval Start: [2024-01-15 ▼]  Interval End: [2024-01-16 ▼]│
│                                          [Load Services]      │
│                                                               │
│ Service: [________]  User ID: [________]  Action: [_____]   │
│                                        ☑ Expand User         │
│                                                               │
│ [Run Query] [Cancel]  [Export] [Open Out]                   │
└─────────────────────────────────────────────────────────────┘
```

### 4. Results Panel - Numbers Mode (UNCHANGED)
Shows the familiar plan items table:

```
┌─────────────────────────────────────────────────────────────┐
│ Results (42)                                     [Filter...] │
│                                                               │
│ [All] [Missing] [Duplicates] [Discrep.]                     │
├──────────┬────────────┬─────────┬─────────────┬─────────────┤
│ Category │ User/Notes │ Current │ Action      │ Target      │
├──────────┼────────────┼─────────┼─────────────┼─────────────┤
│ Missing  │ john.doe   │ 1001    │ Assign      │ 1001        │
│ Duplicate│ jane.smith │ 1002    │ Reassign    │ 1005        │
│    ...   │    ...     │   ...   │     ...     │    ...      │
└──────────┴────────────┴─────────┴─────────────┴─────────────┘
```

### 5. Results Panel - Audit Logs Mode (NEW)
Shows audit log events in a new table format:

```
┌─────────────────────────────────────────────────────────────┐
│ Audit Logs (157)                                             │
├──────────────┬────────┬──────────┬──────────┬─────────┬─────┤
│ Timestamp    │ Action │ Entity   │ Entity   │ Service │ User│
│              │        │ Type     │ ID       │         │     │
├──────────────┼────────┼──────────┼──────────┼─────────┼─────┤
│ 2024-01-15   │ create │ User     │ abc-123  │ Platform│ John│
│ 14:23:45     │        │          │          │         │ Doe │
│              │        │          │          │         │     │
│ 2024-01-15   │ update │ Queue    │ def-456  │ Routing │ Jane│
│ 14:22:10     │        │          │          │         │     │
│    ...       │  ...   │   ...    │   ...    │   ...   │ ... │
└──────────────┴────────┴──────────┴──────────┴─────────┴─────┘
```

## User Experience Flow

### Scenario A: Using Numbers Mode (Existing Workflow - NO CHANGES)
1. User selects "Numbers (Ext/DID)" mode (default)
2. UI shows: Build Context, Run Audit, Rebuild Plan buttons
3. User can select Ext, DID, or Both
4. Everything works exactly as before
5. Export generates Extensions/DIDs report

### Scenario B: Using Audit Logs Mode (NEW)
1. User clicks "Audit Logs" radio button
2. UI transforms to show:
   - Date pickers for interval
   - Filter input fields
   - "Load Services" button
   - "Run Query" button
3. User configures query parameters
4. Clicks "Run Query"
5. Progress indicator shows during query execution
6. Results populate in new audit logs table
7. User can export to Excel with summary

### Scenario C: Switching Between Modes
1. User works in Numbers mode, builds context, runs audit
2. User switches to Audit Logs mode
3. UI immediately updates to show query builder
4. Numbers results are hidden (but preserved)
5. User runs audit logs query
6. User switches back to Numbers mode
7. Original plan results reappear
8. No data lost when switching

## Excel Export Differences

### Numbers Mode Export
```
📊 GenesysExtensionAudit_2024-01-15_1430.xlsx
├── ExecutiveSummary (health score, issue breakdown)
├── Users (all user data)
├── Extensions (all extension data)
└── Issues (detailed issue rows)
```

### Audit Logs Mode Export (NEW)
```
📊 GenesysAuditLogs_2024-01-15_1430.xlsx
├── ExecutiveSummary (top actions, entity types, actors)
├── AuditResults (all audit log rows)
├── AuditQuery (query parameters)
├── AuditTransaction (transaction metadata)
└── AuditSvcMapping (service mapping data)
```

## Empty States

### Numbers Mode - No Results
```
┌─────────────────────────────────────────────────────────────┐
│                                                               │
│                      No results yet                           │
│      Build context, then click Run Audit to                 │
│              populate results.                               │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

### Audit Logs Mode - No Results (NEW)
```
┌─────────────────────────────────────────────────────────────┐
│                                                               │
│                   No audit logs yet                          │
│    Configure query parameters and click Run Query           │
│            to fetch audit logs.                              │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

## Status Messages

### Numbers Mode
- "Building context..." → "Context ready."
- "Computing audit..." → "Audit ready."
- "Exporting..." → "Audit report exported."

### Audit Logs Mode (NEW)
- "Loading service mapping..." → "Service mapping loaded (42 services)."
- "Running audit logs query..." → "Audit logs query complete. 157 events."
- "Posting audit query..." → "Fetching results page 1..." → "Audit logs query complete."
- "Exporting..." → "Audit logs report exported."

## Accessibility

All new UI elements follow existing patterns:
- ✅ Keyboard navigation supported
- ✅ Screen reader friendly labels
- ✅ Consistent spacing and sizing
- ✅ Dark/Light theme support
- ✅ High contrast compatibility

## Responsive Behavior

- UI elements wrap appropriately at different window sizes
- Query builder stacks vertically on narrow windows
- Results table scrolls horizontally if needed
- Status messages truncate with ellipsis

---

**Note**: Since this is a Windows MAUI application that cannot be built on Linux, actual screenshots are not available. The ASCII art diagrams above represent the conceptual layout of the UI changes.
