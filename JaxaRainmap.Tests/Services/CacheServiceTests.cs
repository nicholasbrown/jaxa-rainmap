using JaxaRainmap.Services;

namespace JaxaRainmap.Tests.Services;

public class CacheServiceTests
{
    [Fact]
    public void Get_ReturnsNull_WhenKeyNotFound()
    {
        var cache = new CacheService();
        var result = cache.Get<string>("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public void Set_And_Get_ReturnsValue()
    {
        var cache = new CacheService();
        cache.Set("key1", "value1", TimeSpan.FromMinutes(5));
        var result = cache.Get<string>("key1");
        Assert.Equal("value1", result);
    }

    [Fact]
    public void Get_ReturnsNull_WhenExpired()
    {
        var cache = new CacheService();
        cache.Set("key1", "value1", TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);
        var result = cache.Get<string>("key1");
        Assert.Null(result);
    }

    [Fact]
    public void Remove_DeletesEntry()
    {
        var cache = new CacheService();
        cache.Set("key1", "value1", TimeSpan.FromMinutes(5));
        cache.Remove("key1");
        Assert.Null(cache.Get<string>("key1"));
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var cache = new CacheService();
        cache.Set("a", "1", TimeSpan.FromMinutes(5));
        cache.Set("b", "2", TimeSpan.FromMinutes(5));
        cache.Clear();
        Assert.Null(cache.Get<string>("a"));
        Assert.Null(cache.Get<string>("b"));
    }

    [Fact]
    public void Set_OverwritesExistingKey()
    {
        var cache = new CacheService();
        cache.Set("key1", "old", TimeSpan.FromMinutes(5));
        cache.Set("key1", "new", TimeSpan.FromMinutes(5));
        Assert.Equal("new", cache.Get<string>("key1"));
    }
}
