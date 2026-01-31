using System.Text.Json.Serialization;

namespace GcExtensionAuditMaui.Models.Api;

public sealed class GcUserPatch
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("addresses")]
    public List<GcUserAddress>? Addresses { get; set; }
}

