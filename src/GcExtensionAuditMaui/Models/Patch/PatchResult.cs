namespace GcExtensionAuditMaui.Models.Patch;

public sealed class PatchResult
{
    public required PatchSummary Summary { get; init; }
    public required IReadOnlyList<PatchUpdatedRow> Updated { get; init; }
    public required IReadOnlyList<PatchSkippedRow> Skipped { get; init; }
    public required IReadOnlyList<PatchFailedRow> Failed { get; init; }
}

public sealed class PatchSummary
{
    public int MissingFound { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public bool WhatIf { get; init; }
    
    // For PatchFromPlan
    public int TotalPlanItems { get; init; }
    public int ItemsTargeted { get; init; }
}

public sealed class PatchUpdatedRow
{
    public required string UserId { get; init; }
    public required string? User { get; init; }
    public required string Extension { get; init; }
    public required string Status { get; init; } // Patched | WhatIf
    public int PatchedVersion { get; init; }
}

public sealed class PatchSkippedRow
{
    public required string Reason { get; init; }
    public required string UserId { get; init; }
    public required string? User { get; init; }
    public required string Extension { get; init; }
}

public sealed class PatchFailedRow
{
    public required string UserId { get; init; }
    public required string? User { get; init; }
    public required string Extension { get; init; }
    public required string Error { get; init; }
}

