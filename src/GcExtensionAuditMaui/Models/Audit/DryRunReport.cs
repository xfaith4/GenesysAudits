namespace GcExtensionAuditMaui.Models.Audit;

public sealed class DryRunReport
{
    public required DryRunMetadata Metadata { get; init; }
    public required DryRunSummary Summary { get; init; }

    public required IReadOnlyList<DryRunRow> Rows { get; init; }

    public required IReadOnlyList<MissingAssignmentRow> MissingAssignments { get; init; }
    public required IReadOnlyList<DiscrepancyRow> Discrepancies { get; init; }
    public required IReadOnlyList<DuplicateUserAssignmentRow> DuplicateUserAssignments { get; init; }
    public required IReadOnlyList<DuplicateExtensionRecordRow> DuplicateExtensionRecords { get; init; }
}

public sealed class DryRunMetadata
{
    public required string GeneratedAt { get; init; }
    public required string ApiBaseUri { get; init; }
    public required string ExtensionMode { get; init; }
    public required int UsersTotal { get; init; }
    public required int UsersWithProfileExtension { get; init; }
    public required int DistinctProfileExtensions { get; init; }
    public required int ExtensionsLoaded { get; init; }
}

public sealed class DryRunSummary
{
    public required int TotalRows { get; init; }
    public required int MissingAssignments { get; init; }
    public required int Discrepancies { get; init; }
    public required int DuplicateUserRows { get; init; }
    public required int DuplicateExtensionRows { get; init; }
}

public sealed class DryRunRow
{
    public required string Action { get; init; }
    public required string Category { get; init; }
    public string? UserId { get; init; }
    public string? User { get; init; }
    public required string ProfileExtension { get; init; }

    public bool? Before_ExtensionRecordFound { get; init; }
    public string? Before_ExtOwner { get; init; }

    public required string After_Expected { get; init; }
    public required string Notes { get; init; }
}

