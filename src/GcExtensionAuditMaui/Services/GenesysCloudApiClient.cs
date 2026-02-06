using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GcExtensionAuditMaui.Models.Api;
using GcExtensionAuditMaui.Models.Logging;
using GcExtensionAuditMaui.Models.Observability;

namespace GcExtensionAuditMaui.Services;

public sealed class GenesysCloudApiClient
{
    private readonly HttpClient _http;
    private readonly ApiStats _stats;
    private readonly LoggingService _log;
    private readonly JsonSerializerOptions _json;
    private readonly Random _jitter = new();

    // Retry and backoff configuration constants
    private const int MaxRetries = 5;
    private const int InitialBackoffMs = 500;
    private const int MaxBackoffMs = 8000;
    private const double BackoffMultiplier = 1.8;
    private const int MaxJitterMs = 250;
    private const int RequestTimeoutSeconds = 120;

    // HTTP status codes for retry logic
    private const int HttpStatusTooManyRequests = 429;
    private const int HttpStatusInternalServerError = 500;
    private const int HttpStatusBadGateway = 502;
    private const int HttpStatusServiceUnavailable = 503;
    private const int HttpStatusGatewayTimeout = 504;

    public GenesysCloudApiClient(HttpClient http, ApiStats stats, LoggingService log)
    {
        _http = http;
        _stats = stats;
        _log = log;
        _json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public ApiStats Stats => _stats;

    public Task<PagedResponse<GcUser>?> GetUsersPageAsync(string apiBaseUri, string accessToken, int pageSize, int pageNumber, bool includeInactive, CancellationToken ct)
    {
        var state = includeInactive ? "" : "&state=active";
        var expand = "station,locations,lasttokenissued,authorization.unusedRoles";
        var pq = $"/api/v2/users?pageSize={pageSize}&pageNumber={pageNumber}{state}&expand={expand}";
        return SendAsync<PagedResponse<GcUser>>(HttpMethod.Get, apiBaseUri, accessToken, pq, body: null, ct);
    }

    public Task<GcUser?> GetUserAsync(string apiBaseUri, string accessToken, string userId, CancellationToken ct)
        => SendAsync<GcUser>(HttpMethod.Get, apiBaseUri, accessToken, $"/api/v2/users/{Uri.EscapeDataString(userId)}", body: null, ct);

    public Task<GcUser?> PatchUserAsync(string apiBaseUri, string accessToken, string userId, GcUserPatch patch, CancellationToken ct)
        => SendAsync<GcUser>(new HttpMethod("PATCH"), apiBaseUri, accessToken, $"/api/v2/users/{Uri.EscapeDataString(userId)}", patch, ct);

    public Task<PagedResponse<GcExtension>?> GetExtensionsPageAsync(string apiBaseUri, string accessToken, int pageSize, int pageNumber, CancellationToken ct)
        => SendAsync<PagedResponse<GcExtension>>(HttpMethod.Get, apiBaseUri, accessToken, $"/api/v2/telephony/providers/edges/extensions?pageSize={pageSize}&pageNumber={pageNumber}", body: null, ct);

    public Task<PagedResponse<GcDid>?> GetDidsPageAsync(string apiBaseUri, string accessToken, int pageSize, int pageNumber, CancellationToken ct)
        => SendAsync<PagedResponse<GcDid>>(HttpMethod.Get, apiBaseUri, accessToken, $"/api/v2/telephony/providers/edges/dids?pageSize={pageSize}&pageNumber={pageNumber}", body: null, ct);

    public async Task<PagedResponse<GcDid>?> GetDidsByNumberAsync(string apiBaseUri, string accessToken, string didNumber, CancellationToken ct)
    {
        // The platform has historically varied between query keys. Try a couple before failing.
        try
        {
            return await SendAsync<PagedResponse<GcDid>>(HttpMethod.Get, apiBaseUri, accessToken,
                $"/api/v2/telephony/providers/edges/dids?phoneNumber={Uri.EscapeDataString(didNumber)}", body: null, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest)
        {
            return await SendAsync<PagedResponse<GcDid>>(HttpMethod.Get, apiBaseUri, accessToken,
                $"/api/v2/telephony/providers/edges/dids?number={Uri.EscapeDataString(didNumber)}", body: null, ct).ConfigureAwait(false);
        }
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string apiBaseUri, string accessToken, string pathAndQuery, object? body, CancellationToken ct)
    {
        var backoffMs = InitialBackoffMs;

        apiBaseUri = apiBaseUri.TrimEnd('/');
        if (!pathAndQuery.StartsWith('/')) { pathAndQuery = "/" + pathAndQuery; }

        var uri = new Uri(apiBaseUri + pathAndQuery, UriKind.Absolute);
        var pathKey = pathAndQuery.Split('?')[0];

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            using var req = new HttpRequestMessage(method, uri);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (body is not null)
            {
                var json = JsonSerializer.Serialize(body, _json);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            _stats.RecordCall(method.Method, pathKey);
            _log.Log(LogLevel.Debug, $"API {method.Method} {pathAndQuery} (attempt {attempt})");

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(RequestTimeoutSeconds));

            HttpResponseMessage? resp = null;
            try
            {
                resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
                CaptureSupportHeaders(resp);
                CaptureRateLimit(resp);

                if (resp.IsSuccessStatusCode)
                {
                    await PreemptiveThrottleAsync(ct).ConfigureAwait(false);

                    if (resp.Content is null) { return default; }
                    var content = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(content)) { return default; }

                    return JsonSerializer.Deserialize<T>(content, _json);
                }

                var status = (int)resp.StatusCode;
                var isRetryable = status == HttpStatusTooManyRequests || status is >= HttpStatusInternalServerError and <= HttpStatusGatewayTimeout;

                var bodyText = resp.Content is null ? null : await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var msg = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
                _stats.RecordError(msg);
                _log.Log(LogLevel.Warn, $"API failure {method.Method} {pathAndQuery}", new
                {
                    Status = status,
                    Reason = resp.ReasonPhrase,
                    Retryable = isRetryable,
                    RequestId = GetHeader(resp.Headers, "x-request-id") ?? GetHeader(resp.Headers, "request-id"),
                    CorrelationId = GetHeader(resp.Headers, "inin-correlation-id") ?? GetHeader(resp.Headers, "x-correlation-id"),
                    RetryAfter = resp.Headers.RetryAfter?.ToString(),
                    Body = Truncate(bodyText, 1000),
                });

                if (!isRetryable || attempt == MaxRetries)
                {
                    throw new HttpRequestException(msg, null, resp.StatusCode);
                }

                var retryAfter = GetRetryAfterMs(resp);
                var jitter = _jitter.Next(0, MaxJitterMs);
                var sleepMs = Math.Max(backoffMs, retryAfter) + jitter;
                await Task.Delay(sleepMs, ct).ConfigureAwait(false);

                backoffMs = Math.Min(MaxBackoffMs, (int)(backoffMs * BackoffMultiplier));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                var msg = $"Request timed out after {RequestTimeoutSeconds}s: {method.Method} {pathAndQuery}";
                _stats.RecordError(msg);
                _log.Log(LogLevel.Error, msg);
                throw new TimeoutException(msg);
            }
            finally
            {
                resp?.Dispose();
            }
        }

        return default;
    }

    private void CaptureSupportHeaders(HttpResponseMessage resp)
    {
        var requestId = GetHeader(resp.Headers, "x-request-id") ?? GetHeader(resp.Headers, "request-id");
        var correlationId = GetHeader(resp.Headers, "inin-correlation-id") ?? GetHeader(resp.Headers, "x-correlation-id");
        _stats.RecordLastResponse((int)resp.StatusCode, requestId, correlationId);
    }

    private void CaptureRateLimit(HttpResponseMessage resp)
    {
        int? TryParseInt(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) { return null; }
            if (int.TryParse(s, out var i)) { return i; }
            if (double.TryParse(s, out var d)) { return (int)Math.Floor(d); }
            return null;
        }

        DateTime? TryParseReset(string? resetRaw)
        {
            if (string.IsNullOrWhiteSpace(resetRaw)) { return null; }
            if (!double.TryParse(resetRaw, out var resetNum)) { return null; }
            var now = DateTimeOffset.UtcNow;
            if (resetNum > 1000000000000) { return DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Floor(resetNum)).UtcDateTime; }
            if (resetNum > 1000000000) { return DateTimeOffset.FromUnixTimeSeconds((long)Math.Floor(resetNum)).UtcDateTime; }
            return now.AddSeconds(Math.Max(0, resetNum)).UtcDateTime;
        }

        var limitRaw = GetHeader(resp.Headers, "X-RateLimit-Limit");
        var remRaw = GetHeader(resp.Headers, "X-RateLimit-Remaining");
        var resetRaw = GetHeader(resp.Headers, "X-RateLimit-Reset");

        if (string.IsNullOrWhiteSpace(limitRaw) && string.IsNullOrWhiteSpace(remRaw) && string.IsNullOrWhiteSpace(resetRaw))
        {
            return;
        }

        _stats.RecordRateLimit(new RateLimitSnapshot
        {
            Limit = TryParseInt(limitRaw),
            Remaining = TryParseInt(remRaw),
            ResetUtc = TryParseReset(resetRaw),
            CapturedAtUtc = DateTime.UtcNow,
        });
    }

    private static string? GetHeader(HttpResponseHeaders headers, string name)
    {
        foreach (var kvp in headers)
        {
            if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return string.Join(",", kvp.Value);
            }
        }
        return null;
    }

    private async Task PreemptiveThrottleAsync(CancellationToken ct)
    {
        var snapshot = _stats.RateLimit;
        if (snapshot?.Remaining is null) { return; }
        if (snapshot.Remaining > 2) { return; }

        var sleepMs = 500;
        if (snapshot.ResetUtc is not null)
        {
            var delta = snapshot.ResetUtc.Value - DateTime.UtcNow;
            if (delta.TotalMilliseconds > 0)
            {
                sleepMs = (int)Math.Ceiling(delta.TotalMilliseconds + 250);
            }
        }

        sleepMs = Math.Clamp(sleepMs, 0, 60000);
        if (sleepMs <= 0) { return; }

        _log.Log(LogLevel.Warn, "Rate limit low; throttling", new { snapshot.Remaining, snapshot.Limit, snapshot.ResetUtc, SleepMs = sleepMs });
        await Task.Delay(sleepMs, ct).ConfigureAwait(false);
    }

    private static int GetRetryAfterMs(HttpResponseMessage resp)
    {
        try
        {
            if (resp.Headers.RetryAfter?.Delta is { } d) { return (int)d.TotalMilliseconds; }
            if (resp.Headers.RetryAfter?.Date is { } dt)
            {
                var delta = dt - DateTimeOffset.UtcNow;
                if (delta.TotalMilliseconds > 0) { return (int)delta.TotalMilliseconds; }
            }
        }
        catch { /* ignore */ }

        return 0;
    }

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "...";
}
