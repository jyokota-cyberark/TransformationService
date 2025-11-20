using System.Text.Json.Serialization;

namespace EntityContracts;

/// <summary>
/// Abstract base class for entities that implements common IEntity properties.
/// Entity-specific implementations should inherit from this class.
/// </summary>
public abstract class BaseEntity : IEntity
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Must be implemented by derived classes to specify the entity type.
    /// </summary>
    [JsonPropertyName("entityType")]
    public abstract string EntityType { get; }
}
