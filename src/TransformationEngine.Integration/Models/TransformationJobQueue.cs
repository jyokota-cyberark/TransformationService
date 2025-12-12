namespace TransformationEngine.Integration.Models;

/// <summary>
/// Queued transformation job (when service is unhealthy)
/// </summary>
public class TransformationJobQueue
{
    /// <summary>
    /// Database ID (auto-increment)
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Job ID (GUID string for tracking)
    /// </summary>
    public string JobId { get; set; } = string.Empty;

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

    /// <summary>
    /// Orchestration engine that executed/will execute this job
    /// </summary>
    public OrchestrationEngine Engine { get; set; } = OrchestrationEngine.LocalQueue;

    /// <summary>
    /// External job ID from the orchestration engine (e.g., Spark job ID, Airflow DAG run ID)
    /// </summary>
    public string? ExternalJobId { get; set; }

    /// <summary>
    /// Engine-specific metadata (JSON) - cluster info, progress, etc.
    /// </summary>
    public string? ExecutionMetadata { get; set; }

    /// <summary>
    /// Actual execution start time (different from CreatedAt which is queue time)
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Execution duration in milliseconds
    /// </summary>
    public int? DurationMs { get; set; }
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

/// <summary>
/// Orchestration engine type
/// </summary>
public enum OrchestrationEngine
{
    LocalQueue,
    Hangfire,
    Airflow,
    Spark,
    Remote
}
