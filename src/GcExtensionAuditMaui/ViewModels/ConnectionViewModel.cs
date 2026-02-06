using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GcExtensionAuditMaui.Models.Audit;
using GcExtensionAuditMaui.Models.Logging;
using GcExtensionAuditMaui.Services;
using Microsoft.Maui.Storage;

namespace GcExtensionAuditMaui.ViewModels;

public sealed partial class ConnectionViewModel : ObservableObject
{
    private readonly ContextStore _store;
    private readonly AuditService _audit;
    private readonly LoggingService _log;

    private CancellationTokenSource? _cts;
    private string _stage = "Ready";

    public ConnectionViewModel(ContextStore store, AuditService audit, LoggingService log)
    {
        _store = store;
        _audit = audit;
        _log = log;

        ApiBaseUri = Preferences.Get(nameof(ApiBaseUri), "https://api.usw2.pure.cloud");
        UseEnvToken = Preferences.Get(nameof(UseEnvToken), false);
        IncludeInactive = Preferences.Get(nameof(IncludeInactive), false);
    }

    private string _apiBaseUri = "";
    public string ApiBaseUri
    {
        get => _apiBaseUri;
        set
        {
            // Normalize and validate the URI
            var normalized = value?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                // Basic URI format validation
                if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    _log.Log(LogLevel.Warn, "Invalid API Base URI format. Must be a valid HTTP/HTTPS URL.");
                    // Don't update the value if invalid
                    return;
                }
            }
            
            if (SetProperty(ref _apiBaseUri, normalized))
            {
                Preferences.Set(nameof(ApiBaseUri), normalized);
            }
        }
    }

    private string _accessToken = "";
    public string AccessToken
    {
        get => _accessToken;
        set
        {
            // Basic token validation
            var token = value ?? "";
            if (!string.IsNullOrWhiteSpace(token) && token.Length < 10)
            {
                _log.Log(LogLevel.Warn, "Access token appears too short. Ensure you have a valid token.");
            }
            SetProperty(ref _accessToken, token);
        }
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

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                BuildContextCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
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

    private string _apiStatsText = "";
    public string ApiStatsText
    {
        get => _apiStatsText;
        set => SetProperty(ref _apiStatsText, value ?? "");
    }

    [RelayCommand]
    private async Task PasteTokenAsync()
    {
        try
        {
            var text = await Clipboard.Default.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text)) { return; }
            UseEnvToken = false;
            AccessToken = NormalizeToken(text);
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Warn, "Paste token failed", ex: ex);
        }
    }

    [RelayCommand(CanExecute = nameof(CanBuildContext))]
    private async Task BuildContextAsync()
    {
        IsBusy = true;
        StatusText = "Building context…";
        ContextSummaryText = "";
        ApiStatsText = "";

        _cts = new CancellationTokenSource();

        var token = UseEnvToken ? (Environment.GetEnvironmentVariable("GC_ACCESS_TOKEN") ?? "") : AccessToken;
        token = NormalizeToken(token);
        if (string.IsNullOrWhiteSpace(token))
        {
            IsBusy = false;
            StatusText = "Access token is required.";
            return;
        }

        var progress = new Progress<string>(s =>
        {
            _stage = s;
            StatusText = s;
            RefreshApiStatsText();
        });

        try
        {
            _stage = "Building context";
            var context = await _audit.BuildContextAsync(
                auditKind: AuditNumberKind.Extension,
                apiBaseUri: ApiBaseUri,
                accessToken: token,
                includeInactive: IncludeInactive,
                usersPageSize: AuditService.DefaultUsersPageSize,
                extensionsPageSize: AuditService.DefaultExtensionsPageSize,
                maxFullExtensionPages: 25,
                progress: progress,
                ct: _cts.Token);

            _store.Context = context;
            _store.Summary = _audit.GetSummary(context);
            ContextSummaryText = _store.Summary.ToString();
            RefreshApiStatsText();

            StatusText = "Context ready.";
            _log.Log(LogLevel.Info, "Context built", new { _store.Summary });
        }
        catch (OperationCanceledException)
        {
            StatusText = "Canceled.";
            _log.Log(LogLevel.Warn, "Build context canceled");
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
            _log.Log(LogLevel.Error, ex.Message, ex: ex);
        }
        catch (Exception ex)
        {
            StatusText = $"Failed at stage: {_stage}";
            _log.Log(LogLevel.Error, $"Build context failed at stage: {_stage}", ex: ex);
        }
        finally
        {
            IsBusy = false;
            RefreshApiStatsText();
            BuildContextCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanBuildContext() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private bool CanCancel() => IsBusy;

    private void RefreshApiStatsText()
    {
        var s = _audit.Api.Stats;
        var rl = s.RateLimit;
        ApiStatsText =
            $"API Calls: {s.TotalCalls}; LastError: {(string.IsNullOrWhiteSpace(s.LastError) ? "None" : s.LastError)}; " +
            $"LastStatus: {(s.LastStatusCode is null ? "N/A" : s.LastStatusCode)}; " +
            $"RequestId: {(string.IsNullOrWhiteSpace(s.LastRequestId) ? "N/A" : s.LastRequestId)}; " +
            $"CorrelationId: {(string.IsNullOrWhiteSpace(s.LastCorrelationId) ? "N/A" : s.LastCorrelationId)}; " +
            $"RateLimit: {(rl?.Remaining is null ? "N/A" : $"{rl.Remaining}/{rl.Limit} reset={rl.ResetUtc:O}")}";
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
