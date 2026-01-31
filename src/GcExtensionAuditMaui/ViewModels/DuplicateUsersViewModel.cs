using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcExtensionAuditMaui.Models.Audit;
using GcExtensionAuditMaui.Services;
using GcExtensionAuditMaui.Utilities;

namespace GcExtensionAuditMaui.ViewModels;

public sealed partial class DuplicateUsersViewModel : ObservableObject
{
    private readonly ContextStore _store;
    private readonly AuditService _audit;
    private readonly ExportService _export;

    private IReadOnlyList<DuplicateUserAssignmentRow> _rows = Array.Empty<DuplicateUserAssignmentRow>();

    public DuplicateUsersViewModel(ContextStore store, AuditService audit, ExportService export)
    {
        _store = store;
        _audit = audit;
        _export = export;
    }

    public ObservableRangeCollection<DuplicateUserAssignmentRow> Rows { get; } = new();

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
            _rows = await Task.Run(() => _audit.FindDuplicateUserExtensionAssignments(_store.Context));
            ApplyFilter();
            SummaryText = $"Rows: {Rows.Count}; DuplicateExtensions: {Rows.Select(r => r.ProfileExtension).Distinct(StringComparer.OrdinalIgnoreCase).Count()}";
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
            var outDir = await _export.ExportRowsAsync(_store.Context, _rows, "DuplicatesUsers.csv", _audit.Api.Stats, CancellationToken.None);
            _store.LastOutputFolder = outDir;
            LastExportFolder = outDir;
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
                Contains(r.ProfileExtension, q)
                || Contains(r.UserName, q)
                || Contains(r.UserEmail, q)
                || Contains(r.UserId, q)
                || Contains(r.UserState, q))
            .ToList();

        Rows.ReplaceRange(filtered);
    }

}
