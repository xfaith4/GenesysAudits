using System.Text.Json.Serialization;

namespace GcExtensionAuditMaui.Models.Api;

public sealed class GcExtension
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("ownerType")]
    public string? OwnerType { get; set; }

    [JsonPropertyName("owner")]
    public GcExtensionOwner? Owner { get; set; }

    [JsonPropertyName("extensionPool")]
    public GcExtensionPool? ExtensionPool { get; set; }
}

