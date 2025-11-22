namespace TransformationEngine.Integration.Models;

/// <summary>
/// Queued transformation job (when service is unhealthy)
/// </summary>
public class TransformationJobQueue
{
    /// <summary>
    /// Job ID
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
    /// Raw data to transform (JSON)
    /// </summary>
    public string RawData { get; set; } = string.Empty;

    /// <summary>
    /// Generated fields that can be re-transformed (JSON)
    /// </summary>
    public string? GeneratedFields { get; set; }

    /// <summary>
    /// Job status
    /// </summary>
    public JobStatus Status { get; set; } = JobStatus.Pending;

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Error message from last attempt
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When job was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When job was processed (if completed or failed)
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Next retry attempt time
    /// </summary>
    public DateTime? NextRetryAt { get; set; }
}

/// <summary>
/// Job status enumeration
/// </summary>
public enum JobStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}
