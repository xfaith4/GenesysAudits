namespace GcExtensionAuditMaui.Models.Audit;

public sealed class UserWithProfileExtensionRow
{
    public required string UserId { get; init; }
    public required string? UserName { get; init; }
    public required string? UserEmail { get; init; }
    public required string? UserState { get; init; }
    public required string ProfileExtension { get; init; }
}

