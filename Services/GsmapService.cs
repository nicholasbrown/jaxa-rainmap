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

        var itemLinks = collection.Links
            .Where(l => l.Rel == "item")
            .ToList();

        if (itemLinks.Any())
        {
            frames = await BuildFramesFromItemLinksAsync(itemLinks, collectionId, startDate, endDate);
        }
        else
        {
            frames = BuildFramesFromDatePattern(collectionId, collectionType, startDate, endDate);
        }

        return frames.OrderBy(f => f.DateTime).ToList();
    }

    public async Task<PrecipitationFrame?> GetLatestFrameAsync(string collectionType)
    {
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var frames = await GetFramesAsync(collectionType, yesterday, yesterday);
        return frames.LastOrDefault();
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

        if (collectionType == "daily")
        {
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                var dateStr = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                frames.Add(new PrecipitationFrame
                {
                    Id = $"{collectionId}_{dateStr}",
                    DateTime = date,
                    CogUrl = $"{baseUrl}/{collectionId}/{dateStr}.tif",
                    CollectionId = collectionId,
                    Bbox = new double[] { -180, -60, 180, 60 }
                });
            }
        }
        else if (collectionType == "monthly")
        {
            for (var date = new DateTime(startDate.Year, startDate.Month, 1);
                 date <= endDate;
                 date = date.AddMonths(1))
            {
                var dateStr = date.ToString("yyyyMM", CultureInfo.InvariantCulture);
                frames.Add(new PrecipitationFrame
                {
                    Id = $"{collectionId}_{dateStr}",
                    DateTime = date,
                    CogUrl = $"{baseUrl}/{collectionId}/{dateStr}.tif",
                    CollectionId = collectionId,
                    Bbox = new double[] { -180, -60, 180, 60 }
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
