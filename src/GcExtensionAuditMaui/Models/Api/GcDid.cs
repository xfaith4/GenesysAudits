using System.Text.Json.Serialization;

namespace GcExtensionAuditMaui.Models.Api;

public sealed class GcDid
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    // Common field name in Genesys Cloud for DIDs.
    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    // Some endpoints/variants may use "number".
    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("ownerType")]
    public string? OwnerType { get; set; }

    [JsonPropertyName("owner")]
    public GcExtensionOwner? Owner { get; set; }

    [JsonPropertyName("didPool")]
    public GcExtensionPool? DidPool { get; set; }

    public string? GetDidNumber()
        => string.IsNullOrWhiteSpace(PhoneNumber) ? Number : PhoneNumber;
}

