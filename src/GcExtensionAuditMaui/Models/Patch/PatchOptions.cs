namespace GcExtensionAuditMaui.Models.Patch;

public sealed class PatchOptions
{
    public bool WhatIf { get; init; } = true;
    public int SleepMsBetween { get; init; } = 150;
    public int MaxUpdates { get; init; } = 0; // 0 = unlimited
    public int MaxFailures { get; init; } = 0; // 0 = unlimited
}

public sealed class PatchFromPlanOptions
{
    public bool WhatIf { get; init; } = true;
    public int SleepMsBetween { get; init; } = 150;
    public int MaxUpdates { get; init; } = 0; // 0 = unlimited
    public int MaxFailures { get; init; } = 0; // 0 = unlimited
    
    // Category filters - if all false, nothing is patched
    public bool IncludeMissing { get; init; } = true;
    public bool IncludeDuplicateUser { get; init; } = true;
    public bool IncludeDiscrepancy { get; init; } = true;
    public bool IncludeReassert { get; init; } = false;
}
