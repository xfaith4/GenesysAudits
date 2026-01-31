using System.Text.Json.Serialization;

namespace GcExtensionAuditMaui.Models.Api;

public sealed class GcUserAddress
{
    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("extension")]
    public string? Extension { get; set; }

    // Keep unknown fields to avoid losing data when we PATCH the full addresses array.
    [JsonExtensionData]
    public Dictionary<string, object?> Extra { get; set; } = new();
}

