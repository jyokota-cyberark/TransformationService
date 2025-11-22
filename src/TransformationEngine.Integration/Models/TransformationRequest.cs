using System.Text.Json;

namespace TransformationEngine.Integration.Models;

/// <summary>
/// Request for transforming entity data
/// </summary>
public class TransformationRequest
{
    /// <summary>
    /// Entity type (e.g., "User", "Application")
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Entity ID
    /// </summary>
    public int EntityId { get; set; }

    /// <summary>
    /// Raw data to transform (JSON)
    /// </summary>
    public string RawData { get; set; } = string.Empty;

    /// <summary>
    /// Previously generated fields that can be re-transformed
    /// </summary>
    public string? GeneratedFields { get; set; }

    /// <summary>
    /// Transformation mode to use (overrides configuration)
    /// </summary>
    public string? PreferredMode { get; set; }

    /// <summary>
    /// Specific rules to apply (null = apply all rules for entity type)
    /// </summary>
    public List<string>? RuleNames { get; set; }

    /// <summary>
    /// Additional context for transformation
    /// </summary>
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Get raw data as typed object
    /// </summary>
    public T? GetRawDataAs<T>()
    {
        return JsonSerializer.Deserialize<T>(RawData);
    }

    /// <summary>
    /// Get generated fields as typed object
    /// </summary>
    public T? GetGeneratedFieldsAs<T>()
    {
        if (string.IsNullOrEmpty(GeneratedFields))
            return default;

        return JsonSerializer.Deserialize<T>(GeneratedFields);
    }
}
