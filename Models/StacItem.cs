using System.Text.Json.Serialization;

namespace JaxaRainmap.Models;

public class StacItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("bbox")]
    public List<double>? Bbox { get; set; }

    [JsonPropertyName("properties")]
    public StacItemProperties Properties { get; set; } = new();

    [JsonPropertyName("links")]
    public List<StacLink> Links { get; set; } = new();

    [JsonPropertyName("assets")]
    public Dictionary<string, StacAsset> Assets { get; set; } = new();
}

public class StacItemProperties
{
    [JsonPropertyName("datetime")]
    public string? DateTime { get; set; }

    [JsonPropertyName("start_datetime")]
    public string? StartDateTime { get; set; }

    [JsonPropertyName("end_datetime")]
    public string? EndDateTime { get; set; }
}

public class StacAsset
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("roles")]
    public List<string>? Roles { get; set; }
}
