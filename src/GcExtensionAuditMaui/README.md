# GcExtensionAuditMaui (.NET MAUI / Windows)

Windows rewrite of the PowerShell/WPF **Genesys Audits** tool as a **.NET 8+ / .NET 9** .NET MAUI application using **MVVM**, `async/await`, and `HttpClient`. Supports auditing **Extensions** and **DIDs** (toggle in the UI).

## Run (Windows)

Prereqs:

- .NET SDK with MAUI workload installed (`dotnet workload list` should include `maui` and `maui-windows`)

From the solution root:

- `dotnet build GcExtensionAuditMaui.sln -c Debug -f net9.0-windows10.0.19041.0`
- `dotnet run --project src/GcExtensionAuditMaui/GcExtensionAuditMaui.csproj -c Debug`

Note:

- This project targets Windows only.

Publish (recommended: self-contained, no .NET install required on the target machine):

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\publish-windows.ps1`

Optional (framework-dependent; requires the .NET Desktop Runtime matching the target framework, currently .NET 9):

- `powershell -NoProfile -ExecutionPolicy Bypass -File .\\scripts\\publish-windows.ps1 -FrameworkDependent`

Note:

- The EXE is not standalone; keep the entire publish folder together.
- To reduce size, consider `-WinAppRuntimeDependent` (requires Windows App Runtime installed) and/or `-EnglishOnly`.

Alternative (default publish folder):
- `dotnet publish src\\GcExtensionAuditMaui\\GcExtensionAuditMaui.csproj -c Release`
- Run: `src\\GcExtensionAuditMaui\\bin\\Release\\net9.0-windows10.0.19041.0\\win10-x64\\publish\\GcExtensionAuditMaui.exe`

## App workflow (single-page dashboard)

1) Build Context

- Provide API Base URI + access token (or `GC_ACCESS_TOKEN`) and click **Build Context**.
- The app shows explicit progress stages and produces an in-memory `AuditContext` with:
  - users and profile extensions
  - extension inventory (FULL crawl)
  - lookup dictionaries for fast report generation

2) Run Audit (generate findings + plan)

- Click **Run Audit** to compute:
  - Missing Assignments
  - Discrepancies
  - Duplicate Users
  - Duplicate Extension records (for awareness)
- The app produces a reviewable **Plan** (“Advised Changes”) that drives patch execution.
- Use the defaults (Duplicates/Missing/Discrepancies) and/or select a plan row to override:
  - `ReassertExisting` (affirm/sync attempt)
  - `AssignSpecific` (set a new extension)
  - `ClearExtension` (blank the profile extension)

3) Execute Patch

- Review the plan, then click **EXECUTE PATCH**:
  - `WhatIf=true` shows intended changes without calling PATCH.
  - To run real changes: set `WhatIf=false` and type `PATCH`.

## Token scopes / permissions

This app uses the following endpoints:

- `GET /api/v2/users` and `GET /api/v2/users/{id}`
- `PATCH /api/v2/users/{id}`
- `GET /api/v2/telephony/providers/edges/extensions` (paged)
- `GET /api/v2/telephony/providers/edges/dids` (paged)

Genesys Cloud authorization is governed by the intersection of:

- **OAuth scopes** assigned to the OAuth client (for example, `users` vs `users:readonly`).
- The **user permissions** of the user represented by the access token.

Practical scope guidance:

- Read-only audit: `users:readonly` + `telephony:readonly`
- Patch execution enabled: `users` + `telephony:readonly`

## Extensions inventory fetch strategy

During **Build Context**, the app always crawls the full Extensions inventory paged and then audits that list against the users list.

## Output / exports

Each export creates a timestamped folder:

- Windows: `Documents\\GcExtensionAudit\\yyyyMMdd_HHmmss\\...`

Exports include:

- One or more CSVs for the selected report
- `Snapshot.json` containing the context summary + API stats (total calls, calls by method/path, last error, last rate limit headers)
- Patch runs also export `PatchUpdated.csv`, `PatchSkipped.csv`, `PatchFailed.csv`, `PatchSummary.csv`

## Porting notes (PowerShell parity)

The port preserves the original intent:

- **Profile extension** is extracted from `user.addresses` where `mediaType=PHONE` and `type=WORK` (fallback to any `PHONE` with an extension).
- Patch uses `version + 1` and sends the **full** `addresses` array to avoid unintended overwrites.
- Duplicate logic matches the PowerShell module: duplicates are excluded from Missing/Discrepancies and routed to manual review.

## PowerShell → C# mapping

Reference PowerShell functions (from `GcExtensionAudit.psm1`) map to C# as follows:

- `Invoke-GcApi` → `Services/GenesysCloudApiClient.cs` (`SendAsync<T>`, retry/backoff, rate-limit snapshot + preemptive throttle)
- `Get-GcUsersAll` → `Services/AuditService.cs` (`BuildContextAsync` user paging loop)
- `Get-UserProfileExtension` → `Services/AuditService.cs` (`GetUserProfileExtension`)
- `Get-GcExtensionsAll` → `Services/AuditService.cs` (`BuildContextAsync` extensions paging loop)
- `New-GcExtensionAuditContext` → `Services/AuditService.cs` (`BuildContextAsync`)
- `Find-DuplicateUserExtensionAssignments` → `Services/AuditService.cs` (`FindDuplicateUserExtensionAssignments`)
- `Find-DuplicateExtensionRecords` → `Services/AuditService.cs` (`FindDuplicateExtensionRecords`)
- `Find-ExtensionDiscrepancies` → `Services/AuditService.cs` (`FindExtensionDiscrepancies`)
- `Find-MissingExtensionAssignments` → `Services/AuditService.cs` (`FindMissingExtensionAssignments`)
- `New-ExtensionDryRunReport` → `Services/AuditService.cs` (`NewDryRunReport`)
- `Patch-MissingExtensionAssignments` → `Services/AuditService.cs` (`PatchMissingAsync`)
- `Export-ReportCsv` → `Services/ExportService.cs`
