namespace JaxaRainmap.Models;

public class PrecipitationFrame
{
    public string Id { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public string CogUrl { get; set; } = string.Empty;
    public string CollectionId { get; set; } = string.Empty;
    public double[]? Bbox { get; set; }

    public string DisplayLabel =>
        DateTime.ToString("yyyy-MM-dd HH:mm UTC");
}
