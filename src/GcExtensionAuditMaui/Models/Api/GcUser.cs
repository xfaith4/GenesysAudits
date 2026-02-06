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

    [JsonPropertyName("station")]
    public GcUserStation? Station { get; set; }

    [JsonPropertyName("locations")]
    public List<GcLocation>? Locations { get; set; }

    [JsonPropertyName("dateLastLogin")]
    public DateTime? DateLastLogin { get; set; }

    [JsonPropertyName("authorization")]
    public GcUserAuthorization? Authorization { get; set; }
}

public sealed class GcUserStation
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("selfUri")]
    public string? SelfUri { get; set; }
}

public sealed class GcLocation
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("selfUri")]
    public string? SelfUri { get; set; }
}

public sealed class GcUserAuthorization
{
    [JsonPropertyName("unusedRoles")]
    public List<GcRole>? UnusedRoles { get; set; }
}

public sealed class GcRole
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

