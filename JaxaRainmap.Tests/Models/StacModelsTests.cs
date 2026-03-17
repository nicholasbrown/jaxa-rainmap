using System.Text.Json;
using JaxaRainmap.Models;

namespace JaxaRainmap.Tests.Models;

public class StacModelsTests
{
    [Fact]
    public void StacCollection_Deserializes_FromJson()
    {
        var json = """
        {
            "id": "JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_daily",
            "title": "GSMaP Daily Gauge",
            "description": "Daily gauge-adjusted precipitation",
            "links": [
                { "rel": "self", "href": "https://example.com/collection.json" },
                { "rel": "item", "href": "https://example.com/item1.json" }
            ],
            "extent": {
                "spatial": { "bbox": [[-180, -60, 180, 60]] },
                "temporal": { "interval": [["2000-03-01T00:00:00Z", null]] }
            }
        }
        """;

        var collection = JsonSerializer.Deserialize<StacCollection>(json);

        Assert.NotNull(collection);
        Assert.Equal("JAXA.EORC_GSMaP_standard.Gauge.00Z-23Z.v6_daily", collection!.Id);
        Assert.Equal("GSMaP Daily Gauge", collection.Title);
        Assert.Equal(2, collection.Links.Count);
        Assert.Equal("item", collection.Links[1].Rel);
        Assert.NotNull(collection.Extent?.Spatial?.Bbox);
    }

    [Fact]
    public void StacItem_Deserializes_FromJson()
    {
        var json = """
        {
            "type": "Feature",
            "id": "20240101",
            "bbox": [-180, -60, 180, 60],
            "properties": {
                "datetime": "2024-01-01T00:00:00Z"
            },
            "links": [],
            "assets": {
                "data": {
                    "href": "https://example.com/20240101.tif",
                    "type": "image/tiff; application=geotiff",
                    "title": "Precipitation"
                }
            }
        }
        """;

        var item = JsonSerializer.Deserialize<StacItem>(json);

        Assert.NotNull(item);
        Assert.Equal("20240101", item!.Id);
        Assert.Equal("2024-01-01T00:00:00Z", item.Properties.DateTime);
        Assert.Single(item.Assets);
        Assert.Contains("20240101.tif", item.Assets["data"].Href);
    }

    [Fact]
    public void PrecipitationFrame_DisplayLabel_FormatsCorrectly()
    {
        var frame = new PrecipitationFrame
        {
            DateTime = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
            CogUrl = "https://example.com/test.tif"
        };

        Assert.Contains("2024", frame.DisplayLabel);
        Assert.Contains("06", frame.DisplayLabel);
        Assert.Contains("15", frame.DisplayLabel);
    }

    [Fact]
    public void RegionPreset_All_ContainsExpectedRegions()
    {
        Assert.Equal(7, RegionPreset.All.Length);
        Assert.Contains(RegionPreset.All, r => r.Name == "Global");
        Assert.Contains(RegionPreset.All, r => r.Name == "Japan");
        Assert.Contains(RegionPreset.All, r => r.Name == "Asia");
    }

    [Fact]
    public void GsmapCollections_GetCollectionUrl_ReturnsValidUrl()
    {
        var url = GsmapCollections.GetCollectionUrl(GsmapCollections.Daily);
        Assert.StartsWith("https://", url);
        Assert.Contains("je-pds/cog/v1", url);
        Assert.EndsWith("collection.json", url);
    }
}
