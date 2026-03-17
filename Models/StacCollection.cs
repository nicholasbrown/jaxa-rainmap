using System.Text.Json.Serialization;

namespace JaxaRainmap.Models;

public class StacCollection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("links")]
    public List<StacLink> Links { get; set; } = new();

    [JsonPropertyName("extent")]
    public StacExtent? Extent { get; set; }
}

public class StacLink
{
    [JsonPropertyName("rel")]
    public string Rel { get; set; } = string.Empty;

    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

public class StacExtent
{
    [JsonPropertyName("spatial")]
    public StacSpatialExtent? Spatial { get; set; }

    [JsonPropertyName("temporal")]
    public StacTemporalExtent? Temporal { get; set; }
}

public class StacSpatialExtent
{
    [JsonPropertyName("bbox")]
    public List<List<double>>? Bbox { get; set; }
}

public class StacTemporalExtent
{
    [JsonPropertyName("interval")]
    public List<List<string?>>? Interval { get; set; }
}
