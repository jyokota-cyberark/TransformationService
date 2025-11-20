using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core;

namespace TransformationEngine.Transformers;

/// <summary>
/// Transformer that enriches data from external sources
/// </summary>
public class EnrichmentTransformer<T> : ITransformer<T, T>
{
    private readonly IEnrichmentSource _enrichmentSource;
    private readonly IMemoryCache? _cache;
    private readonly EnrichmentOptions _options;
    private readonly ILogger<EnrichmentTransformer<T>>? _logger;

    public int Order => 25; // After normalization, before validation
    public string Name => "Enrichment";

    public EnrichmentTransformer(
        IEnrichmentSource enrichmentSource,
        IMemoryCache? cache = null,
        EnrichmentOptions? options = null,
        ILogger<EnrichmentTransformer<T>>? logger = null)
    {
        _enrichmentSource = enrichmentSource ?? throw new ArgumentNullException(nameof(enrichmentSource));
        _cache = cache;
        _options = options ?? new EnrichmentOptions();
        _logger = logger;
    }

    public bool CanTransform(Type inputType, Type outputType)
    {
        return inputType == typeof(T) && outputType == typeof(T);
    }

    public async Task<T> TransformAsync(T input, TransformationContext context)
    {
        if (input == null)
            return input!;

        _logger?.LogDebug("Enriching data from source: {SourceType}", _enrichmentSource.GetType().Name);

        try
        {
            // Extract key for enrichment lookup
            var key = ExtractKey(input);
            if (string.IsNullOrEmpty(key))
            {
                _logger?.LogWarning("Could not extract key for enrichment, skipping");
                return input;
            }

            // Check cache first
            Dictionary<string, object>? enrichmentData = null;
            var cacheKey = $"enrichment_{_enrichmentSource.GetType().Name}_{key}";

            if (_cache != null && _cache.TryGetValue(cacheKey, out var cached) && cached is Dictionary<string, object> cachedDict)
            {
                enrichmentData = cachedDict;
                _logger?.LogDebug("Using cached enrichment data for key {Key}", key);
            }

            // Fetch from source if not cached
            if (enrichmentData == null)
            {
                var parameters = ExtractParameters(input);
                enrichmentData = await _enrichmentSource.GetEnrichmentDataAsync(key, parameters);

                // Cache the result
                if (_cache != null && enrichmentData != null && _options.CacheEnabled)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _options.CacheTtl
                    };
                    _cache.Set(cacheKey, enrichmentData, cacheOptions);
                    _logger?.LogDebug("Cached enrichment data for key {Key} with TTL {Ttl}", 
                        key, _options.CacheTtl);
                }
            }

            // Merge enrichment data into input
            if (enrichmentData != null && enrichmentData.Count > 0)
            {
                var enriched = MergeEnrichmentData(input, enrichmentData);
                _logger?.LogDebug("Successfully enriched data with {Count} fields", enrichmentData.Count);
                return enriched;
            }

            _logger?.LogDebug("No enrichment data found, returning original input");
            return input;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during enrichment transformation");
            
            if (_options.FailOnError)
            {
                throw new TransformationException("Enrichment transformation failed", ex);
            }
            
            _logger?.LogWarning("Enrichment failed but FailOnError is false, returning original input");
            return input;
        }
    }

    private string? ExtractKey(T input)
    {
        // Try to extract a key field (Id, UserId, EntityId, etc.)
        var json = JsonSerializer.Serialize(input);
        var jsonDoc = JsonDocument.Parse(json);

        var keyFields = new[] { "Id", "UserId", "EntityId", "id", "userId", "entityId" };
        
        foreach (var field in keyFields)
        {
            if (jsonDoc.RootElement.TryGetProperty(field, out var prop))
            {
                return prop.GetString() ?? prop.GetInt32().ToString();
            }
        }

        return null;
    }

    private Dictionary<string, object> ExtractParameters(T input)
    {
        var parameters = new Dictionary<string, object>();
        var json = JsonSerializer.Serialize(input);
        var jsonDoc = JsonDocument.Parse(json);

        foreach (var prop in jsonDoc.RootElement.EnumerateObject())
        {
            object value = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => prop.Value.GetInt32(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => prop.Value.GetRawText()
            };
            parameters[prop.Name] = value;
        }

        return parameters;
    }

    private T MergeEnrichmentData(T input, Dictionary<string, object> enrichmentData)
    {
        var json = JsonSerializer.Serialize(input);
        var jsonDoc = JsonDocument.Parse(json);
        var root = jsonDoc.RootElement;

        var mergedDict = new Dictionary<string, object?>();

        // Add original fields
        foreach (var property in root.EnumerateObject())
        {
            object? value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetInt32(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Object => property.Value.GetRawText(),
                JsonValueKind.Array => property.Value.GetRawText(),
                _ => property.Value.GetRawText()
            };
            mergedDict[property.Name] = value;
        }

        // Add enrichment fields (with prefix if configured)
        foreach (var kvp in enrichmentData)
        {
            var key = _options.EnrichmentPrefix != null 
                ? $"{_options.EnrichmentPrefix}{kvp.Key}" 
                : kvp.Key;
            mergedDict[key] = kvp.Value;
        }

        var jsonString = JsonSerializer.Serialize(mergedDict);
        var result = JsonSerializer.Deserialize<T>(jsonString);

        return result ?? input;
    }
}

/// <summary>
/// Interface for enrichment sources
/// </summary>
public interface IEnrichmentSource
{
    Task<Dictionary<string, object>> GetEnrichmentDataAsync(
        string key,
        Dictionary<string, object> parameters);
}

/// <summary>
/// Options for enrichment
/// </summary>
public class EnrichmentOptions
{
    public bool CacheEnabled { get; set; } = true;
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);
    public bool FailOnError { get; set; } = false;
    public string? EnrichmentPrefix { get; set; } = "Enriched";
}

