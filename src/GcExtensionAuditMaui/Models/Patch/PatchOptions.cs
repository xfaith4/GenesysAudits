namespace GcExtensionAuditMaui.Models.Patch;

public sealed class PatchOptions
{
    public bool WhatIf { get; init; } = true;
    public int SleepMsBetween { get; init; } = 150;
    public int MaxUpdates { get; init; } = 0; // 0 = unlimited
    public int MaxFailures { get; init; } = 0; // 0 = unlimited
}
