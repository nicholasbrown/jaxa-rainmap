namespace JaxaRainmap.Models;

public class MapSettings
{
    public double CenterLat { get; set; } = 20.0;
    public double CenterLon { get; set; } = 136.0;
    public int Zoom { get; set; } = 3;
    public DateTime SelectedDate { get; set; } = DateTime.UtcNow.AddDays(-1);
    public int SelectedHour { get; set; } = 0;
    public string CollectionType { get; set; } = "daily";
    public string ColorPalette { get; set; } = "jma";
    public bool IsPlaying { get; set; } = false;
    public double PlaybackSpeed { get; set; } = 1.0;
}

public static class GsmapCollections
{
    public const string Daily = "JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_daily";
    public const string Monthly = "JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_monthly";
    public const string MonthlyNormal = "JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_monthly-normal";
    public const string HalfMonthlyNormal = "JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_half-monthly-normal";

    public static readonly string BaseUrl = "https://s3.ap-northeast-1.wasabisys.com/je-pds/cog/v1";

    public static string GetCollectionUrl(string collectionId) =>
        $"{BaseUrl}/{collectionId}/collection.json";
}

public record RegionPreset(string Name, double South, double West, double North, double East, int Zoom)
{
    public static readonly RegionPreset Global = new("Global", -60, -180, 60, 180, 2);
    public static readonly RegionPreset Asia = new("Asia", -10, 60, 55, 150, 4);
    public static readonly RegionPreset Japan = new("Japan", 24, 122, 46, 153, 5);
    public static readonly RegionPreset Americas = new("Americas", -55, -130, 60, -30, 3);
    public static readonly RegionPreset Europe = new("Europe", 35, -15, 72, 45, 4);
    public static readonly RegionPreset Africa = new("Africa", -35, -20, 38, 55, 4);
    public static readonly RegionPreset Oceania = new("Oceania", -50, 100, 10, 180, 4);

    public static readonly RegionPreset[] All = { Global, Asia, Japan, Americas, Europe, Africa, Oceania };
}
