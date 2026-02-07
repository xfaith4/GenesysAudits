using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcExtensionAuditMaui.Models.Audit;
using GcExtensionAuditMaui.Models.Logging;
using GcExtensionAuditMaui.Models.Planning;
using GcExtensionAuditMaui.Models.Patch;
using GcExtensionAuditMaui.Services;
using GcExtensionAuditMaui.Utilities;
using GcExtensionAuditMaui.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Storage;
using System.Text;
using System.Text.Json;

namespace GcExtensionAuditMaui.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly AuditService _audit;
    private readonly FixupPlannerService _planner;
    private readonly ExportService _export;
    private readonly ReportModule _reportModule;
    private readonly ContextStore _store;
    private readonly DialogService _dialogs;
    private readonly PlatformOpenService _open;
    private readonly LoggingService _log;
    private readonly IServiceProvider _services;

    private CancellationTokenSource? _cts;
    private FixupPlan? _plan;

    public DashboardViewModel(
        AuditService audit,
        FixupPlannerService planner,
        ExportService export,
        ReportModule reportModule,
        ContextStore store,
        DialogService dialogs,
        PlatformOpenService open,
        LoggingService log,
        IServiceProvider services)
    {
        _audit = audit;
        _planner = planner;
        _export = export;
        _reportModule = reportModule;
        _store = store;
        _dialogs = dialogs;
        _open = open;
        _log = log;
        _services = services;

        AuditKind = (AuditNumberKind)Preferences.Get(nameof(AuditKind), (int)AuditNumberKind.Extension);
        RunBothAudits = Preferences.Get(nameof(RunBothAudits), false);
        ApiBaseUri = Preferences.Get(nameof(ApiBaseUri), "https://api.usw2.pure.cloud");
        UseEnvToken = Preferences.Get(nameof(UseEnvToken), false);
        IncludeInactive = Preferences.Get(nameof(IncludeInactive), false);

        WhatIf = Preferences.Get(nameof(WhatIf), true);
        SleepMsBetween = Preferences.Get(nameof(SleepMsBetween), 150);
        MaxUpdates = Preferences.Get(nameof(MaxUpdates), 0);
        MaxFailures = Preferences.Get(nameof(MaxFailures), 0);

        PreferAssignAvailable = Preferences.Get(nameof(PreferAssignAvailable), true);
        ReassertConsistentUsers = Preferences.Get(nameof(ReassertConsistentUsers), false);

        DuplicateDefault = Preferences.Get(nameof(DuplicateDefault), "Assign");
        MissingDefault = Preferences.Get(nameof(MissingDefault), "Assign");
        DiscrepancyDefault = Preferences.Get(nameof(DiscrepancyDefault), "Reassert");

        AutoScrollLog = Preferences.Get(nameof(AutoScrollLog), true);
        IsLogExpanded = Preferences.Get(nameof(IsLogExpanded), true);
    }

    // Connection / context
    private AuditNumberKind _auditKind = AuditNumberKind.Extension;
    public AuditNumberKind AuditKind
    {
        get => _auditKind;
        set
        {
            if (IsBusy) { return; }
            if (SetProperty(ref _auditKind, value))
            {
                Preferences.Set(nameof(AuditKind), (int)value);
                OnPropertyChanged(nameof(IsAuditExtensions));
                OnPropertyChanged(nameof(IsAuditDids));
                OnPropertyChanged(nameof(AuditTitle));
                ResetForAuditKindChange();
            }
        }
    }

    public bool IsAuditExtensions
    {
        get => AuditKind == AuditNumberKind.Extension;
        set { if (value) { AuditKind = AuditNumberKind.Extension; } }
    }

    public bool IsAuditDids
    {
        get => AuditKind == AuditNumberKind.Did;
        set { if (value) { AuditKind = AuditNumberKind.Did; } }
    }

    private bool _runBothAudits;
    public bool RunBothAudits
    {
        get => _runBothAudits;
        set
        {
            if (IsBusy) { return; }
            if (SetProperty(ref _runBothAudits, value))
            {
                Preferences.Set(nameof(RunBothAudits), value);
                OnPropertyChanged(nameof(AuditTitle));
            }
        }
    }

    public string AuditTitle
        => RunBothAudits ? "Genesys Audits - Combined (Extensions + DIDs)" 
            : (AuditKind == AuditNumberKind.Did ? "Genesys Audits - DID" : "Genesys Audits - Extension");

    private string _apiBaseUri = "";
    public string ApiBaseUri
    {
        get => _apiBaseUri;
        set
        {
            if (SetProperty(ref _apiBaseUri, value ?? ""))
            {
                Preferences.Set(nameof(ApiBaseUri), value ?? "");
            }
        }
    }

    private string _accessToken = "";
    public string AccessToken
    {
        get => _accessToken;
        set => SetProperty(ref _accessToken, value ?? "");
    }

    private bool _useEnvToken;
    public bool UseEnvToken
    {
        get => _useEnvToken;
        set
        {
            if (SetProperty(ref _useEnvToken, value))
            {
                Preferences.Set(nameof(UseEnvToken), value);
            }
        }
    }

    private bool _includeInactive;
    public bool IncludeInactive
    {
        get => _includeInactive;
        set
        {
            if (SetProperty(ref _includeInactive, value))
            {
                Preferences.Set(nameof(IncludeInactive), value);
            }
        }
    }

    // Planning toggles
    private bool _preferAssignAvailable = true;
    public bool PreferAssignAvailable
    {
        get => _preferAssignAvailable;
        set
        {
            if (SetProperty(ref _preferAssignAvailable, value))
            {
                Preferences.Set(nameof(PreferAssignAvailable), value);
            }
        }
    }

    private bool _reassertConsistentUsers;
    public bool ReassertConsistentUsers
    {
        get => _reassertConsistentUsers;
        set
        {
            if (SetProperty(ref _reassertConsistentUsers, value))
            {
                Preferences.Set(nameof(ReassertConsistentUsers), value);
            }
        }
    }

    private bool _autoScrollLog = true;
    public bool AutoScrollLog
    {
        get => _autoScrollLog;
        set
        {
            if (SetProperty(ref _autoScrollLog, value))
            {
                Preferences.Set(nameof(AutoScrollLog), value);
            }
        }
    }

    private bool _isLogExpanded = true;
    public bool IsLogExpanded
    {
        get => _isLogExpanded;
        set
        {
            if (SetProperty(ref _isLogExpanded, value))
            {
                Preferences.Set(nameof(IsLogExpanded), value);
            }
        }
    }

    private DateTime? _lastContextAt;
    public DateTime? LastContextAt
    {
        get => _lastContextAt;
        set => SetProperty(ref _lastContextAt, value);
    }

    private DateTime? _lastAuditAt;
    public DateTime? LastAuditAt
    {
        get => _lastAuditAt;
        set => SetProperty(ref _lastAuditAt, value);
    }

    // Execution options
    private bool _whatIf = true;
    public bool WhatIf
    {
        get => _whatIf;
        set
        {
            if (SetProperty(ref _whatIf, value))
            {
                Preferences.Set(nameof(WhatIf), value);
                OnPropertyChanged(nameof(CanExecuteReal));
            }
        }
    }

    private int _sleepMsBetween = 150;
    public int SleepMsBetween
    {
        get => _sleepMsBetween;
        set
        {
            // Ensure non-negative values only
            var sanitized = Math.Max(0, value);
            if (sanitized != value)
            {
                _log.Log(LogLevel.Warn, $"Sleep time must be non-negative. Adjusted from {value} to {sanitized}.");
            }
            if (SetProperty(ref _sleepMsBetween, sanitized))
            {
                Preferences.Set(nameof(SleepMsBetween), sanitized);
            }
        }
    }

    private int _maxUpdates;
    public int MaxUpdates
    {
        get => _maxUpdates;
        set
        {
            // Ensure non-negative values only
            var sanitized = Math.Max(0, value);
            if (sanitized != value)
            {
                _log.Log(LogLevel.Warn, $"Max updates must be non-negative. Adjusted from {value} to {sanitized}.");
            }
            if (SetProperty(ref _maxUpdates, sanitized))
            {
                Preferences.Set(nameof(MaxUpdates), sanitized);
            }
        }
    }

    private int _maxFailures;
    public int MaxFailures
    {
        get => _maxFailures;
        set
        {
            // Ensure non-negative values only
            var sanitized = Math.Max(0, value);
            if (sanitized != value)
            {
                _log.Log(LogLevel.Warn, $"Max failures must be non-negative. Adjusted from {value} to {sanitized}.");
            }
            if (SetProperty(ref _maxFailures, sanitized))
            {
                Preferences.Set(nameof(MaxFailures), sanitized);
            }
        }
    }

    private string _confirmText = "";
    public string ConfirmText
    {
        get => _confirmText;
        set
        {
            if (SetProperty(ref _confirmText, value ?? ""))
            {
                OnPropertyChanged(nameof(CanExecuteReal));
            }
        }
    }

    public bool CanExecuteReal => !WhatIf && string.Equals(ConfirmText, "PATCH", StringComparison.Ordinal);

    public IReadOnlyList<string> DuplicateDefaultOptions { get; } = new[] { "Assign", "Blank" };
    public IReadOnlyList<string> MissingDefaultOptions { get; } = new[] { "Assign", "Blank" };
    public IReadOnlyList<string> DiscrepancyDefaultOptions { get; } = new[] { "Reassert", "Assign", "Blank" };

    public IReadOnlyList<FixupActionType> FixupActionOptions { get; } = new[]
    {
        FixupActionType.ReassertExisting,
        FixupActionType.AssignSpecific,
        FixupActionType.ClearExtension,
    };

    private string _duplicateDefault = "Assign";
    public string DuplicateDefault
    {
        get => _duplicateDefault;
        set
        {
            if (SetProperty(ref _duplicateDefault, value ?? "Assign"))
            {
                Preferences.Set(nameof(DuplicateDefault), _duplicateDefault);
                ApplyDefaultsToPlan();
            }
        }
    }

    private string _missingDefault = "Assign";
    public string MissingDefault
    {
        get => _missingDefault;
        set
        {
            if (SetProperty(ref _missingDefault, value ?? "Assign"))
            {
                Preferences.Set(nameof(MissingDefault), _missingDefault);
                ApplyDefaultsToPlan();
            }
        }
    }

    private string _discrepancyDefault = "Reassert";
    public string DiscrepancyDefault
    {
        get => _discrepancyDefault;
        set
        {
            if (SetProperty(ref _discrepancyDefault, value ?? "Reassert"))
            {
                Preferences.Set(nameof(DiscrepancyDefault), _discrepancyDefault);
                ApplyDefaultsToPlan();
            }
        }
    }

    // UI state
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                BuildContextCommand.NotifyCanExecuteChanged();
                RunAuditCommand.NotifyCanExecuteChanged();
                ExecuteCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
                ExportAuditReportCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value ?? "");
    }

    private string _contextSummaryText = "";
    public string ContextSummaryText
    {
        get => _contextSummaryText;
        set => SetProperty(ref _contextSummaryText, value ?? "");
    }

    private string _auditSummaryText = "";
    public string AuditSummaryText
    {
        get => _auditSummaryText;
        set => SetProperty(ref _auditSummaryText, value ?? "");
    }

    private string _planSummaryText = "";
    public string PlanSummaryText
    {
        get => _planSummaryText;
        set => SetProperty(ref _planSummaryText, value ?? "");
    }

    private string _lastOutputFolder = "";
    public string LastOutputFolder
    {
        get => _lastOutputFolder;
        set => SetProperty(ref _lastOutputFolder, value ?? "");
    }

    public ObservableRangeCollection<FixupItem> PlanItems { get; } = new();
    public ObservableCollection<string> AvailableExtensions { get; } = new();

    public ObservableCollection<LogEntry> LogEntries => _log.Entries;

    private string _planFilterText = "";
    public string PlanFilterText
    {
        get => _planFilterText;
        set
        {
            if (SetProperty(ref _planFilterText, value ?? ""))
            {
                RefreshPlanView();
            }
        }
    }

    private FixupItem? _selectedPlanItem;
    public FixupItem? SelectedPlanItem
    {
        get => _selectedPlanItem;
        set
        {
            if (SetProperty(ref _selectedPlanItem, value))
            {
                OnPropertyChanged(nameof(SelectedAction));
                OnPropertyChanged(nameof(HasSelectedPlanItem));
                PickNextAvailableCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasSelectedPlanItem => SelectedPlanItem is not null;

    private int _planItemsCount;
    public int PlanItemsCount
    {
        get => _planItemsCount;
        private set
        {
            if (SetProperty(ref _planItemsCount, value))
            {
                OnPropertyChanged(nameof(HasPlanItems));
            }
        }
    }

    public bool HasPlanItems => PlanItemsCount > 0;

    public FixupActionType SelectedAction
    {
        get => SelectedPlanItem?.Action ?? FixupActionType.None;
        set
        {
            if (SelectedPlanItem is null) { return; }

            SelectedPlanItem.Action = value;
            if (value != FixupActionType.AssignSpecific)
            {
                SelectedPlanItem.RecommendedExtension = null;
            }
            else if (string.IsNullOrWhiteSpace(SelectedPlanItem.RecommendedExtension))
            {
                TryPickNextAvailableForSelected();
            }

            RefreshPlanView();
        }
    }

    private void ResetForAuditKindChange()
    {
        _store.Clear();
        _plan = null;

        ContextSummaryText = "";
        AuditSummaryText = "";
        PlanSummaryText = "";
        StatusText = "Ready.";

        AvailableExtensions.Clear();
        PlanItems.Clear();
        PlanItemsCount = 0;
        SelectedPlanItem = null;

        LastContextAt = null;
        LastAuditAt = null;
        LastOutputFolder = "";
    }

    [RelayCommand(CanExecute = nameof(CanBuildContext))]
    private async Task BuildContextAsync()
    {
        IsBusy = true;
        StatusText = "Building context…";
        ContextSummaryText = "";
        _cts = new CancellationTokenSource();

        var token = NormalizeToken(UseEnvToken ? (Environment.GetEnvironmentVariable("GC_ACCESS_TOKEN") ?? "") : AccessToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            IsBusy = false;
            StatusText = "Access token is required.";
            return;
        }

        var progress = new Progress<string>(s => StatusText = s);

        try
        {
            if (RunBothAudits)
            {
                // Build Extension context
                StatusText = "Building Extension context…";
                var extCtx = await _audit.BuildContextAsync(
                    auditKind: AuditNumberKind.Extension,
                    apiBaseUri: ApiBaseUri,
                    accessToken: token,
                    includeInactive: IncludeInactive,
                    usersPageSize: AuditService.DefaultUsersPageSize,
                    extensionsPageSize: AuditService.DefaultExtensionsPageSize,
                    maxFullExtensionPages: 25,
                    progress: progress,
                    ct: _cts.Token);

                _store.ExtensionContext = extCtx;

                // Build DID context
                StatusText = "Building DID context…";
                var didCtx = await _audit.BuildContextAsync(
                    auditKind: AuditNumberKind.Did,
                    apiBaseUri: ApiBaseUri,
                    accessToken: token,
                    includeInactive: IncludeInactive,
                    usersPageSize: AuditService.DefaultUsersPageSize,
                    extensionsPageSize: AuditService.DefaultExtensionsPageSize,
                    maxFullExtensionPages: 25,
                    progress: progress,
                    ct: _cts.Token);

                _store.DidContext = didCtx;
                
                // Set Context to ExtensionContext for compatibility with UI
                _store.Context = extCtx;
                _store.Summary = _audit.GetSummary(extCtx);
                
                var extSummary = _audit.GetSummary(extCtx);
                var didSummary = _audit.GetSummary(didCtx);
                ContextSummaryText = $"Extensions: {extSummary}; DIDs: {didSummary}";
            }
            else
            {
                var ctx = await _audit.BuildContextAsync(
                    auditKind: AuditKind,
                    apiBaseUri: ApiBaseUri,
                    accessToken: token,
                    includeInactive: IncludeInactive,
                    usersPageSize: AuditService.DefaultUsersPageSize,
                    extensionsPageSize: AuditService.DefaultExtensionsPageSize,
                    maxFullExtensionPages: 25,
                    progress: progress,
                    ct: _cts.Token);

                _store.Context = ctx;
                _store.Summary = _audit.GetSummary(ctx);
                ContextSummaryText = _store.Summary.ToString();
            }

            LastContextAt = DateTime.Now;
            StatusText = "Context ready.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Canceled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Build context failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanBuildContext() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRunAudit))]
    private async Task RunAuditAsync()
    {
        if (_store.Context is null)
        {
            await BuildContextAsync();
            if (_store.Context is null) { return; }
        }

        IsBusy = true;
        StatusText = "Computing audit…";
        try
        {
            var ctx = _store.Context!;
            await Task.Run(() =>
            {
                var missing = _audit.FindMissingExtensionAssignments(ctx);
                var disc = _audit.FindExtensionDiscrepancies(ctx);
                var dupUsers = _audit.FindDuplicateUserExtensionAssignments(ctx);
                var dupExts = _audit.FindDuplicateExtensionRecords(ctx);

                var summary =
                    $"Missing={missing.Count}; Discrepancies={disc.Count}; DuplicateUsersRows={dupUsers.Count}; DuplicateExtRows={dupExts.Count}";

                _plan = _planner.BuildPlan(ctx, reassertConsistentUsers: ReassertConsistentUsers, preferAssignAvailableOverBlank: PreferAssignAvailable);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AuditSummaryText = summary;
                    PlanSummaryText = _plan.SummaryText;
                    AvailableExtensions.Clear();
                    foreach (var n in _plan.AvailableExtensionNumbers) { AvailableExtensions.Add(n); }
                    SelectedPlanItem = null;
                    ApplyDefaultsToPlan();
                    LastAuditAt = DateTime.Now;
                });
            });

            StatusText = "Audit ready. Review plan and execute when ready.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRunAudit() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRunAudit))]
    private async Task RebuildPlanAsync()
    {
        if (_store.Context is null)
        {
            StatusText = "Context not built.";
            return;
        }

        IsBusy = true;
        StatusText = "Rebuilding plan…";
        try
        {
            var ctx = _store.Context!;
            await Task.Run(() =>
            {
                _plan = _planner.BuildPlan(ctx, reassertConsistentUsers: ReassertConsistentUsers, preferAssignAvailableOverBlank: PreferAssignAvailable);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PlanSummaryText = _plan.SummaryText;
                    AvailableExtensions.Clear();
                    foreach (var n in _plan.AvailableExtensionNumbers) { AvailableExtensions.Add(n); }
                    SelectedPlanItem = null;
                    ApplyDefaultsToPlan();
                });
            });
            StatusText = "Plan rebuilt.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ShowAll()
        => PlanFilterText = "";

    [RelayCommand]
    private void ShowMissing()
        => PlanFilterText = "Missing";

    [RelayCommand]
    private void ShowDuplicates()
        => PlanFilterText = "Duplicate";

    [RelayCommand]
    private void ShowDiscrepancies()
        => PlanFilterText = "Discrepancy";

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync()
    {
        if (_store.Context is null)
        {
            StatusText = "Context not built.";
            return;
        }

        if (!WhatIf && !CanExecuteReal)
        {
            StatusText = "To run real changes: uncheck WhatIf and type PATCH in Confirm.";
            return;
        }

        if (_plan is null)
        {
            StatusText = "No plan yet. Click Run Audit first.";
            return;
        }

        if (!WhatIf)
        {
            var ok = await _dialogs.ConfirmAsync("Confirm Execute", $"You are about to apply real changes.\n\n{PlanSummaryText}\n\nContinue?", accept: "PATCH", cancel: "Cancel");
            if (!ok)
            {
                StatusText = "Canceled.";
                return;
            }
        }

        IsBusy = true;
        StatusText = WhatIf ? "Executing (WhatIf)…" : "Executing (REAL)…";
        _cts = new CancellationTokenSource();

        try
        {
            var ctx = _store.Context!;
            var options = new PatchOptions
            {
                WhatIf = WhatIf,
                SleepMsBetween = Math.Max(0, SleepMsBetween),
                MaxUpdates = Math.Max(0, MaxUpdates),
                MaxFailures = Math.Max(0, MaxFailures),
            };

            var updated = new List<PatchUpdatedRow>();
            var skipped = new List<PatchSkippedRow>();
            var failed = new List<PatchFailedRow>();

            var done = 0;
            var i = 0;

            foreach (var item in _plan.Items)
            {
                _cts.Token.ThrowIfCancellationRequested();
                i++;

                if (options.MaxFailures > 0 && failed.Count >= options.MaxFailures)
                {
                    skipped.Add(new PatchSkippedRow { Reason = "MaxFailuresReached", UserId = item.UserId, User = item.User, Extension = item.CurrentExtension ?? "" });
                    continue;
                }

                if (options.MaxUpdates > 0 && done >= options.MaxUpdates)
                {
                    skipped.Add(new PatchSkippedRow { Reason = "MaxUpdatesReached", UserId = item.UserId, User = item.User, Extension = item.CurrentExtension ?? "" });
                    continue;
                }

                string? target = item.Action switch
                {
                    FixupActionType.ReassertExisting => item.CurrentExtension,
                    FixupActionType.AssignSpecific => item.RecommendedExtension,
                    FixupActionType.ClearExtension => null,
                    _ => item.CurrentExtension,
                };

                if (item.Action == FixupActionType.AssignSpecific && string.IsNullOrWhiteSpace(target))
                {
                    skipped.Add(new PatchSkippedRow { Reason = "NoTargetExtension", UserId = item.UserId, User = item.User, Extension = item.CurrentExtension ?? "" });
                    continue;
                }

                try
                {
                    StatusText = $"Patching {i}/{_plan.Items.Count}: {item.User}";
                    var (status, version) = await _audit.PatchUserExtensionAsync(ctx, item.UserId, target, whatIf: options.WhatIf, ct: _cts.Token);

                    updated.Add(new PatchUpdatedRow
                    {
                        UserId = item.UserId,
                        User = item.User,
                        Extension = target ?? "",
                        Status = status,
                        PatchedVersion = version,
                    });

                    done++;
                    if (options.SleepMsBetween > 0)
                    {
                        await Task.Delay(options.SleepMsBetween, _cts.Token);
                    }
                }
                catch (Exception ex)
                {
                    failed.Add(new PatchFailedRow { UserId = item.UserId, User = item.User, Extension = item.CurrentExtension ?? "", Error = ex.Message });
                }
            }

            var result = new PatchResult
            {
                Summary = new PatchSummary
                {
                    MissingFound = _audit.FindMissingExtensionAssignments(ctx).Count,
                    Updated = updated.Count,
                    Skipped = skipped.Count,
                    Failed = failed.Count,
                    WhatIf = options.WhatIf,
                },
                Updated = updated,
                Skipped = skipped,
                Failed = failed,
            };

            var outDir = await _export.ExportPatchAsync(ctx, result, _audit.Api.Stats, CancellationToken.None);
            _store.LastOutputFolder = outDir;
            LastOutputFolder = outDir;

            StatusText = "Execute complete (exported).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Canceled.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExecute() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanCancel() => IsBusy;

    [RelayCommand]
    private async Task ExportAuditReportAsync()
    {
        if (_store.Context is null)
        {
            StatusText = "Context not built.";
            return;
        }

        IsBusy = true;
        try
        {
            if (RunBothAudits && _store.ExtensionContext is not null && _store.DidContext is not null)
            {
                // Export combined audit report
                var extReport = await Task.Run(() => _audit.NewDryRunReport(_store.ExtensionContext));
                var didReport = await Task.Run(() => _audit.NewDryRunReport(_store.DidContext));
                var outDir = await _reportModule.ExportCombinedAuditReportAsync(
                    _store.ExtensionContext, 
                    _store.DidContext, 
                    extReport, 
                    didReport, 
                    _audit.Api.Stats, 
                    CancellationToken.None);
                _store.LastOutputFolder = outDir;
                LastOutputFolder = outDir;
                StatusText = "Combined audit report exported.";
                
                await _dialogs.AlertAsync("Export Successful", $"Combined report exported successfully to:\n{outDir}");
            }
            else
            {
                // Export single audit report
                var report = await Task.Run(() => _audit.NewDryRunReport(_store.Context));
                var outDir = await _reportModule.ExportFullAuditReportAsync(_store.Context, report, _audit.Api.Stats, CancellationToken.None);
                _store.LastOutputFolder = outDir;
                LastOutputFolder = outDir;
                StatusText = "Audit report exported.";
                
                await _dialogs.AlertAsync("Export Successful", $"Report exported successfully to:\n{outDir}");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenOutAsync()
    {
        if (!string.IsNullOrWhiteSpace(_store.LastOutputFolder))
        {
            await _open.OpenFolderAsync(_store.LastOutputFolder);
        }
    }

    [RelayCommand]
    private async Task ViewSummaryAsync()
    {
        try
        {
            var summaryPage = _services.GetRequiredService<SummaryPage>();
            await Application.Current.MainPage.Navigation.PushModalAsync(summaryPage);
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, "Failed to show summary page", ex: ex);
            await _dialogs.AlertAsync("Error", $"Failed to open summary page: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenLogAsync()
    {
        if (!string.IsNullOrWhiteSpace(_log.LogPath))
        {
            await _open.OpenFileAsync(_log.LogPath);
        }
    }

    [RelayCommand]
    private void ClearLogView()
    {
        _log.ClearView();
    }

    [RelayCommand]
    private async Task CopyDiagnosticsAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"GeneratedAt: {DateTime.Now:O}");
        sb.AppendLine($"ContextSummary: {_store.Summary?.ToString() ?? "(none)"}");
        sb.AppendLine("ApiStats:");
        sb.AppendLine(JsonSerializer.Serialize(_audit.Api.Stats.ToSnapshotObject(), new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        sb.AppendLine("LastLogLines:");
        foreach (var e in LogEntries.TakeLast(50))
        {
            sb.AppendLine($"[{e.Timestamp:HH:mm:ss.fff}] [{e.Level}] {e.Message}");
        }
        await Clipboard.Default.SetTextAsync(sb.ToString());
        await _dialogs.AlertAsync("Diagnostics", "Copied diagnostics to clipboard.");
    }

    [RelayCommand]
    private void ToggleLogExpanded()
        => IsLogExpanded = !IsLogExpanded;

    [RelayCommand(CanExecute = nameof(CanPickNextAvailable))]
    private void PickNextAvailable()
    {
        TryPickNextAvailableForSelected();
        RefreshPlanView();
    }

    private bool CanPickNextAvailable() => SelectedPlanItem is not null && !IsBusy;

    private void TryPickNextAvailableForSelected()
    {
        if (_plan is null || SelectedPlanItem is null) { return; }

        var used = _plan.Items
            .Where(i => i.Action == FixupActionType.AssignSpecific && !string.IsNullOrWhiteSpace(i.RecommendedExtension))
            .Select(i => i.RecommendedExtension!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Allow re-picking for the selected item by temporarily removing its current value from the used set.
        if (!string.IsNullOrWhiteSpace(SelectedPlanItem.RecommendedExtension))
        {
            used.Remove(SelectedPlanItem.RecommendedExtension!);
        }

        foreach (var n in _plan.AvailableExtensionNumbers)
        {
            if (!used.Contains(n))
            {
                SelectedPlanItem.RecommendedExtension = n;
                SelectedPlanItem.Action = FixupActionType.AssignSpecific;
                OnPropertyChanged(nameof(SelectedAction));
                return;
            }
        }
    }

    private void ApplyDefaultsToPlan()
    {
        if (_plan is null) { return; }

        // Recompute assignment decisions from current defaults.
        // When assigning, use the plan's AvailableExtensionNumbers as the pool, excluding numbers already used.
        var pool = new Queue<string>(_plan.AvailableExtensionNumbers);

        var used = _plan.Items
            .Where(i => i.Action == FixupActionType.AssignSpecific && !string.IsNullOrWhiteSpace(i.RecommendedExtension))
            .Select(i => i.RecommendedExtension!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string? NextAvailable()
        {
            while (pool.Count > 0)
            {
                var n = pool.Dequeue();
                if (!used.Contains(n))
                {
                    used.Add(n);
                    return n;
                }
            }
            return null;
        }

        foreach (var item in _plan.Items)
        {
            switch (item.Category)
            {
                case "DuplicateUser":
                    if (string.Equals(DuplicateDefault, "Blank", StringComparison.OrdinalIgnoreCase))
                    {
                        item.Action = FixupActionType.ClearExtension;
                        item.RecommendedExtension = null;
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(item.RecommendedExtension))
                        {
                            item.RecommendedExtension = NextAvailable();
                        }
                        item.Action = string.IsNullOrWhiteSpace(item.RecommendedExtension) ? FixupActionType.ClearExtension : FixupActionType.AssignSpecific;
                    }
                    break;

                case "Missing":
                    if (string.Equals(MissingDefault, "Blank", StringComparison.OrdinalIgnoreCase))
                    {
                        item.Action = FixupActionType.ClearExtension;
                        item.RecommendedExtension = null;
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(item.RecommendedExtension))
                        {
                            item.RecommendedExtension = NextAvailable();
                        }
                        item.Action = string.IsNullOrWhiteSpace(item.RecommendedExtension) ? FixupActionType.ClearExtension : FixupActionType.AssignSpecific;
                    }
                    break;

                case "Discrepancy":
                    if (string.Equals(DiscrepancyDefault, "Blank", StringComparison.OrdinalIgnoreCase))
                    {
                        item.Action = FixupActionType.ClearExtension;
                        item.RecommendedExtension = null;
                    }
                    else if (string.Equals(DiscrepancyDefault, "Assign", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(item.RecommendedExtension))
                        {
                            item.RecommendedExtension = NextAvailable();
                        }
                        item.Action = string.IsNullOrWhiteSpace(item.RecommendedExtension) ? FixupActionType.ReassertExisting : FixupActionType.AssignSpecific;
                    }
                    else
                    {
                        item.Action = FixupActionType.ReassertExisting;
                        item.RecommendedExtension = null;
                    }
                    break;
            }
        }

        RefreshPlanView();
    }

    private void RefreshPlanView()
    {
        if (_plan is null)
        {
            PlanItems.Clear();
            SelectedPlanItem = null;
            PlanItemsCount = 0;
            return;
        }

        var filter = (PlanFilterText ?? "").Trim();
        IEnumerable<FixupItem> items = _plan.Items;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            items = items.Where(i =>
                Contains(i.Category, filter)
                || Contains(i.User, filter)
                || Contains(i.UserId, filter)
                || Contains(i.CurrentExtension, filter)
                || Contains(i.RecommendedExtension, filter)
                || Contains(i.Notes, filter));
        }

        var list = items.ToList();
        PlanItems.ReplaceRange(list);
        if (SelectedPlanItem is not null && !list.Contains(SelectedPlanItem))
        {
            SelectedPlanItem = null;
        }
        PlanItemsCount = list.Count;
        PickNextAvailableCommand.NotifyCanExecuteChanged();
    }

    private static bool Contains(string? value, string fragment)
        => value?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true;

    [RelayCommand]
    private async Task PasteTokenAsync()
    {
        var text = await Clipboard.Default.GetTextAsync();
        if (string.IsNullOrWhiteSpace(text)) { return; }
        UseEnvToken = false;
        AccessToken = NormalizeToken(text);
    }

    private static string NormalizeToken(string raw)
    {
        var t = (raw ?? "").Trim();
        if (t.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            t = t.Substring("Bearer ".Length).Trim();
        }
        return t;
    }
}
