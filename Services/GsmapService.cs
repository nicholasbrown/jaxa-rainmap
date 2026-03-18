using System.Globalization;
using System.Net.Http.Json;
using JaxaRainmap.Models;

namespace JaxaRainmap.Services;

public class GsmapService : IGsmapService
{
    private readonly HttpClient _http;
    private readonly ICacheService _cache;

    public GsmapService(HttpClient http, ICacheService cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<StacCollection?> GetCollectionMetadataAsync(string collectionId)
    {
        var cacheKey = $"collection:{collectionId}";
        var cached = _cache.Get<StacCollection>(cacheKey);
        if (cached is not null) return cached;

        try
        {
            var url = GsmapCollections.GetCollectionUrl(collectionId);
            var collection = await _http.GetFromJsonAsync<StacCollection>(url);
            if (collection is not null)
            {
                _cache.Set(cacheKey, collection, TimeSpan.FromHours(24));
            }
            return collection;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to fetch collection {collectionId}: {ex.Message}");
            return null;
        }
    }

    public async Task<List<PrecipitationFrame>> GetFramesAsync(
        string collectionType, DateTime startDate, DateTime endDate)
    {
        var collectionId = ResolveCollectionId(collectionType);
        var frames = new List<PrecipitationFrame>();

        var collection = await GetCollectionMetadataAsync(collectionId);
        if (collection is null) return frames;

        if (collectionType == "daily")
        {
            frames = await BuildDailyFramesFromCatalogAsync(collectionId, startDate, endDate);
        }
        else if (collectionType == "monthly")
        {
            frames = BuildFramesFromDatePattern(collectionId, collectionType, startDate, endDate);
        }
        else
        {
            frames = BuildFramesFromDatePattern(collectionId, collectionType, startDate, endDate);
        }

        return frames.OrderBy(f => f.DateTime).ToList();
    }

    public async Task<PrecipitationFrame?> GetLatestFrameAsync(string collectionType)
    {
        var collectionId = ResolveCollectionId(collectionType);
        for (int daysBack = 3; daysBack <= 14; daysBack++)
        {
            var date = DateTime.UtcNow.AddDays(-daysBack);
            var frames = await BuildDailyFramesFromCatalogAsync(collectionId, date, date);
            if (frames.Count > 0) return frames.Last();
        }
        return null;
    }

    /// <summary>
    /// Fetches the STAC monthly catalog to discover which days actually have data,
    /// then builds COG URLs only for days that exist.
    /// </summary>
    private async Task<List<PrecipitationFrame>> BuildDailyFramesFromCatalogAsync(
        string collectionId, DateTime startDate, DateTime endDate)
    {
        var frames = new List<PrecipitationFrame>();
        var baseUrl = GsmapCollections.BaseUrl;

        const string eastHemi = "E000.00-E180.00/E000.00-S90.00-E180.00-N90.00";
        const string westHemi = "W180.00-E000.00/W180.00-S90.00-E000.00-N90.00";

        var currentMonth = new DateTime(startDate.Year, startDate.Month, 1);
        var lastMonth = new DateTime(endDate.Year, endDate.Month, 1);

        while (currentMonth <= lastMonth)
        {
            var monthStr = currentMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var catalogUrl = $"{baseUrl}/{collectionId}/{monthStr}/catalog.json";

            try
            {
                var cacheKey = $"catalog:{catalogUrl}";
                var catalog = _cache.Get<StacCollection>(cacheKey);

                if (catalog is null)
                {
                    catalog = await _http.GetFromJsonAsync<StacCollection>(catalogUrl);
                    if (catalog is not null)
                    {
                        _cache.Set(cacheKey, catalog, TimeSpan.FromHours(1));
                    }
                }

                if (catalog is null)
                {
                    currentMonth = currentMonth.AddMonths(1);
                    continue;
                }

                // Extract available days from child links (e.g. "./15/catalog.json")
                var dayLinks = catalog.Links
                    .Where(l => l.Rel == "child")
                    .Select(l => l.Href.TrimStart('.', '/').TrimEnd('/'))
                    .Where(h => h.EndsWith("/catalog.json"))
                    .Select(h => h.Split('/')[0])
                    .Where(d => int.TryParse(d, out _))
                    .Select(d => int.Parse(d))
                    .OrderBy(d => d)
                    .ToList();

                foreach (var day in dayLinks)
                {
                    try
                    {
                        var date = new DateTime(currentMonth.Year, currentMonth.Month, day);
                        if (date < startDate.Date || date > endDate.Date) continue;

                        var pathBase = $"{baseUrl}/{collectionId}/{monthStr}/{day}/0";

                        frames.Add(new PrecipitationFrame
                        {
                            Id = $"{collectionId}_{date:yyyyMMdd}",
                            DateTime = date,
                            CogUrl = $"{pathBase}/{eastHemi}-PRECIP.tiff",
                            AdditionalCogUrls = new List<string> { $"{pathBase}/{westHemi}-PRECIP.tiff" },
                            CollectionId = collectionId,
                            Bbox = new double[] { -180, -90, 180, 90 }
                        });
                    }
                    catch { /* skip invalid day numbers */ }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to fetch catalog for {monthStr}: {ex.Message}");
            }

            currentMonth = currentMonth.AddMonths(1);
        }

        return frames;
    }

    private async Task<List<PrecipitationFrame>> BuildFramesFromItemLinksAsync(
        List<StacLink> itemLinks, string collectionId, DateTime startDate, DateTime endDate)
    {
        var frames = new List<PrecipitationFrame>();

        foreach (var link in itemLinks)
        {
            try
            {
                var cacheKey = $"item:{link.Href}";
                var item = _cache.Get<StacItem>(cacheKey);

                if (item is null)
                {
                    item = await _http.GetFromJsonAsync<StacItem>(link.Href);
                    if (item is not null)
                    {
                        _cache.Set(cacheKey, item, TimeSpan.FromHours(6));
                    }
                }

                if (item is null) continue;

                var dt = ParseItemDateTime(item);
                if (dt is null || dt < startDate || dt > endDate) continue;

                var cogAsset = item.Assets.Values
                    .FirstOrDefault(a => a.Type?.Contains("geotiff") == true
                                     || a.Href.EndsWith(".tif")
                                     || a.Href.EndsWith(".tiff"));

                if (cogAsset is null) continue;

                frames.Add(new PrecipitationFrame
                {
                    Id = item.Id,
                    DateTime = dt.Value,
                    CogUrl = cogAsset.Href,
                    CollectionId = collectionId,
                    Bbox = item.Bbox?.ToArray()
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to fetch item {link.Href}: {ex.Message}");
            }
        }

        return frames;
    }

    private static List<PrecipitationFrame> BuildFramesFromDatePattern(
        string collectionId, string collectionType, DateTime startDate, DateTime endDate)
    {
        var frames = new List<PrecipitationFrame>();
        var baseUrl = GsmapCollections.BaseUrl;

        const string eastHemi = "E000.00-E180.00/E000.00-S90.00-E180.00-N90.00";
        const string westHemi = "W180.00-E000.00/W180.00-S90.00-E000.00-N90.00";

        if (collectionType == "daily")
        {
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var monthStr = date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                var dayStr = date.Day.ToString(CultureInfo.InvariantCulture);
                var pathBase = $"{baseUrl}/{collectionId}/{monthStr}/{dayStr}/0";

                frames.Add(new PrecipitationFrame
                {
                    Id = $"{collectionId}_{date:yyyyMMdd}",
                    DateTime = date,
                    CogUrl = $"{pathBase}/{eastHemi}-PRECIP.tiff",
                    AdditionalCogUrls = new List<string> { $"{pathBase}/{westHemi}-PRECIP.tiff" },
                    CollectionId = collectionId,
                    Bbox = new double[] { -180, -90, 180, 90 }
                });
            }
        }
        else if (collectionType == "monthly")
        {
            for (var date = new DateTime(startDate.Year, startDate.Month, 1);
                 date <= endDate;
                 date = date.AddMonths(1))
            {
                var monthStr = date.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                var pathBase = $"{baseUrl}/{collectionId}/{monthStr}/0";

                frames.Add(new PrecipitationFrame
                {
                    Id = $"{collectionId}_{date:yyyyMM}",
                    DateTime = date,
                    CogUrl = $"{pathBase}/{eastHemi}-PRECIP.tiff",
                    AdditionalCogUrls = new List<string> { $"{pathBase}/{westHemi}-PRECIP.tiff" },
                    CollectionId = collectionId,
                    Bbox = new double[] { -180, -90, 180, 90 }
                });
            }
        }

        return frames;
    }

    private static DateTime? ParseItemDateTime(StacItem item)
    {
        var dtString = item.Properties.DateTime
                    ?? item.Properties.StartDateTime;

        if (string.IsNullOrEmpty(dtString)) return null;

        if (DateTime.TryParse(dtString, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            return dt;
        }

        return null;
    }

    private static string ResolveCollectionId(string collectionType) => collectionType switch
    {
        "daily" => GsmapCollections.Daily,
        "monthly" => GsmapCollections.Monthly,
        "monthly-normal" => GsmapCollections.MonthlyNormal,
        "half-monthly-normal" => GsmapCollections.HalfMonthlyNormal,
        _ => GsmapCollections.Daily
    };
}
