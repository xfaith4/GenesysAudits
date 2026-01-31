using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcExtensionAuditMaui.Models.Audit;
using GcExtensionAuditMaui.Services;
using GcExtensionAuditMaui.Utilities;

namespace GcExtensionAuditMaui.ViewModels;

public sealed partial class DryRunViewModel : ObservableObject
{
    private readonly ContextStore _store;
    private readonly AuditService _audit;
    private readonly ExportService _export;

    private DryRunReport? _report;
    private IReadOnlyList<DryRunRow> _allRows = Array.Empty<DryRunRow>();

    public DryRunViewModel(ContextStore store, AuditService audit, ExportService export)
    {
        _store = store;
        _audit = audit;
        _export = export;
    }

    public ObservableRangeCollection<DryRunRow> Rows { get; } = new();

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
                GenerateCommand.NotifyCanExecuteChanged();
                ExportCommand.NotifyCanExecuteChanged();
            }
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task GenerateAsync()
    {
        if (_store.Context is null)
        {
            SummaryText = "Context not built. Go to Home and click Build Context.";
            return;
        }

        IsBusy = true;
        try
        {
            _report = await Task.Run(() => _audit.NewDryRunReport(_store.Context));

            _allRows = _report.Rows;
            ApplyFilter();

            SummaryText =
                $"GeneratedAt: {_report.Metadata.GeneratedAt}\n" +
                $"Rows: {_report.Summary.TotalRows}; Missing: {_report.Summary.MissingAssignments}; Discrepancies: {_report.Summary.Discrepancies}; " +
                $"DupUsers: {_report.Summary.DuplicateUserRows}; DupExts: {_report.Summary.DuplicateExtensionRows}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        if (_store.Context is null || _report is null) { return; }

        IsBusy = true;
        try
        {
            var outDir = await _export.ExportDryRunAsync(_store.Context, _report, _audit.Api.Stats, CancellationToken.None);
            _store.LastOutputFolder = outDir;
            LastExportFolder = outDir;
        }
        finally { IsBusy = false; }
    }

    private bool CanRun() => !IsBusy;
    private bool CanExport() => !IsBusy && _report is not null;

    private void ApplyFilter()
    {
        var q = (FilterText ?? "").Trim();
        if (string.IsNullOrWhiteSpace(q))
        {
            Rows.ReplaceRange(_allRows);
            return;
        }

        static bool Contains(string? value, string term)
            => !string.IsNullOrWhiteSpace(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);

        var filtered = _allRows.Where(r =>
                Contains(r.Category, q)
                || Contains(r.ProfileExtension, q)
                || Contains(r.User, q)
                || Contains(r.After_Expected, q)
                || Contains(r.Notes, q))
            .ToList();

        Rows.ReplaceRange(filtered);
    }

}
