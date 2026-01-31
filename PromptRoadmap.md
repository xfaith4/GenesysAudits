# PromptRoadmap.md

Tracks remaining “publish-ready” work. Items marked completed were implemented; anything still outstanding has a ready-to-run prompt.

---

## Status summary

- Completed: 2, 3, 4, 5, 9, 10, 11
- Partially completed: 6
- Outstanding: 1, 7, 8

---

## 1) MSIX packaging + signing + versioning (OUTSTANDING)

**Goal:** produce a clean, installable, updatable Windows package with proper identity, signing, and semantic versioning.

Prompt:
> Update the MAUI Windows packaging so the app can be published as MSIX. Configure app identity, publisher, display name, icons, and versioning. Add a Release publish profile and document the exact publish commands. Ensure the output is a signed MSIX (self-signed acceptable for internal). Update `src/GcExtensionAuditMaui/Platforms/Windows/Package.appxmanifest` and project settings as needed.

---

## 2) End-user friendly “Publish output” flow (COMPLETED)

- Implemented: `scripts/publish-windows.ps1`
- Produces a deterministic `dist/` folder output.

---

## 3) Tests for audit logic (golden-data) (COMPLETED)

- Implemented: `tests/GcExtensionAuditMaui.Tests/AuditServiceTests.cs`
- Wired into solution: `GcExtensionAuditMaui.sln`

---

## 4) HTTP resilience / diagnostics (COMPLETED)

- Added support headers capture (request/correlation IDs) + rate-limit capture:
  - `src/GcExtensionAuditMaui/Services/GenesysCloudApiClient.cs`
  - `src/GcExtensionAuditMaui/Models/Observability/ApiStats.cs`
- Added “Copy Diagnostics” button:
  - `src/GcExtensionAuditMaui/ViewModels/LogViewModel.cs`
  - `src/GcExtensionAuditMaui/Views/LogPage.xaml`

---

## 5) Better token handling (COMPLETED)

- Accepts `Bearer ...` pasted tokens (normalizes to raw token) + “Paste” button on Home:
  - `src/GcExtensionAuditMaui/ViewModels/ConnectionViewModel.cs`
  - `src/GcExtensionAuditMaui/Views/HomePage.xaml`
- Friendlier permission errors for Users fetch:
  - `src/GcExtensionAuditMaui/Services/AuditService.cs`

---

## 6) Large dataset UX (search/filter/sort) (PARTIAL)

Implemented:

- Simple text filter boxes on report tabs (client-side filtering without recomputing context).

Outstanding:

- Sorting (column header sort) and richer filters (dropdowns for Issue/Category).

Prompt (sorting + richer filters):
> Add column sorting and structured filtering to all report tabs. Implement: sort by tapping column headers (Windows), Issue/Category dropdowns, and fast filtering over the existing in-memory report rows. Keep filtering local (do not rebuild context).

---

## 7) Windows DataGrid (better than CollectionView columns) (OUTSTANDING)

Prompt:
> Replace the “fake grid” CollectionView layouts with a real Windows DataGrid control (choose a stable, permissive package). Implement column definitions matching the WPF app, enable sorting and copy-to-clipboard, and document the dependency choice in the README.

---

## 8) Export UX improvements (OUTSTANDING)

Prompt:
> Implement a Windows folder picker for export destination (default to Documents\\GcExtensionAudit\\timestamp). Show a dialog on success with buttons: “Open Folder”, “Open CSV”, “Open Snapshot”. Keep current automatic default for quick usage.

---

## 9) Patch safety + review screen (COMPLETED)

- Patch preview + counts, required confirm, max failures, and automatic patch exports:
  - `src/GcExtensionAuditMaui/ViewModels/PatchMissingViewModel.cs`
  - `src/GcExtensionAuditMaui/Views/PatchMissingPage.xaml`
  - `src/GcExtensionAuditMaui/Services/ExportService.cs`
  - `src/GcExtensionAuditMaui/Services/DialogService.cs`

---

## 10) CI build + static analysis (COMPLETED)

- Added GitHub Actions Windows build + test workflow:
  - `.github/workflows/windows-build.yml`

---

## 11) Single-page “Windows dashboard” UX (COMPLETED)

**Goal:** a lighter Windows-first workflow that feels like a typical desktop app: one page, clear action buttons, a reviewable plan, and a highlighted “EXECUTE PATCH” call-to-action.

Implemented:

- Replaced the old tabbed UI with a single dashboard:
  - `src/GcExtensionAuditMaui/Views/DashboardPage.xaml`
  - `src/GcExtensionAuditMaui/ViewModels/DashboardViewModel.cs`
- Added plan defaults and per-item overrides:
  - Defaults: Duplicates / Missing / Discrepancies
  - Per-row edit: choose action + pick/enter target extension
- Embedded a bounded, throttled live log on the dashboard (no UI freezing):
  - `src/GcExtensionAuditMaui/Services/LoggingService.cs`
