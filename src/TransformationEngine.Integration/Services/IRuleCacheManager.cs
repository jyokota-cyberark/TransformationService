using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.Services;

/// <summary>
/// Manager for caching transformation rules
/// </summary>
public interface IRuleCacheManager
{
    /// <summary>
    /// Get cached rules for an entity type
    /// </summary>
    Task<List<TransformationRule>?> GetCachedRulesAsync(string entityType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache rules for an entity type
    /// </summary>
    Task CacheRulesAsync(string entityType, List<TransformationRule> rules, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate cache for an entity type
    /// </summary>
    Task InvalidateCacheAsync(string entityType);

    /// <summary>
    /// Invalidate all caches
    /// </summary>
    Task InvalidateAllCachesAsync();

    /// <summary>
    /// Check if cache exists and is valid for an entity type
    /// </summary>
    bool IsCacheValid(string entityType);
}
