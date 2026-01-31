using System.Text.Json.Serialization;

namespace GcExtensionAuditMaui.Models.Api;

public sealed class GcExtensionPool
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

