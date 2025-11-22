namespace TransformationEngine.Integration.Configuration;

/// <summary>
/// Configuration for Kafka enrichment pipeline
/// </summary>
public class KafkaEnrichmentConfig
{
    /// <summary>
    /// Enable or disable Kafka enrichment (kill switch)
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Enrichment rules to apply (e.g., "validation", "enrichment", "normalization")
    /// </summary>
    public List<string> Rules { get; set; } = new();

    /// <summary>
    /// Raw topic to publish to (before enrichment)
    /// </summary>
    public string? RawTopic { get; set; }

    /// <summary>
    /// Enriched topic to consume from (after enrichment)
    /// </summary>
    public string? EnrichedTopic { get; set; }

    /// <summary>
    /// Timeout in seconds for enrichment processing
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
