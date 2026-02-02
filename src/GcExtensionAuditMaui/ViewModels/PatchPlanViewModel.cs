using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcExtensionAuditMaui.Models.Patch;
using GcExtensionAuditMaui.Models.Planning;
using GcExtensionAuditMaui.Services;
using Microsoft.Maui.Storage;
using GcExtensionAuditMaui.Utilities;

namespace GcExtensionAuditMaui.ViewModels;

public sealed partial class PatchPlanViewModel : ObservableObject
{
    private readonly ContextStore _store;
    private readonly AuditService _audit;
    private readonly FixupPlannerService _planner;
    private readonly ExportService _export;
    private readonly DialogService _dialogs;
    private readonly PlatformOpenService _open;

    private CancellationTokenSource? _cts;
    private FixupPlan? _currentPlan;

    public PatchPlanViewModel(
        ContextStore store, 
        AuditService audit, 
        FixupPlannerService planner,
        ExportService export, 
        DialogService dialogs, 
        PlatformOpenService open)
    {
        _store = store;
        _audit = audit;
        _planner = planner;
        _export = export;
        _dialogs = dialogs;
        _open = open;

        WhatIf = Preferences.Get(nameof(WhatIf), true);
        SleepMsBetween = Preferences.Get(nameof(SleepMsBetween), 150);
        MaxUpdates = Preferences.Get(nameof(MaxUpdates), 0);
        MaxFailures = Preferences.Get(nameof(MaxFailures), 0);
        
        IncludeMissing = Preferences.Get(nameof(IncludeMissing), true);
        IncludeDuplicateUser = Preferences.Get(nameof(IncludeDuplicateUser), true);
        IncludeDiscrepancy = Preferences.Get(nameof(IncludeDiscrepancy), true);
        IncludeReassert = Preferences.Get(nameof(IncludeReassert), false);
    }

    private bool _whatIf = true;
    public bool WhatIf
    {
        get => _whatIf;
        set
        {
            if (SetProperty(ref _whatIf, value))
            {
                Preferences.Set(nameof(WhatIf), value);
            }
        }
    }

    private int _sleepMsBetween = 150;
    public int SleepMsBetween
    {
        get => _sleepMsBetween;
        set
        {
            if (SetProperty(ref _sleepMsBetween, value))
            {
                Preferences.Set(nameof(SleepMsBetween), value);
            }
        }
    }

    private int _maxUpdates;
    public int MaxUpdates
    {
        get => _maxUpdates;
        set
        {
            if (SetProperty(ref _maxUpdates, value))
            {
                Preferences.Set(nameof(MaxUpdates), value);
            }
        }
    }

    private int _maxFailures;
    public int MaxFailures
    {
        get => _maxFailures;
        set
        {
            if (SetProperty(ref _maxFailures, value))
            {
                Preferences.Set(nameof(MaxFailures), value);
            }
        }
    }

    private bool _includeMissing = true;
    public bool IncludeMissing
    {
        get => _includeMissing;
        set
        {
            if (SetProperty(ref _includeMissing, value))
            {
                Preferences.Set(nameof(IncludeMissing), value);
                GeneratePlanCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _includeDuplicateUser = true;
    public bool IncludeDuplicateUser
    {
        get => _includeDuplicateUser;
        set
        {
            if (SetProperty(ref _includeDuplicateUser, value))
            {
                Preferences.Set(nameof(IncludeDuplicateUser), value);
                GeneratePlanCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _includeDiscrepancy = true;
    public bool IncludeDiscrepancy
    {
        get => _includeDiscrepancy;
        set
        {
            if (SetProperty(ref _includeDiscrepancy, value))
            {
                Preferences.Set(nameof(IncludeDiscrepancy), value);
                GeneratePlanCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool _includeReassert;
    public bool IncludeReassert
    {
        get => _includeReassert;
        set
        {
            if (SetProperty(ref _includeReassert, value))
            {
                Preferences.Set(nameof(IncludeReassert), value);
                GeneratePlanCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private string _confirmText = "";
    public string ConfirmText
    {
        get => _confirmText;
        set => SetProperty(ref _confirmText, value ?? "");
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                GeneratePlanCommand.NotifyCanExecuteChanged();
                RunPatchCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value ?? "");
    }

    private string _summaryText = "";
    public string SummaryText
    {
        get => _summaryText;
        set => SetProperty(ref _summaryText, value ?? "");
    }

    private string _lastExportFolder = "";
    public string LastExportFolder
    {
        get => _lastExportFolder;
        set => SetProperty(ref _lastExportFolder, value ?? "");
    }

    private string _planSummaryText = "";
    public string PlanSummaryText
    {
        get => _planSummaryText;
        set => SetProperty(ref _planSummaryText, value ?? "");
    }

    public ObservableRangeCollection<FixupItem> PlanItems { get; } = new();
    public ObservableCollection<PatchUpdatedRow> Updated { get; } = new();
    public ObservableCollection<PatchSkippedRow> Skipped { get; } = new();
    public ObservableCollection<PatchFailedRow> Failed { get; } = new();

    [RelayCommand(CanExecute = nameof(CanGeneratePlan))]
    private async Task GeneratePlanAsync()
    {
        if (_store.Context is null)
        {
            PlanSummaryText = "Context not built. Go to Home and click Build Context.";
            return;
        }

        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                var plan = _planner.BuildPlan(
                    _store.Context, 
                    reassertConsistentUsers: IncludeReassert, 
                    preferAssignAvailableOverBlank: true);

                _currentPlan = plan;

                // Filter items based on category selection for preview
                var filteredItems = plan.Items.Where(item =>
                {
                    return item.Category switch
                    {
                        "Missing" => IncludeMissing,
                        "DuplicateUser" => IncludeDuplicateUser,
                        "Discrepancy" => IncludeDiscrepancy,
                        "Reassert" => IncludeReassert,
                        _ => false
                    };
                }).ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PlanItems.ReplaceRange(filteredItems);
                    PlanSummaryText =
                        $"{plan.SummaryText}\n" +
                        $"ItemsToExecute={filteredItems.Count}; MaxUpdates={(MaxUpdates <= 0 ? "All" : MaxUpdates)}; MaxFailures={(MaxFailures <= 0 ? "Unlimited" : MaxFailures)}";
                });
            });
        }
        finally
        {
            IsBusy = false;
            RunPatchCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanGeneratePlan() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRunPatch))]
    private async Task RunPatchAsync()
    {
        if (_store.Context is null || _currentPlan is null)
        {
            StatusText = "Context not built or plan not generated. Generate plan first.";
            return;
        }

        if (!WhatIf && !string.Equals(ConfirmText, "PATCH", StringComparison.Ordinal))
        {
            StatusText = "To run real changes: uncheck WhatIf and type PATCH in Confirm.";
            return;
        }

        // Double verification when not in WhatIf mode
        if (!WhatIf)
        {
            var firstConfirm = await _dialogs.ConfirmAsync(
                "⚠️ First Confirmation",
                $"You are about to apply REAL changes to user extensions.\n\n{PlanSummaryText}\n\nAre you absolutely sure?",
                accept: "YES, CONTINUE",
                cancel: "Cancel");
            
            if (!firstConfirm)
            {
                StatusText = "Canceled at first confirmation.";
                return;
            }

            // Second confirmation
            var itemsToProcess = PlanItems.Count; // This is the filtered count based on category selection
            var secondConfirm = await _dialogs.ConfirmAsync(
                "⚠️⚠️ FINAL Confirmation",
                $"This is your LAST CHANCE to cancel.\n\nThis will modify {itemsToProcess} user extension assignment{(itemsToProcess != 1 ? "s" : "")}.\n\nThis action CANNOT be undone automatically.\n\nProceed with REAL changes?",
                accept: "PATCH NOW",
                cancel: "Cancel");
            
            if (!secondConfirm)
            {
                StatusText = "Canceled at final confirmation.";
                return;
            }
        }

        IsBusy = true;
        StatusText = WhatIf ? "Running patch (WhatIf)…" : "Running patch (REAL)…";
        SummaryText = "";
        LastExportFolder = "";
        Updated.Clear();
        Skipped.Clear();
        Failed.Clear();

        _cts = new CancellationTokenSource();
        var progress = new Progress<string>(s => StatusText = s);

        try
        {
            var options = new PatchFromPlanOptions
            {
                WhatIf = WhatIf,
                SleepMsBetween = Math.Max(0, SleepMsBetween),
                MaxUpdates = Math.Max(0, MaxUpdates),
                MaxFailures = Math.Max(0, MaxFailures),
                IncludeMissing = IncludeMissing,
                IncludeDuplicateUser = IncludeDuplicateUser,
                IncludeDiscrepancy = IncludeDiscrepancy,
                IncludeReassert = IncludeReassert,
            };

            var result = await _audit.PatchFromPlanAsync(_store.Context, _currentPlan, options, progress, _cts.Token);

            SummaryText =
                $"TotalPlanItems={result.Summary.TotalPlanItems}; ItemsTargeted={result.Summary.ItemsTargeted}; " +
                $"Updated={result.Summary.Updated}; Skipped={result.Summary.Skipped}; Failed={result.Summary.Failed}; WhatIf={result.Summary.WhatIf}";

            foreach (var r in result.Updated) { Updated.Add(r); }
            foreach (var r in result.Skipped) { Skipped.Add(r); }
            foreach (var r in result.Failed) { Failed.Add(r); }

            var outDir = await _export.ExportPatchAsync(_store.Context, result, _audit.Api.Stats, CancellationToken.None);
            _store.LastOutputFolder = outDir;
            LastExportFolder = outDir;

            StatusText = "Patch complete (exported).";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Canceled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Patch failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            RunPatchCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            GeneratePlanCommand.NotifyCanExecuteChanged();
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

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanRunPatch() => !IsBusy && _currentPlan is not null;
    private bool CanCancel() => IsBusy;
}
