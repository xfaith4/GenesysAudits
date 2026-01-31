using GcExtensionAuditMaui.Models.Audit;
using GcExtensionAuditMaui.Models.Planning;

namespace GcExtensionAuditMaui.Services;

public sealed class FixupPlannerService
{
    private readonly AuditService _audit;

    public FixupPlannerService(AuditService audit)
    {
        _audit = audit;
    }

    public FixupPlan BuildPlan(AuditContext context, bool reassertConsistentUsers, bool preferAssignAvailableOverBlank)
    {
        var duplicatesUsers = _audit.FindDuplicateUserExtensionAssignments(context);
        var duplicatesExts = _audit.FindDuplicateExtensionRecords(context);
        var discrepancies = _audit.FindExtensionDiscrepancies(context);
        var missing = _audit.FindMissingExtensionAssignments(context);

        var dupUserExtSet = duplicatesUsers.Select(d => d.ProfileExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dupExtNumSet = duplicatesExts.Select(d => d.ExtensionNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // “Available” extensions heuristic:
        // - must have exactly one extension record
        // - ownerType empty OR ownerType==USER with empty owner id (unassigned)
        // - not already used by any user profile extension
        // - not part of duplicate extension records
        var usedByProfiles = context.UsersWithProfileExtension.Select(r => r.ProfileExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var available = new List<string>();

        foreach (var (n, list) in context.ExtensionsByNumber)
        {
            if (dupExtNumSet.Contains(n)) { continue; }
            if (list.Count != 1) { continue; }
            if (usedByProfiles.Contains(n)) { continue; }

            var e = list[0];
            var ownerType = e.OwnerType ?? "";
            var ownerId = e.Owner?.Id ?? "";

            var unassigned =
                string.IsNullOrWhiteSpace(ownerType)
                || (string.Equals(ownerType, "USER", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(ownerId));

            if (unassigned)
            {
                available.Add(n);
            }
        }

        available.Sort(StringComparer.OrdinalIgnoreCase);
        var availableQueue = new Queue<string>(available);

        string? TakeAvailable()
            => availableQueue.Count > 0 ? availableQueue.Dequeue() : null;

        var items = new List<FixupItem>();

        // Duplicates (Users): for each duplicate extension group, keep the first user, reassign others.
        var dupGroups = duplicatesUsers
            .GroupBy(d => d.ProfileExtension, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var g in dupGroups)
        {
            var ordered = g.OrderBy(x => x.UserName ?? "", StringComparer.OrdinalIgnoreCase).ThenBy(x => x.UserId, StringComparer.OrdinalIgnoreCase).ToList();
            if (ordered.Count == 0) { continue; }

            // Keep first by name (no action).
            for (var i = 1; i < ordered.Count; i++)
            {
                var u = ordered[i];
                var chosen = preferAssignAvailableOverBlank ? TakeAvailable() : null;

                items.Add(new FixupItem
                {
                    Category = "DuplicateUser",
                    UserId = u.UserId,
                    User = context.UserDisplayById.GetValueOrDefault(u.UserId, u.UserId),
                    CurrentExtension = u.ProfileExtension,
                    RecommendedExtension = chosen,
                    Action = chosen is null ? FixupActionType.ClearExtension : FixupActionType.AssignSpecific,
                    Notes = chosen is null ? "Duplicate profile extension; no available extension found." : "Duplicate profile extension; reassign to available number.",
                });
            }
        }

        // Missing: user has profile extension but no extension record exists.
        foreach (var m in missing.OrderBy(x => x.ProfileExtension, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.UserId, StringComparer.OrdinalIgnoreCase))
        {
            var chosen = preferAssignAvailableOverBlank ? TakeAvailable() : null;

            items.Add(new FixupItem
            {
                Category = "Missing",
                UserId = m.UserId,
                User = context.UserDisplayById.GetValueOrDefault(m.UserId, m.UserId),
                CurrentExtension = m.ProfileExtension,
                RecommendedExtension = chosen,
                Action = chosen is null ? FixupActionType.ClearExtension : FixupActionType.AssignSpecific,
                Notes = chosen is null ? "No extension record; clear profile extension." : "No extension record; assign available extension.",
            });
        }

        // Discrepancies: extension record exists but owned by non-user or mismatch.
        foreach (var d in discrepancies.OrderBy(x => x.ProfileExtension, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.UserId, StringComparer.OrdinalIgnoreCase))
        {
            // Default: reassert existing (safe sync attempt) unless user chooses otherwise.
            items.Add(new FixupItem
            {
                Category = "Discrepancy",
                UserId = d.UserId,
                User = context.UserDisplayById.GetValueOrDefault(d.UserId, d.UserId),
                CurrentExtension = d.ProfileExtension,
                RecommendedExtension = null,
                Action = FixupActionType.ReassertExisting,
                Notes = d.Issue == "OwnerMismatch"
                    ? "Extension owner mismatch; reassert user profile extension (sync attempt) or choose different action."
                    : "Extension owner type is not USER; reassert user profile extension (sync attempt) or choose different action.",
            });
        }

        // Optional: reassert users that look consistent (profile extension exists and is uniquely owned correctly).
        if (reassertConsistentUsers)
        {
            foreach (var u in context.UsersWithProfileExtension)
            {
                var n = u.ProfileExtension;
                if (dupUserExtSet.Contains(n)) { continue; }
                if (dupExtNumSet.Contains(n)) { continue; }

                if (!context.ExtensionsByNumber.TryGetValue(n, out var list) || list.Count != 1) { continue; }
                var e = list[0];
                if (!string.Equals(e.OwnerType, "USER", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (!string.Equals(e.Owner?.Id, u.UserId, StringComparison.OrdinalIgnoreCase)) { continue; }

                items.Add(new FixupItem
                {
                    Category = "Reassert",
                    UserId = u.UserId,
                    User = context.UserDisplayById.GetValueOrDefault(u.UserId, u.UserId),
                    CurrentExtension = n,
                    RecommendedExtension = n,
                    Action = FixupActionType.ReassertExisting,
                    Notes = "Consistent assignment; reassert user profile extension (sync attempt).",
                });
            }
        }

        var summary =
            $"DuplicatesUsers={duplicatesUsers.Count}; Missing={missing.Count}; Discrepancies={discrepancies.Count}; " +
            $"PlanItems={items.Count}; AvailableExtensions={available.Count}";

        return new FixupPlan
        {
            Items = items,
            AvailableExtensionNumbers = available,
            SummaryText = summary,
        };
    }
}

