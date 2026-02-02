using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcExtensionAuditMaui.Models.Audit;
using GcExtensionAuditMaui.Services;
using GcExtensionAuditMaui.Utilities;

namespace GcExtensionAuditMaui.ViewModels;

public sealed partial class DiscrepanciesViewModel : ObservableObject
{
    private readonly ContextStore _store;
    private readonly AuditService _audit;
    private readonly ReportModule _reportModule;
    private readonly DialogService _dialogs;

    private IReadOnlyList<DiscrepancyRow> _rows = Array.Empty<DiscrepancyRow>();

    public DiscrepanciesViewModel(ContextStore store, AuditService audit, ReportModule reportModule, DialogService dialogs)
    {
        _store = store;
        _audit = audit;
        _reportModule = reportModule;
        _dialogs = dialogs;
    }

    public ObservableRangeCollection<DiscrepancyRow> Rows { get; } = new();

    private string _filterText = "";
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value ?? ""))
            {
                ApplyFilter();
            }
        }
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

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                ExportCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RefreshAsync()
    {
        if (_store.Context is null)
        {
            SummaryText = "Context not built. Go to Home and click Build Context.";
            return;
        }

        IsBusy = true;
        try
        {
            _rows = await Task.Run(() => _audit.FindExtensionDiscrepancies(_store.Context));
            ApplyFilter();
            SummaryText = $"Rows: {Rows.Count}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        if (_store.Context is null || _rows.Count == 0) { return; }
        IsBusy = true;
        try
        {
            var outDir = await _reportModule.ExportDiscrepanciesToExcelAsync(_store.Context, _audit.Api.Stats, _rows, CancellationToken.None);
            _store.LastOutputFolder = outDir;
            LastExportFolder = outDir;
            
            await _dialogs.AlertAsync("Export Successful", $"Report exported successfully to:\n{outDir}");
        }
        finally { IsBusy = false; }
    }

    private bool CanRun() => !IsBusy;
    private bool CanExport() => !IsBusy && _rows.Count > 0;

    private void ApplyFilter()
    {
        var q = (FilterText ?? "").Trim();
        if (string.IsNullOrWhiteSpace(q))
        {
            Rows.ReplaceRange(_rows);
            return;
        }

        static bool Contains(string? value, string term)
            => !string.IsNullOrWhiteSpace(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);

        var filtered = _rows.Where(r =>
                Contains(r.Issue, q)
                || Contains(r.ProfileExtension, q)
                || Contains(r.UserName, q)
                || Contains(r.UserEmail, q)
                || Contains(r.ExtensionOwnerType, q)
                || Contains(r.ExtensionOwnerId, q)
                || Contains(r.ExtensionId, q))
            .ToList();

        Rows.ReplaceRange(filtered);
    }

}
