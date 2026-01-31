using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcExtensionAuditMaui.Models.Observability;
using GcExtensionAuditMaui.Services;

namespace GcExtensionAuditMaui.ViewModels;

public sealed partial class LogViewModel : ObservableObject
{
    private readonly LoggingService _log;
    private readonly ContextStore _store;
    private readonly PlatformOpenService _open;
    private readonly ApiStats _apiStats;
    private readonly DialogService _dialogs;

    public LogViewModel(LoggingService log, ContextStore store, PlatformOpenService open, ApiStats apiStats, DialogService dialogs)
    {
        _log = log;
        _store = store;
        _open = open;
        _apiStats = apiStats;
        _dialogs = dialogs;
    }

    public ObservableCollection<Models.Logging.LogEntry> Entries => _log.Entries;

    private bool _autoScroll = true;
    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    [RelayCommand]
    private void ClearView() => _log.ClearView();

    [RelayCommand]
    private async Task OpenLogAsync()
    {
        if (!string.IsNullOrWhiteSpace(_log.LogPath))
        {
            await _open.OpenFileAsync(_log.LogPath);
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
    private async Task CopyDiagnosticsAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"GeneratedAt: {DateTime.Now:O}");

        if (_store.Summary is not null)
        {
            sb.AppendLine($"ContextSummary: {_store.Summary}");
        }
        else
        {
            sb.AppendLine("ContextSummary: (none)");
        }

        sb.AppendLine("ApiStats:");
        sb.AppendLine(JsonSerializer.Serialize(_apiStats.ToSnapshotObject(), new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

        sb.AppendLine("LastLogLines:");
        foreach (var e in Entries.TakeLast(50))
        {
            sb.AppendLine($"[{e.Timestamp:HH:mm:ss.fff}] [{e.Level}] {e.Message}");
        }

        await Clipboard.Default.SetTextAsync(sb.ToString());
        await _dialogs.AlertAsync("Diagnostics", "Copied diagnostics to clipboard.");
    }
}
