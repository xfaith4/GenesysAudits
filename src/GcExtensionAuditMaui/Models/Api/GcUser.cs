using System.Text.Json.Serialization;

namespace GcExtensionAuditMaui.Models.Api;

public sealed class GcUser
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("addresses")]
    public List<GcUserAddress>? Addresses { get; set; }
}

