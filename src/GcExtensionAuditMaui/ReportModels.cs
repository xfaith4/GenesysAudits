// ### BEGIN: ReportModels

public sealed class ApiSnapshot
{
    public required string SheetName { get; init; }      // "Users", "Extensions", etc.
    public required string Endpoint { get; init; }       // "/api/v2/users", etc.

    // Each item is one "row" from the API response (typically an element of "entities" or similar).
    public required IReadOnlyList<System.Text.Json.JsonElement> Items { get; init; }
}

public sealed class IssueRow
{
    public required string IssueFound { get; init; }     // REQUIRED
    public required string CurrentState { get; init; }   // REQUIRED
    public required string NewState { get; init; }       // REQUIRED

    // Strongly recommended metadata:
    public string Severity { get; init; } = "Medium";    // Low/Medium/High
    public string EntityType { get; init; } = "";        // User/Extension/DID/Pool
    public string EntityId { get; init; } = "";
    public string EntityName { get; init; } = "";
    public string Field { get; init; } = "";
    public string Recommendation { get; init; } = "";
    public string SourceEndpoint { get; init; } = "";
}

// ### END: ReportModels
