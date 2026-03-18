using System.Net;
using System.Text.Json;
using JaxaRainmap.Models;
using JaxaRainmap.Services;

namespace JaxaRainmap.Tests.Services;

public class GsmapServiceTests
{
    private static StacCollection CreateTestCollection()
    {
        return new StacCollection
        {
            Id = "JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_daily",
            Title = "GSMaP Daily",
            Links = new List<StacLink>(),
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent
                {
                    Bbox = new List<List<double>> { new() { -180, -60, 180, 60 } }
                }
            }
        };
    }

    private static StacCollection CreateMonthlyCatalog(int[] days)
    {
        var links = days.Select(d => new StacLink
        {
            Rel = "child",
            Href = $"./{d}/catalog.json",
            Type = "application/json"
        }).ToList();

        return new StacCollection
        {
            Id = "catalog",
            Title = "Monthly catalog",
            Links = links
        };
    }

    [Fact]
    public void ResolveCollectionId_ReturnsCorrectId()
    {
        Assert.Equal(
            "JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_daily",
            GsmapCollections.Daily);

        Assert.Equal(
            "JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_monthly",
            GsmapCollections.Monthly);
    }

    [Fact]
    public void GetCollectionUrl_BuildsCorrectUrl()
    {
        var url = GsmapCollections.GetCollectionUrl(GsmapCollections.Daily);
        Assert.Contains("je-pds/cog/v1", url);
        Assert.EndsWith("collection.json", url);
    }

    [Fact]
    public async Task GetFramesAsync_WithNoItems_ReturnsDatePatternFrames()
    {
        var collection = CreateTestCollection();
        var catalog = CreateMonthlyCatalog(new[] { 1, 2, 3 });

        var handler = new SmartTestHandler(collection, catalog);
        var http = new HttpClient(handler);
        var cache = new CacheService();
        var service = new GsmapService(http, cache, Microsoft.Extensions.Logging.Abstractions.NullLogger<GsmapService>.Instance);

        var start = new DateTime(2024, 1, 1);
        var end = new DateTime(2024, 1, 3);
        var frames = await service.GetFramesAsync("daily", start, end);

        Assert.Equal(3, frames.Count);
        Assert.Equal(start, frames[0].DateTime);
        Assert.All(frames, f => Assert.EndsWith("-PRECIP.tiff", f.CogUrl));
        Assert.All(frames, f => Assert.NotEmpty(f.AdditionalCogUrls));
    }

    [Fact]
    public async Task GetFramesAsync_ReturnsSortedByDate()
    {
        var collection = CreateTestCollection();
        var catalog = CreateMonthlyCatalog(new[] { 1, 2, 3, 4, 5 });

        var handler = new SmartTestHandler(collection, catalog);
        var http = new HttpClient(handler);
        var cache = new CacheService();
        var service = new GsmapService(http, cache, Microsoft.Extensions.Logging.Abstractions.NullLogger<GsmapService>.Instance);

        var frames = await service.GetFramesAsync("daily",
            new DateTime(2024, 3, 1), new DateTime(2024, 3, 5));

        for (int i = 1; i < frames.Count; i++)
        {
            Assert.True(frames[i].DateTime >= frames[i - 1].DateTime);
        }
    }

    [Fact]
    public async Task GetFramesAsync_Monthly_BuildsFromDatePattern()
    {
        var collection = CreateTestCollection();
        var handler = new TestHttpMessageHandler(JsonSerializer.Serialize(collection));
        var http = new HttpClient(handler);
        var cache = new CacheService();
        var service = new GsmapService(http, cache, Microsoft.Extensions.Logging.Abstractions.NullLogger<GsmapService>.Instance);

        var frames = await service.GetFramesAsync("monthly",
            new DateTime(2024, 1, 1), new DateTime(2024, 3, 1));

        Assert.Equal(3, frames.Count);
        Assert.All(frames, f => Assert.EndsWith("-PRECIP.tiff", f.CogUrl));
    }

    [Fact]
    public async Task GetLatestFrameAsync_ReturnsSingleFrame()
    {
        var collection = CreateTestCollection();
        // Create a catalog with recent days
        var recentDays = Enumerable.Range(1, 28).ToArray();
        var catalog = CreateMonthlyCatalog(recentDays);

        var handler = new SmartTestHandler(collection, catalog);
        var http = new HttpClient(handler);
        var cache = new CacheService();
        var service = new GsmapService(http, cache, Microsoft.Extensions.Logging.Abstractions.NullLogger<GsmapService>.Instance);

        var frame = await service.GetLatestFrameAsync("daily");
        Assert.NotNull(frame);
    }

    private class SmartTestHandler : HttpMessageHandler
    {
        private readonly string _collectionJson;
        private readonly string _catalogJson;

        public SmartTestHandler(StacCollection collection, StacCollection catalog)
        {
            _collectionJson = JsonSerializer.Serialize(collection);
            _catalogJson = JsonSerializer.Serialize(catalog);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";
            var content = url.Contains("collection.json") ? _collectionJson : _catalogJson;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseContent;

        public TestHttpMessageHandler(string responseContent)
        {
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
