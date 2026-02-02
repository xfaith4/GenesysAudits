# Genesys Cloud Extension Audit & Repair

Audit Genesys Cloud user profile **WORK PHONE** **extensions** or **DIDs** against the Telephony inventory, generate reviewable reports, and (optionally) repair **missing assignments** by re-asserting the number on the user profile with the required `version` bump.

This addon includes:

- A **Windows** **.NET MAUI** app in `src/GcExtensionAuditMaui/`
- The original **PowerShell module + WPF UI** (kept for reference/compatibility)

---

## .NET MAUI app (recommended)

### Build / Run (Windows)

Prereqs:

- .NET SDK with MAUI workloads (`maui`, `maui-windows`)

Commands (from `Addons/ExtensionAudit`):

- `dotnet build GcExtensionAuditMaui.sln -c Debug -f net9.0-windows10.0.19041.0`
- `dotnet run --project src/GcExtensionAuditMaui/GcExtensionAuditMaui.csproj -c Debug`

Note:

- This project is Windows-only, so you should not see Android SDK build errors.

### Publish to an EXE (Windows)

Yes — you publish this as a normal Windows folder containing:

- `GcExtensionAuditMaui.exe`
- supporting `.dll` files + assets

The EXE is not standalone: to run it, keep the whole publish folder together.

Publish (recommended: self-contained, no .NET install required on the target machine):

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\publish-windows.ps1`

Optional (framework-dependent; requires the .NET Desktop Runtime matching the target framework, currently .NET 9):

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\publish-windows.ps1 -FrameworkDependent`

Size reduction options:

- Remove non-English satellite resources: add `-EnglishOnly` (small win; a few MB).
- Keep .NET self-contained but require Windows App Runtime on the target machine: add `-WinAppRuntimeDependent` (big win).
- Smallest output (requires both .NET runtime + Windows App Runtime installed): use `-FrameworkDependent`.

Alternative (default publish folder):

- `dotnet publish -c Release`
- Run: `src\\GcExtensionAuditMaui\\bin\\Release\\net9.0-windows10.0.19041.0\\win10-x64\\publish\\GcExtensionAuditMaui.exe`

### App workflow (single-page dashboard)

1) **Connection / Context**

- Inputs:
  - API Base URI (default `https://api.usw2.pure.cloud`)
  - Access token (secure entry) or “Use `GC_ACCESS_TOKEN` from environment”
  - “Include inactive users”
- **Build Context** (async + cancelable) with explicit stages:
  - Fetch users (paged)
  - Toggle **Audit: Ext / DID**
  - Extract profile value from `user.addresses` where `mediaType=PHONE` and `type=WORK` (fallback to any `PHONE`)
  - Fetch extensions (paged, FULL crawl)
- Shows a “Context Summary” (users totals, distinct extensions, loaded extensions, mode)

1) **Generate findings + plan**

- Click **Run Audit** to compute:
  - Missing Assignments
  - Discrepancies
  - Duplicate Users
  - Duplicate Extension records (for awareness)
- The app then produces a reviewable **Plan** (“Advised Changes”) that drives patch execution.
- Use the **defaults** (Duplicates/Missing/Discrepancies) and/or select a plan row to override:
  - `ReassertExisting` (affirm/sync attempt)
  - `AssignSpecific` (set a new extension)
  - `ClearExtension` (blank the profile extension)

1) **Execute patch**

- Two patch modes available:
  - **PatchMissing** (legacy): Only fixes missing extension assignments
  - **PatchPlan** (comprehensive): Fixes all selected issue types from the plan

- **PatchPlan (Comprehensive)** features:
  - **Category Selection**: Choose which issue types to fix:
    - ☑️ Missing Extensions - Users with profile extension but no extension record exists
    - ☑️ Duplicate Users - Multiple users assigned to the same extension
    - ☑️ Discrepancies - Extension record exists but wrong owner/type mismatch
    - ☐ Reassert Consistent - Optional: Reassert users that look consistent (for sync verification)
  - **Double Verification**: When running real changes (WhatIf=false):
    - First confirmation dialog: Warns about applying REAL changes
    - Second confirmation dialog: FINAL warning before execution
    - Must type `PATCH` in the confirmation field
  - **Selective Fixing**: Can fix one issue type at a time or any combination
  - **Action Types Supported**:
    - `ReassertExisting` - Affirm/sync the current extension (for discrepancies)
    - `AssignSpecific` - Set a new extension (for duplicates/missing with available extensions)
    - `ClearExtension` - Blank the profile extension (when no better option available)

- Options:
  - `WhatIf` (default true) - Preview changes without applying them
  - Sleep (ms) between updates (default 150)
  - MaxUpdates (0 = unlimited)
  - MaxFailures (0 = unlimited)
  - Confirm text (must type `PATCH` when `WhatIf=false`)
  
- Patch semantics (matches PowerShell module intent):
  - `GET /api/v2/users/{id}`
  - Ensure a WORK PHONE address exists (creates one if absent)
  - Set `addresses[idx].extension = <target number>`
  - `PATCH /api/v2/users/{id}` with:
    - `addresses` = full addresses array (complete)
    - `version` = `user.version + 1`

1) **Log**

- Live, non-blocking in-app log view (bounded buffer + throttled UI updates)
- Logs also write to a file on disk
- “Open Log” / “Clear View” / “Copy Diagnostics” / Auto-scroll

### Exports / Output

Each export creates a timestamped output folder:

- Windows: `Documents\\GcExtensionAudit\\yyyyMMdd_HHmmss\\...`

Exports include:

- Dry-run audit report CSVs
- `Snapshot.json` with context summary + API stats (calls by method/path, last error, last rate limit snapshot)

### API endpoints used

- `GET /api/v2/users?pageSize={N}&pageNumber={p}[&state=active]`
- `GET /api/v2/users/{id}`
- `PATCH /api/v2/users/{id}`
- `GET /api/v2/telephony/providers/edges/extensions?pageSize={N}&pageNumber={p}`
- `GET /api/v2/telephony/providers/edges/dids?pageSize={N}&pageNumber={p}`

Authentication:

- `Authorization: Bearer <token>`

See `src/GcExtensionAuditMaui/README.md` for implementation details and PowerShell→C# function mapping.

---

## PowerShell (legacy / reference)

The original implementation remains here:

- Module: `GcExtensionAudit.psm1`
- WPF runner: `GcExtensionAuditUI.ps1` + `GcExtensionAuditUI.xaml`
- CLI menu: `GcExtensionAuditMenu.ps1`

Quick start (WPF UI):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\GcExtensionAuditUI.ps1
```

---

## Repository layout

```text
.
├── GcExtensionAuditMaui.sln
├── src/
│   └── GcExtensionAuditMaui/           # .NET MAUI app (MVVM)
├── GcExtensionAudit.psm1               # PowerShell module (reference)
├── GcExtensionAuditMenu.ps1            # PowerShell CLI menu (reference)
├── GcExtensionAuditUI.ps1              # PowerShell WPF UI runner (reference)
└── GcExtensionAuditUI.xaml             # PowerShell WPF UI layout (reference)
