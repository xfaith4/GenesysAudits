using GcExtensionAuditMaui.Models.Api;

namespace GcExtensionAuditMaui.Models.Audit;

public sealed class AuditContext
{
    public required AuditNumberKind AuditKind { get; init; }

    public required string ApiBaseUri { get; init; }
    public required string AccessToken { get; init; }
    public required bool IncludeInactive { get; init; }

    public required IReadOnlyList<GcUser> Users { get; init; }

    public required IReadOnlyDictionary<string, GcUser> UsersById { get; init; }
    public required IReadOnlyDictionary<string, string> UserDisplayById { get; init; }
    public required IReadOnlyList<UserWithProfileExtensionRow> UsersWithProfileExtension { get; init; }
    public required IReadOnlyList<string> ProfileExtensionNumbers { get; init; }

    public required IReadOnlyList<GcExtension> Extensions { get; init; }
    public required string ExtensionMode { get; init; } // FULL

    // Reserved for any future caching strategy. Currently always null.
    public IReadOnlyDictionary<string, IReadOnlyList<GcExtension>>? ExtensionCache { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<GcExtension>> ExtensionsByNumber { get; init; }
}
