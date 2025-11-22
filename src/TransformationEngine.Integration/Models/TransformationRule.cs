using System.Text.Json;

namespace TransformationEngine.Integration.Models;

/// <summary>
/// Transformation rule definition
/// </summary>
public class TransformationRule
{
    /// <summary>
    /// Rule ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Entity type this rule applies to
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Rule name
    /// </summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>
    /// Rule type (e.g., "Replace", "RegexReplace", "Format", "Lookup", "Custom")
    /// </summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// Rule configuration (JSON)
    /// </summary>
    public string Configuration { get; set; } = string.Empty;

    /// <summary>
    /// Priority (higher = executed earlier)
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// Whether rule is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this rule was cached from central service
    /// </summary>
    public bool CachedFromCentral { get; set; }

    /// <summary>
    /// Cache expiry timestamp
    /// </summary>
    public DateTime? CacheExpiry { get; set; }

    /// <summary>
    /// When rule was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When rule was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Get configuration as typed object
    /// </summary>
    public T? GetConfigurationAs<T>()
    {
        if (string.IsNullOrEmpty(Configuration))
            return default;

        return JsonSerializer.Deserialize<T>(Configuration);
    }

    /// <summary>
    /// Check if rule is expired
    /// </summary>
    public bool IsExpired()
    {
        return CacheExpiry.HasValue && CacheExpiry.Value < DateTime.UtcNow;
    }
}
