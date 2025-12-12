namespace TransformationEngine.Integration.Configuration;

/// <summary>
/// Configuration options for Schema Registry integration
/// </summary>
public class SchemaRegistryOptions
{
    /// <summary>
    /// Schema registry provider (Confluent, InventoryService, etc.)
    /// </summary>
    public string Provider { get; set; } = "Confluent";

    /// <summary>
    /// Schema registry URL
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Validate data against schema before publishing to Kafka
    /// </summary>
    public bool ValidateBeforePublish { get; set; } = true;

    /// <summary>
    /// Enable schema caching
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Schema cache expiration in minutes
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Target schema mappings per entity type
    /// </summary>
    public Dictionary<string, TargetSchemaMapping> TargetSchemas { get; set; } = new();
}

/// <summary>
/// Target schema mapping for an entity type
/// </summary>
public class TargetSchemaMapping
{
    /// <summary>
    /// Kafka topic name
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Schema subject name in registry
    /// </summary>
    public string SchemaSubject { get; set; } = string.Empty;

    /// <summary>
    /// Schema version (optional, defaults to latest)
    /// </summary>
    public int? SchemaVersion { get; set; }
}

