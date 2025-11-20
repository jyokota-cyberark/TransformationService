using Microsoft.Extensions.Logging;

namespace TransformationEngine.Core;

/// <summary>
/// Context passed through the transformation pipeline containing metadata and properties
/// </summary>
public class TransformationContext
{
    /// <summary>
    /// Custom properties that can be set and accessed during transformation
    /// </summary>
    public Dictionary<string, object> Properties { get; } = new();

    /// <summary>
    /// Logger instance for transformation operations
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Cancellation token for async operations
    /// </summary>
    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    /// <summary>
    /// Event type identifier (e.g., "UserChangeEvent", "WebApplicationChangeEvent")
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// Timestamp when transformation started
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Source service name (e.g., "UserManagementService", "ApplicationService")
    /// </summary>
    public string? SourceService { get; set; }

    /// <summary>
    /// Target topic or destination
    /// </summary>
    public string? TargetTopic { get; set; }

    /// <summary>
    /// Gets a property value by key
    /// </summary>
    public T? GetProperty<T>(string key)
    {
        if (Properties.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <summary>
    /// Sets a property value
    /// </summary>
    public void SetProperty<T>(string key, T value)
    {
        Properties[key] = value!;
    }
}

