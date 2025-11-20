namespace EntityContracts;

/// <summary>
/// Base interface for all managed entities in the system.
/// Provides common properties and type identification for generic entity handling.
/// </summary>
public interface IEntity
{
    /// <summary>
    /// Unique identifier for the entity.
    /// </summary>
    Guid Id { get; set; }

    /// <summary>
    /// UTC timestamp when the entity was created.
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the entity was last updated.
    /// </summary>
    DateTime UpdatedAt { get; set; }

    /// <summary>
    /// The type name of the entity (e.g., "User", "WebApplication", "Role").
    /// Used by the Discovery Service to identify entity types without knowing specific implementation details.
    /// </summary>
    string EntityType { get; }
}
