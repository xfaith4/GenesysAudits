using System.Text.Json.Serialization;

namespace GcExtensionAuditMaui.Models.Api;

public sealed class PagedResponse<T>
{
    [JsonPropertyName("pageCount")]
    public int PageCount { get; set; }

    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("entities")]
    public List<T> Entities { get; set; } = new();
}

