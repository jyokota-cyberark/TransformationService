using System.Text.Json;
using TransformationEngine.Integration.Configuration;

namespace TransformationEngine.Integration.Models;

/// <summary>
/// Result of a transformation operation
/// </summary>
public class TransformationResult
{
    /// <summary>
    /// Whether transformation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Entity type that was transformed
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Entity ID
    /// </summary>
    public int EntityId { get; set; }

    /// <summary>
    /// Original raw data (JSON)
    /// </summary>
    public string RawData { get; set; } = string.Empty;

    /// <summary>
    /// Transformed data (JSON)
    /// </summary>
    public string? TransformedData { get; set; }

    /// <summary>
    /// Generated fields from transformation (JSON)
    /// </summary>
    public string? GeneratedFields { get; set; }

    /// <summary>
    /// Mode used for transformation
    /// </summary>
    public TransformationMode ModeUsed { get; set; }

    /// <summary>
    /// Rules applied during transformation
    /// </summary>
    public List<string> RulesApplied { get; set; } = new();

    /// <summary>
    /// Duration of transformation in milliseconds
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Error message if transformation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Exception details if transformation failed
    /// </summary>
    public string? ExceptionDetails { get; set; }

    /// <summary>
    /// Timestamp when transformation occurred
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether fallback mode was used
    /// </summary>
    public bool UsedFallback { get; set; }

    /// <summary>
    /// Get transformed data as typed object
    /// </summary>
    public T? GetTransformedDataAs<T>()
    {
        if (string.IsNullOrEmpty(TransformedData))
            return default;

        return JsonSerializer.Deserialize<T>(TransformedData);
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

    /// <summary>
    /// Create a successful result
    /// </summary>
    public static TransformationResult CreateSuccess(
        string entityType,
        int entityId,
        string rawData,
        string transformedData,
        string? generatedFields,
        TransformationMode modeUsed,
        List<string> rulesApplied,
        long durationMs,
        bool usedFallback = false)
    {
        return new TransformationResult
        {
            Success = true,
            EntityType = entityType,
            EntityId = entityId,
            RawData = rawData,
            TransformedData = transformedData,
            GeneratedFields = generatedFields,
            ModeUsed = modeUsed,
            RulesApplied = rulesApplied,
            DurationMs = durationMs,
            UsedFallback = usedFallback
        };
    }

    /// <summary>
    /// Create a failure result
    /// </summary>
    public static TransformationResult CreateFailure(
        string entityType,
        int entityId,
        string rawData,
        TransformationMode attemptedMode,
        string errorMessage,
        string? exceptionDetails = null)
    {
        return new TransformationResult
        {
            Success = false,
            EntityType = entityType,
            EntityId = entityId,
            RawData = rawData,
            ModeUsed = attemptedMode,
            ErrorMessage = errorMessage,
            ExceptionDetails = exceptionDetails
        };
    }
}
