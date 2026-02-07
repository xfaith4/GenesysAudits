using GcExtensionAuditMaui.Models.AuditLogs;
using GcExtensionAuditMaui.Models.Logging;

namespace GcExtensionAuditMaui.Services;

/// <summary>
/// Service for managing audit log queries and results
/// </summary>
public sealed class AuditLogsService
{
    private readonly GenesysCloudApiClient _api;
    private readonly LoggingService _log;

    private const int PageSize = 500;
    private const int TransactionPollMaxSeconds = 120;
    private const int TransactionPollIntervalMs = 2000;

    public AuditLogsService(GenesysCloudApiClient api, LoggingService log)
    {
        _api = api;
        _log = log;
    }

    /// <summary>
    /// Runs a standard audit query with full pagination
    /// </summary>
    public async Task<AuditLogState> RunStandardQueryAsync(
        string apiBaseUri,
        string accessToken,
        AuditLogQueryRequest request,
        CancellationToken ct)
    {
        var state = new AuditLogState
        {
            QueryRequest = request,
            QueryExecutedAt = DateTime.UtcNow
        };

        // Load service mapping (optional but useful for export)
        try
        {
            _log.Log(LogLevel.Info, "Loading audit query service mapping");
            state.ServiceMapping = await _api.GetAuditQueryServiceMappingAsync(apiBaseUri, accessToken, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Warn, "Failed to load service mapping", new { Error = ex.Message });
        }

        // Build API request
        var apiRequest = BuildApiRequest(request);

        // POST query
        _log.Log(LogLevel.Info, "Posting audit query", new { request.IntervalStart, request.IntervalEnd, request.ServiceName });
        var transaction = await _api.PostAuditQueryAsync(apiBaseUri, accessToken, apiRequest, ct).ConfigureAwait(false);
        if (transaction is null || string.IsNullOrEmpty(transaction.Id))
        {
            throw new InvalidOperationException("Failed to create audit query transaction");
        }

        state.TransactionId = transaction.Id;
        _log.Log(LogLevel.Info, "Audit query transaction created", new { TransactionId = transaction.Id });

        // Poll transaction until complete
        var pollResult = await PollTransactionAsync(apiBaseUri, accessToken, transaction.Id, ct).ConfigureAwait(false);
        state.TransactionStatus = pollResult.Status;

        if (pollResult.Status?.State != "Fulfilled")
        {
            _log.Log(LogLevel.Warn, "Audit query transaction not fulfilled", new { State = pollResult.Status?.State });
            // Continue anyway to attempt results fetch
        }

        // Fetch all results pages
        _log.Log(LogLevel.Info, "Fetching audit query results");
        var expand = request.ExpandUser ? "user" : null;
        var allEntities = await FetchAllResultsAsync(apiBaseUri, accessToken, transaction.Id, expand, ct).ConfigureAwait(false);

        state.RawEntities = allEntities.Entities;
        state.TotalPages = allEntities.TotalPages;
        state.TotalResults = allEntities.TotalResults;

        _log.Log(LogLevel.Info, "Audit query completed", new
        {
            TotalEntities = state.RawEntities.Count,
            TotalPages = state.TotalPages,
            state.TransactionId
        });

        return state;
    }

    /// <summary>
    /// Runs a realtime related audit query
    /// </summary>
    public async Task<AuditLogState> RunRealtimeRelatedAsync(
        string apiBaseUri,
        string accessToken,
        string auditId,
        string? trustorOrgId,
        CancellationToken ct)
    {
        var state = new AuditLogState
        {
            QueryExecutedAt = DateTime.UtcNow
        };

        // Load realtime service mapping (optional)
        try
        {
            _log.Log(LogLevel.Info, "Loading realtime service mapping");
            state.ServiceMapping = await _api.GetAuditQueryRealtimeServiceMappingAsync(apiBaseUri, accessToken, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Warn, "Failed to load realtime service mapping", new { Error = ex.Message });
        }

        // POST realtime related query
        var request = new RealtimeRelatedQueryRequest
        {
            AuditId = auditId,
            TrustorOrgId = trustorOrgId
        };

        _log.Log(LogLevel.Info, "Posting realtime related query", new { auditId, trustorOrgId });
        var response = await _api.PostAuditQueryRealtimeRelatedAsync(apiBaseUri, accessToken, request, ct).ConfigureAwait(false);

        if (response is null)
        {
            throw new InvalidOperationException("Failed to fetch realtime related audit logs");
        }

        state.RawEntities = response.Entities;
        _log.Log(LogLevel.Info, "Realtime related query completed", new { TotalEntities = state.RawEntities.Count });

        return state;
    }

    private AuditQueryApiRequest BuildApiRequest(AuditLogQueryRequest request)
    {
        var apiRequest = new AuditQueryApiRequest
        {
            Interval = request.GetIntervalString(),
            ServiceName = string.IsNullOrWhiteSpace(request.ServiceName) ? null : request.ServiceName
        };

        // Build filters list (only include non-empty)
        var filters = new List<AuditQueryFilter>();
        
        if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            filters.Add(new AuditQueryFilter { Property = "userId", Value = request.UserId });
        }
        if (!string.IsNullOrWhiteSpace(request.ClientId))
        {
            filters.Add(new AuditQueryFilter { Property = "clientId", Value = request.ClientId });
        }
        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            filters.Add(new AuditQueryFilter { Property = "action", Value = request.Action });
        }
        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            filters.Add(new AuditQueryFilter { Property = "entityType", Value = request.EntityType });
        }
        if (!string.IsNullOrWhiteSpace(request.EntityId))
        {
            filters.Add(new AuditQueryFilter { Property = "entityId", Value = request.EntityId });
        }

        if (filters.Count > 0)
        {
            apiRequest.Filters = filters;
        }

        // Sort by Timestamp descending
        apiRequest.Sort = new List<AuditQuerySort>
        {
            new AuditQuerySort { Name = "Timestamp", SortOrder = "DESC" }
        };

        return apiRequest;
    }

    private async Task<(AuditTransactionStatusResponse? Status, bool TimedOut)> PollTransactionAsync(
        string apiBaseUri,
        string accessToken,
        string transactionId,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;
        var maxTime = TimeSpan.FromSeconds(TransactionPollMaxSeconds);

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            var elapsed = DateTime.UtcNow - startTime;
            if (elapsed > maxTime)
            {
                _log.Log(LogLevel.Warn, "Transaction poll timeout", new { TransactionId = transactionId, ElapsedSeconds = elapsed.TotalSeconds });
                var finalStatus = await _api.GetAuditQueryTransactionAsync(apiBaseUri, accessToken, transactionId, ct).ConfigureAwait(false);
                return (finalStatus, true);
            }

            var status = await _api.GetAuditQueryTransactionAsync(apiBaseUri, accessToken, transactionId, ct).ConfigureAwait(false);
            
            if (status is null)
            {
                throw new InvalidOperationException($"Failed to get transaction status for {transactionId}");
            }

            _log.Log(LogLevel.Debug, "Transaction status", new { TransactionId = transactionId, status.State });

            // Terminal states
            if (status.State == "Fulfilled" || status.State == "Failed" || status.State == "Expired")
            {
                return (status, false);
            }

            // Wait before next poll
            await Task.Delay(TransactionPollIntervalMs, ct).ConfigureAwait(false);
        }
    }

    private async Task<(List<AuditLogEntity> Entities, int TotalPages, long? TotalResults)> FetchAllResultsAsync(
        string apiBaseUri,
        string accessToken,
        string transactionId,
        string? expand,
        CancellationToken ct)
    {
        var allEntities = new List<AuditLogEntity>();
        string? cursor = null;
        var pageNumber = 1;
        long? totalResults = null;

        do
        {
            ct.ThrowIfCancellationRequested();

            _log.Log(LogLevel.Debug, "Fetching results page", new { Page = pageNumber, Cursor = cursor });
            var response = await _api.GetAuditQueryResultsAsync(apiBaseUri, accessToken, transactionId, PageSize, cursor, expand, ct).ConfigureAwait(false);

            if (response is null)
            {
                _log.Log(LogLevel.Warn, "Null response from results fetch", new { Page = pageNumber });
                break;
            }

            if (response.Entities is not null && response.Entities.Count > 0)
            {
                allEntities.AddRange(response.Entities);
                _log.Log(LogLevel.Info, "Fetched results page", new { Page = pageNumber, Count = response.Entities.Count, TotalSoFar = allEntities.Count });
            }

            // Capture total on first page
            if (pageNumber == 1 && response.Total.HasValue)
            {
                totalResults = response.Total.Value;
            }

            cursor = response.Cursor;
            pageNumber++;

            // Continue if cursor is present and not empty
        } while (!string.IsNullOrEmpty(cursor));

        _log.Log(LogLevel.Info, "All results fetched", new { TotalEntities = allEntities.Count, TotalPages = pageNumber - 1 });

        return (allEntities, pageNumber - 1, totalResults);
    }
}
