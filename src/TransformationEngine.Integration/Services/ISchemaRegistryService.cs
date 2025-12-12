namespace TransformationEngine.Integration.Services;

/// <summary>
/// Service for interacting with schema registry (Confluent or custom)
/// </summary>
public interface ISchemaRegistryService
{
    /// <summary>
    /// Get schema for an entity type from the registry
    /// </summary>
    /// <param name="entityType">Entity type (e.g., "user", "application")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Schema JSON string</returns>
    Task<string> GetSchemaAsync(string entityType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a new schema in the registry
    /// </summary>
    /// <param name="subject">Schema subject (e.g., "user-changes-value")</param>
    /// <param name="schemaJson">Avro schema JSON</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Schema ID</returns>
    Task<int> RegisterSchemaAsync(string subject, string schemaJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate data against a schema
    /// </summary>
    /// <param name="entityType">Entity type</param>
    /// <param name="dataJson">Data to validate (JSON)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> ValidateDataAgainstSchemaAsync(string entityType, string dataJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if schema registry is available
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if available</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all schema subjects
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of subjects</returns>
    Task<List<string>> GetSubjectsAsync(CancellationToken cancellationToken = default);
}

