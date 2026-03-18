using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace JaxaRainmap.Services;

public class CacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ILogger<CacheService>? _logger;
    private const int MaxEntries = 500;

    public CacheService() { }

    public CacheService(ILogger<CacheService> logger)
    {
        _logger = logger;
    }

    public T? Get<T>(string key) where T : class
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                entry.LastAccessed = DateTime.UtcNow;
                return entry.Value as T;
            }

            _cache.TryRemove(key, out _);
        }

        return null;
    }

    public void Set<T>(string key, T value, TimeSpan ttl) where T : class
    {
        EvictIfNeeded();

        var entry = new CacheEntry
        {
            Value = value,
            ExpiresAt = DateTime.UtcNow.Add(ttl),
            LastAccessed = DateTime.UtcNow
        };

        _cache.AddOrUpdate(key, entry, (_, _) => entry);
    }

    public void Remove(string key)
    {
        _cache.TryRemove(key, out _);
    }

    public void Clear()
    {
        _cache.Clear();
    }

    private void EvictIfNeeded()
    {
        if (_cache.Count < MaxEntries) return;

        // Evict expired entries first
        var expired = _cache
            .Where(kv => kv.Value.ExpiresAt <= DateTime.UtcNow)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in expired)
        {
            _cache.TryRemove(key, out _);
        }

        // If still over limit, evict least recently accessed
        if (_cache.Count >= MaxEntries)
        {
            var lru = _cache
                .OrderBy(kv => kv.Value.LastAccessed)
                .Take(_cache.Count - MaxEntries + 50)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in lru)
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    private class CacheEntry
    {
        public object Value { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public DateTime LastAccessed { get; set; }
    }
}
