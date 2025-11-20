using System.Text.Json.Serialization;

namespace EntityContracts;

/// <summary>
/// Generic implementation of IEntityChangeEvent.
/// Used for publishing entity changes to Kafka topics.
/// </summary>
public class EntityChangeEvent : IEntityChangeEvent
{
    [JsonPropertyName("entityId")]
    public Guid EntityId { get; set; }

    [JsonPropertyName("entityType")]
    public string EntityType { get; set; } = string.Empty;

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("entityData")]
    public string? EntityData { get; set; }

    /// <summary>
    /// Creates a change event for entity creation.
    /// </summary>
    public static EntityChangeEvent Created(IEntity entity, string serializedData, string schemaVersion)
    {
        return new EntityChangeEvent
        {
            EntityId = entity.Id,
            EntityType = entity.EntityType,
            Operation = EntityOperation.Created,
            Timestamp = DateTime.UtcNow,
            SchemaVersion = schemaVersion,
            EntityData = serializedData
        };
    }

    /// <summary>
    /// Creates a change event for entity update.
    /// </summary>
    public static EntityChangeEvent Updated(IEntity entity, string serializedData, string schemaVersion)
    {
        return new EntityChangeEvent
        {
            EntityId = entity.Id,
            EntityType = entity.EntityType,
            Operation = EntityOperation.Updated,
            Timestamp = DateTime.UtcNow,
            SchemaVersion = schemaVersion,
            EntityData = serializedData
        };
    }

    /// <summary>
    /// Creates a change event for entity deletion.
    /// </summary>
    public static EntityChangeEvent Deleted(Guid entityId, string entityType, string schemaVersion)
    {
        return new EntityChangeEvent
        {
            EntityId = entityId,
            EntityType = entityType,
            Operation = EntityOperation.Deleted,
            Timestamp = DateTime.UtcNow,
            SchemaVersion = schemaVersion,
            EntityData = null
        };
    }
}
