namespace GcExtensionAuditMaui.Models.Patch;

/// <summary>
/// Result of post-patch verification comparing expected state against actual state
/// </summary>
public sealed class VerificationResult
{
    public required int TotalVerified { get; init; }
    public required int Confirmed { get; init; }
    public required int Mismatched { get; init; }
    public required int UserNotFound { get; init; }
    public required IReadOnlyList<VerificationItem> Items { get; init; }
}

/// <summary>
/// Individual verification item showing expected vs actual state
/// </summary>
public sealed class VerificationItem
{
    public required string UserId { get; init; }
    public required string? UserDisplay { get; init; }
    public required string ExpectedExtension { get; init; }
    public required string? ActualExtension { get; init; }
    public required VerificationStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum VerificationStatus
{
    Confirmed,      // Extension matches expected
    Mismatch,       // Extension doesn't match expected
    UserNotFound,   // User could not be retrieved
    Error           // Other error occurred
}
