using TransformationEngine.Integration.Configuration;

namespace TransformationEngine.Integration.Models;

/// <summary>
/// Transformation history/audit record
/// </summary>
public class TransformationHistory
{
    /// <summary>
    /// History record ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Entity type
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Entity ID
    /// </summary>
    public int EntityId { get; set; }

    /// <summary>
    /// Transformation mode used
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// Rules applied (JSON array)
    /// </summary>
    public string? RulesApplied { get; set; }

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public long TransformationDuration { get; set; }

    /// <summary>
    /// Whether transformation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether fallback mode was used
    /// </summary>
    public bool UsedFallback { get; set; }

    /// <summary>
    /// Timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Create from transformation result
    /// </summary>
    public static TransformationHistory FromResult(TransformationResult result)
    {
        return new TransformationHistory
        {
            EntityType = result.EntityType,
            EntityId = result.EntityId,
            Mode = result.ModeUsed.ToString(),
            RulesApplied = System.Text.Json.JsonSerializer.Serialize(result.RulesApplied),
            TransformationDuration = result.DurationMs,
            Success = result.Success,
            ErrorMessage = result.ErrorMessage,
            UsedFallback = result.UsedFallback,
            CreatedAt = result.Timestamp
        };
    }
}
