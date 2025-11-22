namespace TransformationEngine.Integration.Configuration;

/// <summary>
/// Configuration for a specific entity type (e.g., "User", "Application")
/// </summary>
public class EntityTypeConfig
{
    /// <summary>
    /// Entity type name (e.g., "User", "Application", "Device")
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Enable or disable transformation for this entity type
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Transformation mode to use for this entity type
    /// </summary>
    public TransformationMode Mode { get; set; } = TransformationMode.Sidecar;

    /// <summary>
    /// Rule storage mode for this entity type
    /// </summary>
    public RuleStorageMode RuleStorage { get; set; } = RuleStorageMode.Central;

    /// <summary>
    /// Kafka enrichment configuration for this entity type
    /// </summary>
    public KafkaEnrichmentConfig KafkaEnrichment { get; set; } = new();

    /// <summary>
    /// Priority for transformation execution (higher = earlier)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Allow re-transformation of generated fields
    /// </summary>
    public bool AllowGeneratedFieldRetransformation { get; set; } = true;

    /// <summary>
    /// Custom settings specific to this entity type
    /// </summary>
    public Dictionary<string, object> CustomSettings { get; set; } = new();
}
