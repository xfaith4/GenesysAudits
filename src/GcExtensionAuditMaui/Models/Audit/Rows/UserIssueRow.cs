namespace GcExtensionAuditMaui.Models.Audit;

public sealed class UserIssueRow
{
    public required string Issue { get; init; }
    public required string UserId { get; init; }
    public string? UserName { get; init; }
    public string? UserEmail { get; init; }
    public string? UserState { get; init; }
    public DateTime? DateLastLogin { get; init; }
}
