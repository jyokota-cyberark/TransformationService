namespace TransformationEngine.Interfaces.Services;

/// <summary>
/// Service for submitting and tracking transformation jobs across different execution backends
/// (in-memory, Spark, etc.)
/// </summary>
public interface ITransformationJobService
{
    /// <summary>
    /// Submits a transformation job for execution
    /// </summary>
    /// <param name="request">Job submission request</param>
    /// <returns>Job submission response with job ID</returns>
    Task<TransformationJobResponse> SubmitJobAsync(TransformationJobRequest request);

    /// <summary>
    /// Gets the status of a submitted job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>Current job status</returns>
    Task<TransformationJobStatus> GetJobStatusAsync(string jobId);

    /// <summary>
    /// Gets the results of a completed job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>Job results if completed, null otherwise</returns>
    Task<TransformationJobResult?> GetJobResultAsync(string jobId);

    /// <summary>
    /// Cancels a running job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>True if cancellation was successful</returns>
    Task<bool> CancelJobAsync(string jobId);

    /// <summary>
    /// Lists all jobs with optional filtering
    /// </summary>
    /// <param name="filter">Optional filter criteria</param>
    /// <returns>List of jobs matching the filter</returns>
    Task<IEnumerable<TransformationJobStatus>> ListJobsAsync(TransformationJobFilter? filter = null);
}

/// <summary>
/// Request to submit a transformation job
/// </summary>
public class TransformationJobRequest
{
    /// <summary>
    /// Unique identifier for this job request
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// Type of execution backend (e.g., "InMemory", "Spark", "Kafka")
    /// </summary>
    public string ExecutionMode { get; set; } = "InMemory";

    /// <summary>
    /// Input data to transform (serialized)
    /// </summary>
    public string InputData { get; set; } = string.Empty;

    /// <summary>
    /// IDs of transformation rules to apply (in order)
    /// </summary>
    public int[] TransformationRuleIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Optional context data for transformations
    /// </summary>
    public Dictionary<string, object?>? Context { get; set; }

    /// <summary>
    /// Maximum execution time in seconds (0 = unlimited)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 0;

    /// <summary>
    /// Spark-specific configuration (only used when ExecutionMode = "Spark")
    /// </summary>
    public SparkJobConfiguration? SparkConfig { get; set; }
}

/// <summary>
/// Response from job submission
/// </summary>
public class TransformationJobResponse
{
    /// <summary>
    /// Unique job ID for tracking
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the job
    /// </summary>
    public string Status { get; set; } = "Submitted";

    /// <summary>
    /// When the job was submitted
    /// </summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional message with details
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Current status of a transformation job
/// </summary>
public class TransformationJobStatus
{
    /// <summary>
    /// Unique job identifier
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Job name
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// Execution mode
    /// </summary>
    public string ExecutionMode { get; set; } = string.Empty;

    /// <summary>
    /// Current status (Submitted, Running, Completed, Failed, Cancelled)
    /// </summary>
    public string Status { get; set; } = "Submitted";

    /// <summary>
    /// Percentage completion (0-100)
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// When job was submitted
    /// </summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>
    /// When job started executing
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When job completed or failed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Error message if job failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional status details
    /// </summary>
    public Dictionary<string, object?>? Metadata { get; set; }
}

/// <summary>
/// Results of a completed transformation job
/// </summary>
public class TransformationJobResult
{
    /// <summary>
    /// Job ID
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Success or failure
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Transformed output data (serialized)
    /// </summary>
    public string? OutputData { get; set; }

    /// <summary>
    /// Number of records processed
    /// </summary>
    public long RecordsProcessed { get; set; }

    /// <summary>
    /// Total execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Stack trace if error occurred
    /// </summary>
    public string? ErrorStackTrace { get; set; }
}

/// <summary>
/// Filter criteria for job listing
/// </summary>
public class TransformationJobFilter
{
    /// <summary>
    /// Filter by status
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Filter by execution mode
    /// </summary>
    public string? ExecutionMode { get; set; }

    /// <summary>
    /// Filter by job name (contains)
    /// </summary>
    public string? JobNameContains { get; set; }

    /// <summary>
    /// Filter jobs submitted after this date
    /// </summary>
    public DateTime? SubmittedAfter { get; set; }

    /// <summary>
    /// Filter jobs submitted before this date
    /// </summary>
    public DateTime? SubmittedBefore { get; set; }

    /// <summary>
    /// Maximum number of results
    /// </summary>
    public int MaxResults { get; set; } = 100;
}

/// <summary>
/// Spark-specific job configuration
/// </summary>
public class SparkJobConfiguration
{
    /// <summary>
    /// JAR file path (relative to /opt/spark-jobs)
    /// </summary>
    public string? JarPath { get; set; }

    /// <summary>
    /// Main class to execute
    /// </summary>
    public string? MainClass { get; set; }

    /// <summary>
    /// Number of executor cores
    /// </summary>
    public int ExecutorCores { get; set; } = 2;

    /// <summary>
    /// Executor memory in GB
    /// </summary>
    public int ExecutorMemoryGb { get; set; } = 2;

    /// <summary>
    /// Number of executors
    /// </summary>
    public int NumExecutors { get; set; } = 2;

    /// <summary>
    /// Additional spark-submit arguments
    /// </summary>
    public Dictionary<string, string>? AdditionalArgs { get; set; }
}
