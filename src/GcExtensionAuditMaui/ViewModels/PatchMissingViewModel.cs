using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcExtensionAuditMaui.Models.Patch;
using GcExtensionAuditMaui.Services;
using Microsoft.Maui.Storage;
using GcExtensionAuditMaui.Utilities;

namespace GcExtensionAuditMaui.ViewModels;

public sealed partial class PatchMissingViewModel : ObservableObject
{
    private readonly ContextStore _store;
    private readonly AuditService _audit;
    private readonly ExportService _export;
    private readonly DialogService _dialogs;
    private readonly PlatformOpenService _open;

    private CancellationTokenSource? _cts;

    public PatchMissingViewModel(ContextStore store, AuditService audit, ExportService export, DialogService dialogs, PlatformOpenService open)
    {
        _store = store;
        _audit = audit;
        _export = export;
        _dialogs = dialogs;
        _open = open;

        WhatIf = Preferences.Get(nameof(WhatIf), true);
        SleepMsBetween = Preferences.Get(nameof(SleepMsBetween), 150);
        MaxUpdates = Preferences.Get(nameof(MaxUpdates), 0);
        MaxFailures = Preferences.Get(nameof(MaxFailures), 0);
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
                RunCommand.NotifyCanExecuteChanged();
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

    private string _previewSummaryText = "";
    public string PreviewSummaryText
    {
        get => _previewSummaryText;
        set => SetProperty(ref _previewSummaryText, value ?? "");
    }

    public ObservableRangeCollection<PatchPreviewRow> PreviewRows { get; } = new();
    public ObservableCollection<PatchUpdatedRow> Updated { get; } = new();
    public ObservableCollection<PatchSkippedRow> Skipped { get; } = new();
    public ObservableCollection<PatchFailedRow> Failed { get; } = new();

    [RelayCommand(CanExecute = nameof(CanPreview))]
    private async Task PreviewAsync()
    {
        if (_store.Context is null)
        {
            PreviewSummaryText = "Context not built. Go to Home and click Build Context.";
            return;
        }

        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                var missing = _audit.FindMissingExtensionAssignments(_store.Context);
                var dupSet = _audit.FindDuplicateUserExtensionAssignments(_store.Context)
                    .Select(d => d.ProfileExtension)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var patchable = missing.Where(m => !dupSet.Contains(m.ProfileExtension)).ToList();
                var excluded = missing.Count - patchable.Count;

                if (MaxUpdates > 0)
                {
                    patchable = patchable.Take(MaxUpdates).ToList();
                }

                var preview = patchable.Select(m => new PatchPreviewRow
                {
                    UserId = m.UserId,
                    User = _store.Context.UserDisplayById.GetValueOrDefault(m.UserId, m.UserId),
                    Extension = m.ProfileExtension,
                }).ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PreviewRows.ReplaceRange(preview);
                    PreviewSummaryText =
                        $"MissingFound={missing.Count}; PatchTargets={preview.Count}; ExcludedDuplicates={excluded}; MaxUpdates={(MaxUpdates <= 0 ? "All" : MaxUpdates)}; MaxFailures={(MaxFailures <= 0 ? "Unlimited" : MaxFailures)}";
                });
            });
        }
        finally
        {
            IsBusy = false;
            PreviewCommand.NotifyCanExecuteChanged();
            RunCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanPreview() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (_store.Context is null)
        {
            StatusText = "Context not built. Go to Home and click Build Context.";
            return;
        }

        if (!WhatIf && !string.Equals(ConfirmText, "PATCH", StringComparison.Ordinal))
        {
            StatusText = "To run real changes: uncheck WhatIf and type PATCH in Confirm.";
            return;
        }

        // Always refresh preview counts before running.
        await PreviewAsync();

        if (!WhatIf)
        {
            var ok = await _dialogs.ConfirmAsync(
                "Confirm Patch",
                $"You are about to apply real changes.\n\n{PreviewSummaryText}\n\nContinue?",
                accept: "PATCH",
                cancel: "Cancel");
            if (!ok)
            {
                StatusText = "Canceled.";
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
            var options = new PatchOptions
            {
                WhatIf = WhatIf,
                SleepMsBetween = Math.Max(0, SleepMsBetween),
                MaxUpdates = Math.Max(0, MaxUpdates),
                MaxFailures = Math.Max(0, MaxFailures),
            };

            var result = await _audit.PatchMissingAsync(_store.Context, options, progress, _cts.Token);

            SummaryText =
                $"MissingFound={result.Summary.MissingFound}; Updated={result.Summary.Updated}; Skipped={result.Summary.Skipped}; Failed={result.Summary.Failed}; WhatIf={result.Summary.WhatIf}";

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
            RunCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
            PreviewCommand.NotifyCanExecuteChanged();
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

    private bool CanRun() => !IsBusy;
    private bool CanCancel() => IsBusy;
}
