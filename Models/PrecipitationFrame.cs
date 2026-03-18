namespace JaxaRainmap.Models;

public class PrecipitationFrame
{
    public string Id { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public string CogUrl { get; set; } = string.Empty;
    public string CollectionId { get; set; } = string.Empty;
    public double[]? Bbox { get; set; }

    /// <summary>
    /// Additional COG URLs for multi-tile frames (e.g. East + West hemisphere).
    /// </summary>
    public List<string> AdditionalCogUrls { get; set; } = new();

    public string DisplayLabel =>
        DateTime.ToString("yyyy-MM-dd HH:mm UTC");
}
