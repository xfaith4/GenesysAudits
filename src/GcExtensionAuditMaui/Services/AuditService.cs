using System.Net;
using GcExtensionAuditMaui.Models.Api;
using GcExtensionAuditMaui.Models.Audit;
using GcExtensionAuditMaui.Models.Logging;
using GcExtensionAuditMaui.Models.Patch;

namespace GcExtensionAuditMaui.Services;

public sealed class AuditService
{
    public const int UsersPageSizeMax = 500;
    public const int ExtensionsPageSizeMax = 100;

    public const int DefaultUsersPageSize = UsersPageSizeMax;
    public const int DefaultExtensionsPageSize = ExtensionsPageSizeMax;

    private readonly GenesysCloudApiClient _api;
    private readonly LoggingService _log;

    public AuditService(GenesysCloudApiClient api, LoggingService log)
    {
        _api = api;
        _log = log;
    }

    public GenesysCloudApiClient Api => _api;

    public async Task<AuditContext> BuildContextAsync(
        AuditNumberKind auditKind,
        string apiBaseUri,
        string accessToken,
        bool includeInactive,
        int usersPageSize,
        int extensionsPageSize,
        int maxFullExtensionPages,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        usersPageSize = Math.Clamp(usersPageSize, 1, UsersPageSizeMax);
        extensionsPageSize = Math.Clamp(extensionsPageSize, 1, ExtensionsPageSizeMax);

        _log.Log(LogLevel.Info, "Building audit context", new { auditKind = auditKind.ToString(), includeInactive, usersPageSize, extensionsPageSize, maxFullExtensionPages });

        progress?.Report("Fetching users page 1…");

        var users = new List<GcUser>(capacity: 2048);
        var page = 1;
        var pageCount = 0;
        do
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"Fetching users page {page}…");

            PagedResponse<GcUser>? resp;
            try
            {
                resp = await _api.GetUsersPageAsync(apiBaseUri, accessToken, usersPageSize, page, includeInactive, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException(
                    $"Token is invalid/expired or lacks required permissions for users. Status: {(int?)ex.StatusCode}.",
                    ex);
            }
            if (resp is null) { break; }

            pageCount = resp.PageCount;
            users.AddRange(resp.Entities.Where(u => u is not null));

            _log.Log(LogLevel.Info, "Users page fetched", new { PageNumber = page, resp.PageCount, Entities = resp.Entities.Count, TotalSoFar = users.Count });
            page++;
        } while (page <= pageCount);

        progress?.Report(auditKind == AuditNumberKind.Did ? "Extracting profile DIDs…" : "Extracting profile extensions…");

        var usersById = new Dictionary<string, GcUser>(StringComparer.OrdinalIgnoreCase);
        var userDisplayById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usersWithProfileExt = new List<UserWithProfileExtensionRow>();
        var profileExtNumbers = new List<string>();

        var processed = 0;
        foreach (var u in users)
        {
            if (u.Id is null) { continue; }
            processed++;

            usersById[u.Id] = u;
            userDisplayById[u.Id] = MakeUserDisplay(u);

            var ext = auditKind == AuditNumberKind.Did ? GetUserProfileDid(u) : GetUserProfileExtension(u);
            if (!string.IsNullOrWhiteSpace(ext))
            {
                usersWithProfileExt.Add(new UserWithProfileExtensionRow
                {
                    UserId = u.Id,
                    UserName = u.Name,
                    UserEmail = u.Email,
                    UserState = u.State,
                    ProfileExtension = ext,
                });
                profileExtNumbers.Add(ext);
            }

            if (processed % 500 == 0)
            {
                _log.Log(LogLevel.Info, "Profile extraction progress", new { ProcessedUsers = processed, TotalUsers = users.Count, UsersWithProfileExtension = usersWithProfileExt.Count });
            }
        }

        var distinctProfileExt = profileExtNumbers.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _log.Log(LogLevel.Info, "User profile numbers collected", new
        {
            AuditKind = auditKind.ToString(),
            UsersTotal = users.Count,
            UsersWithProfileExtension = usersWithProfileExt.Count,
            DistinctProfileExtensions = distinctProfileExt.Count,
        });

        var extensions = new List<GcExtension>(capacity: 2048);
        Dictionary<string, IReadOnlyList<GcExtension>>? extCache = null;

        var probePageCount = 0;
        string extMode;

        if (auditKind == AuditNumberKind.Did)
        {
            progress?.Report("Probing DIDs…");

            PagedResponse<GcDid>? probe;
            try
            {
                probe = await _api.GetDidsPageAsync(apiBaseUri, accessToken, extensionsPageSize, 1, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                var msg = $"Token lacks telephony/dids permissions or is invalid. Status: {(int?)ex.StatusCode}. Original error: {ex.Message}";
                _log.Log(LogLevel.Error, msg, ex: ex);
                throw new InvalidOperationException(msg, ex);
            }

            probePageCount = probe?.PageCount ?? 0;
            if (probePageCount <= maxFullExtensionPages)
            {
                extMode = "FULL";
                progress?.Report("Loading DIDs (FULL)…");
                _log.Log(LogLevel.Info, "Using FULL DIDs mode", new { probePageCount });

                var pageNum = 1;
                var pageCount2 = 0;
                do
                {
                    ct.ThrowIfCancellationRequested();
                    progress?.Report($"Loading DIDs (FULL) page {pageNum}…");

                    var resp = pageNum == 1 ? probe : await _api.GetDidsPageAsync(apiBaseUri, accessToken, extensionsPageSize, pageNum, ct).ConfigureAwait(false);
                    if (resp is null) { break; }

                    pageCount2 = resp.PageCount;
                    foreach (var d in resp.Entities.Where(d => d is not null))
                    {
                        var mapped = MapDidToExtension(d);
                        if (mapped is not null) { extensions.Add(mapped); }
                    }
                    _log.Log(LogLevel.Info, "DIDs page fetched", new { PageNumber = pageNum, resp.PageCount, Entities = resp.Entities.Count, TotalSoFar = extensions.Count });
                    pageNum++;
                } while (pageNum <= pageCount2);
            }
            else
            {
                extMode = "TARGETED";
                progress?.Report("Loading DIDs (TARGETED)…");
                _log.Log(LogLevel.Info, "Using TARGETED DIDs mode", new { probePageCount, TargetNumbers = distinctProfileExt.Count });

                extCache = new Dictionary<string, IReadOnlyList<GcExtension>>(StringComparer.OrdinalIgnoreCase);
                var i = 0;
                var fallbackToFull = false;
                foreach (var n in distinctProfileExt)
                {
                    ct.ThrowIfCancellationRequested();
                    i++;
                    if (extCache.ContainsKey(n)) { continue; }

                    progress?.Report($"Loading DIDs (TARGETED) {i}/{distinctProfileExt.Count}…");

                    try
                    {
                        var resp = await _api.GetDidsByNumberAsync(apiBaseUri, accessToken, n, ct).ConfigureAwait(false);
                        var ents = (resp?.Entities ?? new List<GcDid>())
                            .Select(MapDidToExtension)
                            .OfType<GcExtension>()
                            .ToList();

                        extCache[n] = ents;
                        extensions.AddRange(ents);
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.BadRequest)
                    {
                        // Some orgs/platform variants may not support server-side filtering for this list endpoint.
                        _log.Log(LogLevel.Warn, "DID targeted lookup not supported; falling back to FULL DID crawl", new { FirstFailedNumber = n, ex.StatusCode });
                        fallbackToFull = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        _log.Log(LogLevel.Warn, $"DID lookup failed for number {n}", ex: ex);
                        extCache[n] = Array.Empty<GcExtension>();
                    }

                    await Task.Delay(75, ct).ConfigureAwait(false);
                }

                if (fallbackToFull)
                {
                    extMode = "FULL";
                    extCache = null;
                    extensions.Clear();

                    progress?.Report("Loading DIDs (FULL)…");

                    var pageNum = 1;
                    var pageCount2 = 0;
                    do
                    {
                        ct.ThrowIfCancellationRequested();
                        progress?.Report($"Loading DIDs (FULL) page {pageNum}…");

                        var resp = pageNum == 1 ? probe : await _api.GetDidsPageAsync(apiBaseUri, accessToken, extensionsPageSize, pageNum, ct).ConfigureAwait(false);
                        if (resp is null) { break; }

                        pageCount2 = resp.PageCount;
                        foreach (var d in resp.Entities.Where(d => d is not null))
                        {
                            var mapped = MapDidToExtension(d);
                            if (mapped is not null) { extensions.Add(mapped); }
                        }
                        _log.Log(LogLevel.Info, "DIDs page fetched", new { PageNumber = pageNum, resp.PageCount, Entities = resp.Entities.Count, TotalSoFar = extensions.Count });
                        pageNum++;
                    } while (pageNum <= pageCount2);
                }
            }
        }
        else
        {
            progress?.Report("Probing extensions…");

            PagedResponse<GcExtension>? probe;
            try
            {
                probe = await _api.GetExtensionsPageAsync(apiBaseUri, accessToken, extensionsPageSize, 1, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                var msg = $"Token lacks telephony/extensions permissions or is invalid. Status: {(int?)ex.StatusCode}. Original error: {ex.Message}";
                _log.Log(LogLevel.Error, msg, ex: ex);
                throw new InvalidOperationException(msg, ex);
            }

            probePageCount = probe?.PageCount ?? 0;

            extMode = "FULL";
            progress?.Report("Loading extensions (FULL)…");
            _log.Log(LogLevel.Info, "Using FULL extensions mode", new { probePageCount, maxFullExtensionPages });

            if (probe is not null)
            {
                extensions.AddRange(probe.Entities.Where(e => e is not null));
                _log.Log(LogLevel.Info, "Extensions page fetched", new { PageNumber = 1, probe.PageCount, Entities = probe.Entities.Count, TotalSoFar = extensions.Count });
            }

            for (var extPage = 2; extPage <= probePageCount; extPage++)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"Loading extensions (FULL) page {extPage}…");

                var resp = await _api.GetExtensionsPageAsync(apiBaseUri, accessToken, extensionsPageSize, extPage, ct).ConfigureAwait(false);
                if (resp is null) { break; }

                extensions.AddRange(resp.Entities.Where(e => e is not null));
                _log.Log(LogLevel.Info, "Extensions page fetched", new { PageNumber = extPage, resp.PageCount, Entities = resp.Entities.Count, TotalSoFar = extensions.Count });
            }
        }

        _log.Log(LogLevel.Info, "Extensions loaded", new { Mode = extMode, ProbePageCount = probePageCount, ExtensionsLoaded = extensions.Count });

        var extByNumber = new Dictionary<string, List<GcExtension>>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in extensions)
        {
            var n = e.Number;
            if (string.IsNullOrWhiteSpace(n)) { continue; }

            if (!extByNumber.TryGetValue(n, out var list))
            {
                list = new List<GcExtension>();
                extByNumber[n] = list;
            }
            list.Add(e);
        }

        progress?.Report("Computing findings…");

        return new AuditContext
        {
            AuditKind = auditKind,
            ApiBaseUri = apiBaseUri,
            AccessToken = accessToken,
            IncludeInactive = includeInactive,

            Users = users,
            UsersById = usersById,
            UserDisplayById = userDisplayById,
            UsersWithProfileExtension = usersWithProfileExt,
            ProfileExtensionNumbers = distinctProfileExt,

            Extensions = extensions,
            ExtensionMode = extMode,
            ExtensionCache = extCache,
            ExtensionsByNumber = extByNumber.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<GcExtension>)kvp.Value, StringComparer.OrdinalIgnoreCase),
        };
    }

    public ContextSummary GetSummary(AuditContext context)
        => new()
        {
            AuditKind = context.AuditKind,
            UsersTotal = context.Users.Count,
            UsersWithProfileExtension = context.UsersWithProfileExtension.Count,
            DistinctProfileExtensions = context.ProfileExtensionNumbers.Count,
            ExtensionsLoaded = context.Extensions.Count,
            ExtensionMode = context.ExtensionMode,
        };

    public IReadOnlyList<DuplicateUserAssignmentRow> FindDuplicateUserExtensionAssignments(AuditContext context)
    {
        var byExt = new Dictionary<string, List<UserWithProfileExtensionRow>>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in context.UsersWithProfileExtension)
        {
            var n = r.ProfileExtension;
            if (!byExt.TryGetValue(n, out var list))
            {
                list = new List<UserWithProfileExtensionRow>();
                byExt[n] = list;
            }
            list.Add(r);
        }

        var dups = new List<DuplicateUserAssignmentRow>();
        foreach (var (ext, rows) in byExt)
        {
            if (rows.Count <= 1) { continue; }
            foreach (var row in rows)
            {
                dups.Add(new DuplicateUserAssignmentRow
                {
                    ProfileExtension = ext,
                    UserId = row.UserId,
                    UserName = row.UserName,
                    UserEmail = row.UserEmail,
                    UserState = row.UserState,
                });
            }
        }

        _log.Log(LogLevel.Info, "Duplicate user extension assignments", new { DuplicateRows = dups.Count, DuplicateExtensions = dups.Select(d => d.ProfileExtension).Distinct(StringComparer.OrdinalIgnoreCase).Count() });
        return dups;
    }

    public IReadOnlyList<DuplicateExtensionRecordRow> FindDuplicateExtensionRecords(AuditContext context)
    {
        var dups = new List<DuplicateExtensionRecordRow>();
        foreach (var (n, arr) in context.ExtensionsByNumber)
        {
            if (arr.Count <= 1) { continue; }
            foreach (var e in arr)
            {
                dups.Add(new DuplicateExtensionRecordRow
                {
                    ExtensionNumber = n,
                    ExtensionId = e.Id,
                    OwnerType = e.OwnerType,
                    OwnerId = e.Owner?.Id,
                    ExtensionPoolId = e.ExtensionPool?.Id,
                });
            }
        }

        _log.Log(LogLevel.Info, "Duplicate extension records", new { DuplicateRows = dups.Count, DuplicateNumbers = dups.Select(d => d.ExtensionNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count() });
        return dups;
    }

    public IReadOnlyList<DiscrepancyRow> FindExtensionDiscrepancies(AuditContext context)
    {
        var dupUserExtSet = FindDuplicateUserExtensionAssignments(context).Select(d => d.ProfileExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dupExtNumSet = FindDuplicateExtensionRecords(context).Select(d => d.ExtensionNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = new List<DiscrepancyRow>();

        foreach (var u in context.UsersWithProfileExtension)
        {
            var n = u.ProfileExtension;
            if (dupUserExtSet.Contains(n)) { continue; }
            if (dupExtNumSet.Contains(n)) { continue; }

            var extList = context.ExtensionsByNumber.TryGetValue(n, out var l) ? l : Array.Empty<GcExtension>();

            if (extList.Count == 0) { continue; }
            if (extList.Count > 1) { continue; }

            var e = extList[0];
            var ownerType = e.OwnerType ?? "";
            var ownerId = e.Owner?.Id ?? "";

            if (!string.Equals(ownerType, "USER", StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(new DiscrepancyRow
                {
                    Issue = "OwnerTypeNotUser",
                    ProfileExtension = n,
                    UserId = u.UserId,
                    UserName = u.UserName,
                    UserEmail = u.UserEmail,
                    ExtensionId = e.Id,
                    ExtensionOwnerType = e.OwnerType,
                    ExtensionOwnerId = e.Owner?.Id,
                });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(ownerId) && !string.Equals(ownerId, u.UserId, StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(new DiscrepancyRow
                {
                    Issue = "OwnerMismatch",
                    ProfileExtension = n,
                    UserId = u.UserId,
                    UserName = u.UserName,
                    UserEmail = u.UserEmail,
                    ExtensionId = e.Id,
                    ExtensionOwnerType = e.OwnerType,
                    ExtensionOwnerId = e.Owner?.Id,
                });
            }
        }

        _log.Log(LogLevel.Info, "Extension discrepancies found", new { Count = rows.Count });
        return rows;
    }

    public IReadOnlyList<MissingAssignmentRow> FindMissingExtensionAssignments(AuditContext context)
    {
        var dupUserExtSet = FindDuplicateUserExtensionAssignments(context).Select(d => d.ProfileExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dupExtNumSet = FindDuplicateExtensionRecords(context).Select(d => d.ExtensionNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rows = new List<MissingAssignmentRow>();
        foreach (var u in context.UsersWithProfileExtension)
        {
            var n = u.ProfileExtension;
            if (dupUserExtSet.Contains(n)) { continue; }
            if (dupExtNumSet.Contains(n)) { continue; }

            var hasAny = context.ExtensionsByNumber.TryGetValue(n, out var list) && list.Count > 0;
            if (!hasAny)
            {
                rows.Add(new MissingAssignmentRow
                {
                    Issue = "NoExtensionRecord",
                    ProfileExtension = n,
                    UserId = u.UserId,
                    UserName = u.UserName,
                    UserEmail = u.UserEmail,
                    UserState = u.UserState,
                });
            }
        }

        _log.Log(LogLevel.Info, "Missing assignments found (profile ext not in extension list)", new { Count = rows.Count });
        return rows;
    }

    public DryRunReport NewDryRunReport(AuditContext context)
    {
        var dupsUsers = FindDuplicateUserExtensionAssignments(context);
        var dupsExts = FindDuplicateExtensionRecords(context);
        var disc = FindExtensionDiscrepancies(context);
        var missing = FindMissingExtensionAssignments(context);

        var rows = new List<DryRunRow>();

        foreach (var m in missing)
        {
            rows.Add(new DryRunRow
            {
                Action = "PatchUserResyncExtension",
                Category = "MissingAssignment",
                UserId = m.UserId,
                User = context.UserDisplayById.TryGetValue(m.UserId, out var disp) ? disp : m.UserId,
                ProfileExtension = m.ProfileExtension,
                Before_ExtensionRecordFound = false,
                Before_ExtOwner = null,
                After_Expected = $"User PATCH reasserts extension {m.ProfileExtension} (sync attempt)",
                Notes = "Primary target",
            });
        }

        foreach (var d in disc)
        {
            var beforeOwner = d.ExtensionOwnerId;
            if (!string.IsNullOrWhiteSpace(d.ExtensionOwnerId) && context.UserDisplayById.TryGetValue(d.ExtensionOwnerId, out var ownerDisp))
            {
                beforeOwner = ownerDisp;
            }

            rows.Add(new DryRunRow
            {
                Action = "ReportOnly",
                Category = d.Issue,
                UserId = d.UserId,
                User = context.UserDisplayById.TryGetValue(d.UserId, out var disp) ? disp : d.UserId,
                ProfileExtension = d.ProfileExtension,
                Before_ExtensionRecordFound = true,
                Before_ExtOwner = beforeOwner,
                After_Expected = "N/A (extensions endpoints not reliably writable; fix via user assignment process)",
                Notes = $"ExtensionId={d.ExtensionId}; OwnerType={d.ExtensionOwnerType}",
            });
        }

        foreach (var d in dupsUsers)
        {
            rows.Add(new DryRunRow
            {
                Action = "ManualReview",
                Category = "DuplicateUserAssignment",
                UserId = d.UserId,
                User = context.UserDisplayById.TryGetValue(d.UserId, out var disp) ? disp : d.UserId,
                ProfileExtension = d.ProfileExtension,
                Before_ExtensionRecordFound = null,
                Before_ExtOwner = null,
                After_Expected = "Manual decision required",
                Notes = "Same extension present on multiple users",
            });
        }

        foreach (var d in dupsExts)
        {
            var beforeOwner = d.OwnerId;
            if (!string.IsNullOrWhiteSpace(d.OwnerId) && context.UserDisplayById.TryGetValue(d.OwnerId, out var ownerDisp))
            {
                beforeOwner = ownerDisp;
            }

            rows.Add(new DryRunRow
            {
                Action = "ManualReview",
                Category = "DuplicateExtensionRecords",
                UserId = null,
                User = null,
                ProfileExtension = d.ExtensionNumber,
                Before_ExtensionRecordFound = true,
                Before_ExtOwner = beforeOwner,
                After_Expected = "Manual decision required",
                Notes = $"Multiple extension records exist for number; ExtensionId={d.ExtensionId}",
            });
        }

        _log.Log(LogLevel.Info, "Dry run report created", new
        {
            Rows = rows.Count,
            Missing = missing.Count,
            Discrepancies = disc.Count,
            DuplicateUserRows = dupsUsers.Count,
            DuplicateExtRows = dupsExts.Count,
        });

        return new DryRunReport
        {
            Metadata = new DryRunMetadata
            {
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ApiBaseUri = context.ApiBaseUri,
                ExtensionMode = context.ExtensionMode,
                UsersTotal = context.Users.Count,
                UsersWithProfileExtension = context.UsersWithProfileExtension.Count,
                DistinctProfileExtensions = context.ProfileExtensionNumbers.Count,
                ExtensionsLoaded = context.Extensions.Count,
            },
            Summary = new DryRunSummary
            {
                TotalRows = rows.Count,
                MissingAssignments = missing.Count,
                Discrepancies = disc.Count,
                DuplicateUserRows = dupsUsers.Count,
                DuplicateExtensionRows = dupsExts.Count,
            },
            Rows = rows,
            MissingAssignments = missing,
            Discrepancies = disc,
            DuplicateUserAssignments = dupsUsers,
            DuplicateExtensionRecords = dupsExts,
        };
    }

    public async Task<PatchResult> PatchMissingAsync(
        AuditContext context,
        PatchOptions options,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var missing = FindMissingExtensionAssignments(context);
        var dupUsers = FindDuplicateUserExtensionAssignments(context);
        var dupSet = dupUsers.Select(d => d.ProfileExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var updated = new List<PatchUpdatedRow>();
        var skipped = new List<PatchSkippedRow>();
        var failed = new List<PatchFailedRow>();

        var done = 0;
        var i = 0;

        foreach (var m in missing)
        {
            ct.ThrowIfCancellationRequested();
            i++;

            if (options.MaxFailures > 0 && failed.Count >= options.MaxFailures)
            {
                skipped.Add(new PatchSkippedRow
                {
                    Reason = "MaxFailuresReached",
                    UserId = m.UserId,
                    User = context.UserDisplayById.GetValueOrDefault(m.UserId, m.UserId),
                    Extension = m.ProfileExtension,
                });

                foreach (var rest in missing.Skip(i))
                {
                    skipped.Add(new PatchSkippedRow
                    {
                        Reason = "MaxFailuresReached",
                        UserId = rest.UserId,
                        User = context.UserDisplayById.GetValueOrDefault(rest.UserId, rest.UserId),
                        Extension = rest.ProfileExtension,
                    });
                }
                break;
            }

            if (dupSet.Contains(m.ProfileExtension))
            {
                skipped.Add(new PatchSkippedRow
                {
                    Reason = "DuplicateUserAssignment",
                    UserId = m.UserId,
                    User = context.UserDisplayById.GetValueOrDefault(m.UserId, m.UserId),
                    Extension = m.ProfileExtension,
                });
                continue;
            }

            if (options.MaxUpdates > 0 && done >= options.MaxUpdates)
            {
                skipped.Add(new PatchSkippedRow
                {
                    Reason = "MaxUpdatesReached",
                    UserId = m.UserId,
                    User = context.UserDisplayById.GetValueOrDefault(m.UserId, m.UserId),
                    Extension = m.ProfileExtension,
                });
                continue;
            }

            progress?.Report($"Patching {i}/{missing.Count}: {context.UserDisplayById.GetValueOrDefault(m.UserId, m.UserId)}");

            try
            {
                var user = await _api.GetUserAsync(context.ApiBaseUri, context.AccessToken, m.UserId, ct).ConfigureAwait(false);
                if (user?.Id is null) { throw new InvalidOperationException($"Failed to GET user {m.UserId}."); }

                var addresses = user.Addresses is null ? new List<GcUserAddress>() : CloneAddresses(user.Addresses);
                EnsureWorkPhoneAddress(addresses, out var idx);

                var before = addresses[idx].Extension;
                addresses[idx].Extension = m.ProfileExtension;

                _log.Log(LogLevel.Info, "Preparing user extension PATCH", new { UserId = m.UserId, Before = before, After = m.ProfileExtension });

                var version = user.Version + 1;

                if (options.WhatIf)
                {
                    updated.Add(new PatchUpdatedRow
                    {
                        UserId = m.UserId,
                        User = context.UserDisplayById.GetValueOrDefault(m.UserId),
                        Extension = m.ProfileExtension,
                        Status = "WhatIf",
                        PatchedVersion = version,
                    });
                    done++;
                    continue;
                }

                var patch = new GcUserPatch
                {
                    Version = version,
                    Addresses = addresses,
                };

                await _api.PatchUserAsync(context.ApiBaseUri, context.AccessToken, m.UserId, patch, ct).ConfigureAwait(false);

                updated.Add(new PatchUpdatedRow
                {
                    UserId = m.UserId,
                    User = context.UserDisplayById.GetValueOrDefault(m.UserId),
                    Extension = m.ProfileExtension,
                    Status = "Patched",
                    PatchedVersion = version,
                });

                done++;
                if (options.SleepMsBetween > 0)
                {
                    await Task.Delay(options.SleepMsBetween, ct).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                failed.Add(new PatchFailedRow
                {
                    UserId = m.UserId,
                    User = context.UserDisplayById.GetValueOrDefault(m.UserId),
                    Extension = m.ProfileExtension,
                    Error = ex.Message,
                });
                _log.Log(LogLevel.Error, "Patch failed", new { UserId = m.UserId, Extension = m.ProfileExtension, Error = ex.Message }, ex: ex);
            }
        }

        return new PatchResult
        {
            Summary = new PatchSummary
            {
                MissingFound = missing.Count,
                Updated = updated.Count,
                Skipped = skipped.Count,
                Failed = failed.Count,
                WhatIf = options.WhatIf,
            },
            Updated = updated,
            Skipped = skipped,
            Failed = failed,
        };
    }

    public async Task<(string Status, int Version)> PatchUserExtensionAsync(
        AuditContext context,
        string userId,
        string? extensionNumber,
        bool whatIf,
        CancellationToken ct)
    {
        var user = await _api.GetUserAsync(context.ApiBaseUri, context.AccessToken, userId, ct).ConfigureAwait(false);
        if (user?.Id is null) { throw new InvalidOperationException($"Failed to GET user {userId}."); }

        var addresses = user.Addresses is null ? new List<GcUserAddress>() : CloneAddresses(user.Addresses);
        EnsureWorkPhoneAddress(addresses, out var idx);

        string? before;
        if (context.AuditKind == AuditNumberKind.Did)
        {
            before = GetAddressField(addresses[idx]);
            SetAddressField(addresses[idx], extensionNumber);
        }
        else
        {
            before = addresses[idx].Extension;
            addresses[idx].Extension = extensionNumber;
        }

        _log.Log(LogLevel.Info, "Preparing user PATCH", new
        {
            AuditKind = context.AuditKind.ToString(),
            UserId = userId,
            Before = before,
            After = extensionNumber ?? "(null)",
        });

        var version = user.Version + 1;
        if (whatIf)
        {
            return ("WhatIf", version);
        }

        var patch = new GcUserPatch
        {
            Version = version,
            Addresses = addresses,
        };

        await _api.PatchUserAsync(context.ApiBaseUri, context.AccessToken, userId, patch, ct).ConfigureAwait(false);
        return ("Patched", version);
    }

    public static string? GetUserProfileExtension(GcUser user)
    {
        if (user.Addresses is null || user.Addresses.Count == 0) { return null; }

        var phones = user.Addresses.Where(a => a is not null && string.Equals(a.MediaType, "PHONE", StringComparison.OrdinalIgnoreCase)).ToList();
        if (phones.Count == 0) { return null; }

        var work = phones.FirstOrDefault(a => string.Equals(a.Type, "WORK", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(a.Extension));
        if (work?.Extension is not null) { return work.Extension; }

        var any = phones.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Extension));
        return any?.Extension;
    }

    public static string? GetUserProfileDid(GcUser user)
    {
        if (user.Addresses is null || user.Addresses.Count == 0) { return null; }

        var phones = user.Addresses.Where(a => a is not null && string.Equals(a.MediaType, "PHONE", StringComparison.OrdinalIgnoreCase)).ToList();
        if (phones.Count == 0) { return null; }

        string? ReadDid(GcUserAddress a) => NormalizeDid(GetAddressField(a));

        var work = phones.FirstOrDefault(a => string.Equals(a.Type, "WORK", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(ReadDid(a)));
        if (work is not null) { return ReadDid(work); }

        var any = phones.FirstOrDefault(a => !string.IsNullOrWhiteSpace(ReadDid(a)));
        return any is null ? null : ReadDid(any);
    }

    private static GcExtension? MapDidToExtension(GcDid d)
    {
        var n = d.GetDidNumber();
        if (string.IsNullOrWhiteSpace(n)) { return null; }

        return new GcExtension
        {
            Id = d.Id,
            Number = NormalizeDid(n),
            OwnerType = d.OwnerType,
            Owner = d.Owner,
            ExtensionPool = d.DidPool,
        };
    }

    private static string? GetAddressField(GcUserAddress a)
    {
        if (a.Extra is null || a.Extra.Count == 0) { return null; }

        if (TryGetExtraString(a.Extra, "address", out var v)) { return v; }
        if (TryGetExtraString(a.Extra, "phoneNumber", out v)) { return v; }
        if (TryGetExtraString(a.Extra, "number", out v)) { return v; }
        return null;
    }

    private static void SetAddressField(GcUserAddress a, string? value)
    {
        a.Extra ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        a.Extra["address"] = value;
    }

    private static bool TryGetExtraString(IDictionary<string, object?> extra, string key, out string? value)
    {
        value = null;
        if (!extra.TryGetValue(key, out var raw) || raw is null) { return false; }

        if (raw is string s)
        {
            value = s;
            return true;
        }

        if (raw is System.Text.Json.JsonElement el)
        {
            if (el.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                value = el.GetString();
                return true;
            }
            if (el.ValueKind is System.Text.Json.JsonValueKind.Number or System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
            {
                value = el.ToString();
                return true;
            }
            return false;
        }

        value = raw.ToString();
        return true;
    }

    private static string? NormalizeDid(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return null; }
        var s = raw.Trim();

        // Remove common formatting. Keep digits and a leading '+'.
        var sb = new System.Text.StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '+' && sb.Length == 0) { sb.Append(c); continue; }
            if (char.IsDigit(c)) { sb.Append(c); }
        }

        var normalized = sb.ToString();
        return string.IsNullOrWhiteSpace(normalized) ? s : normalized;
    }

    private static string MakeUserDisplay(GcUser u)
    {
        var name = string.IsNullOrWhiteSpace(u.Name) ? "(no name)" : u.Name.Trim();
        if (!string.IsNullOrWhiteSpace(u.Email))
        {
            return $"{name} <{u.Email.Trim()}>";
        }
        return name;
    }

    private static List<GcUserAddress> CloneAddresses(List<GcUserAddress> src)
        => src.Select(a => new GcUserAddress
        {
            MediaType = a.MediaType,
            Type = a.Type,
            Extension = a.Extension,
            Extra = new Dictionary<string, object?>(a.Extra, StringComparer.OrdinalIgnoreCase),
        }).ToList();

    private static void EnsureWorkPhoneAddress(List<GcUserAddress> addresses, out int idx)
    {
        idx = addresses.FindIndex(a => string.Equals(a.MediaType, "PHONE", StringComparison.OrdinalIgnoreCase) && string.Equals(a.Type, "WORK", StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) { return; }

        // If there is any PHONE address but no WORK phone, create a WORK phone entry (do not overwrite HOME/OTHER).
        var anyPhone = addresses.FirstOrDefault(a => string.Equals(a.MediaType, "PHONE", StringComparison.OrdinalIgnoreCase));
        if (anyPhone is not null)
        {
            addresses.Add(new GcUserAddress
            {
                MediaType = "PHONE",
                Type = "WORK",
                Extension = null,
                Extra = new Dictionary<string, object?>(anyPhone.Extra, StringComparer.OrdinalIgnoreCase),
            });
            idx = addresses.Count - 1;
            return;
        }

        addresses.Add(new GcUserAddress { MediaType = "PHONE", Type = "WORK", Extension = null });
        idx = addresses.Count - 1;
    }
}
