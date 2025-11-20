namespace EntityContracts;

/// <summary>
/// Base interface for entity change events published to Kafka.
/// Provides a standardized event structure for all entity types.
/// </summary>
public interface IEntityChangeEvent
{
    /// <summary>
    /// The unique identifier of the entity that changed.
    /// </summary>
    Guid EntityId { get; set; }

    /// <summary>
    /// The type name of the entity (e.g., "User", "WebApplication", "Role").
    /// </summary>
    string EntityType { get; set; }

    /// <summary>
    /// The operation performed: "Created", "Updated", or "Deleted".
    /// </summary>
    string Operation { get; set; }

    /// <summary>
    /// UTC timestamp when the change occurred.
    /// </summary>
    DateTime Timestamp { get; set; }

    /// <summary>
    /// Schema version for the entity data payload.
    /// Format: "EntityType-v{version}" (e.g., "User-v1", "WebApplication-v1").
    /// Used by consumers to retrieve the correct schema from the schema registry.
    /// </summary>
    string SchemaVersion { get; set; }

    /// <summary>
    /// Serialized entity data (JSON or Avro).
    /// Contains the full entity state after the change.
    /// Null for delete operations.
    /// </summary>
    string? EntityData { get; set; }
}
