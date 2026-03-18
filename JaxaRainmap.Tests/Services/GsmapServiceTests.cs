using System.Net;
using System.Text.Json;
using JaxaRainmap.Models;
using JaxaRainmap.Services;

namespace JaxaRainmap.Tests.Services;

public class GsmapServiceTests
{
    private static StacCollection CreateTestCollection(List<StacLink>? itemLinks = null)
    {
        return new StacCollection
        {
            Id = "JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_daily",
            Title = "GSMaP Daily",
            Links = itemLinks ?? new List<StacLink>(),
            Extent = new StacExtent
            {
                Spatial = new StacSpatialExtent
                {
                    Bbox = new List<List<double>> { new() { -180, -60, 180, 60 } }
                }
            }
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
        var json = JsonSerializer.Serialize(collection);

        var handler = new TestHttpMessageHandler(json);
        var http = new HttpClient(handler);
        var cache = new CacheService();
        var service = new GsmapService(http, cache);

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
        var json = JsonSerializer.Serialize(collection);

        var handler = new TestHttpMessageHandler(json);
        var http = new HttpClient(handler);
        var cache = new CacheService();
        var service = new GsmapService(http, cache);

        var frames = await service.GetFramesAsync("daily",
            new DateTime(2024, 3, 1), new DateTime(2024, 3, 5));

        for (int i = 1; i < frames.Count; i++)
        {
            Assert.True(frames[i].DateTime >= frames[i - 1].DateTime);
        }
    }

    [Fact]
    public async Task GetLatestFrameAsync_ReturnsSingleFrame()
    {
        var collection = CreateTestCollection();
        var json = JsonSerializer.Serialize(collection);

        var handler = new TestHttpMessageHandler(json);
        var http = new HttpClient(handler);
        var cache = new CacheService();
        var service = new GsmapService(http, cache);

        var frame = await service.GetLatestFrameAsync("daily");
        Assert.NotNull(frame);
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
