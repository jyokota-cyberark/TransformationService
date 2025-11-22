using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransformationEngine.Integration.Configuration;
using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.Services;

/// <summary>
/// Implementation of rule cache manager using IMemoryCache
/// </summary>
public class RuleCacheManager : IRuleCacheManager
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<RuleCacheManager> _logger;
    private readonly TransformationConfiguration _config;
    private const string CacheKeyPrefix = "TransformationRules_";

    public RuleCacheManager(
        IMemoryCache cache,
        ILogger<RuleCacheManager> logger,
        IOptions<TransformationConfiguration> config)
    {
        _cache = cache;
        _logger = logger;
        _config = config.Value;
    }

    public Task<List<TransformationRule>?> GetCachedRulesAsync(string entityType, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(entityType);

        if (_cache.TryGetValue(cacheKey, out List<TransformationRule>? rules))
        {
            _logger.LogDebug("Cache hit for entity type: {EntityType}", entityType);
            return Task.FromResult(rules);
        }

        _logger.LogDebug("Cache miss for entity type: {EntityType}", entityType);
        return Task.FromResult<List<TransformationRule>?>(null);
    }

    public Task CacheRulesAsync(string entityType, List<TransformationRule> rules, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(entityType);

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_config.RuleCache.ExpirationMinutes),
            SlidingExpiration = TimeSpan.FromMinutes(_config.RuleCache.SlidingExpirationMinutes)
        };

        _cache.Set(cacheKey, rules, cacheOptions);

        _logger.LogInformation(
            "Cached {Count} rules for entity type: {EntityType}, Expiry: {Expiry} minutes",
            rules.Count,
            entityType,
            _config.RuleCache.ExpirationMinutes);

        return Task.CompletedTask;
    }

    public Task InvalidateCacheAsync(string entityType)
    {
        var cacheKey = GetCacheKey(entityType);
        _cache.Remove(cacheKey);

        _logger.LogInformation("Invalidated cache for entity type: {EntityType}", entityType);

        return Task.CompletedTask;
    }

    public Task InvalidateAllCachesAsync()
    {
        // Note: IMemoryCache doesn't have a built-in method to clear all entries
        // For production, consider using a more advanced caching solution like Redis
        // that supports clearing by pattern

        _logger.LogWarning("InvalidateAllCaches called - note that IMemoryCache doesn't support clearing all entries by pattern");

        return Task.CompletedTask;
    }

    public bool IsCacheValid(string entityType)
    {
        var cacheKey = GetCacheKey(entityType);
        return _cache.TryGetValue(cacheKey, out _);
    }

    private static string GetCacheKey(string entityType)
    {
        return $"{CacheKeyPrefix}{entityType}";
    }
}
