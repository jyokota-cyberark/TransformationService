namespace TransformationEngine.Integration.Services;

/// <summary>
/// Service for orchestrating transformation jobs across different execution engines
/// Supports: Local (Airflow, Hangfire, Spark) and Remote (TransformationService)
/// </summary>
public interface IJobOrchestrationService
{
    /// <summary>
    /// Submit a one-time transformation job
    /// </summary>
    Task<JobSubmissionResult> SubmitOneTimeJobAsync(JobSubmissionRequest request);

    /// <summary>
    /// Submit a scheduled transformation job (cron-based)
    /// </summary>
    Task<JobSubmissionResult> SubmitScheduledJobAsync(ScheduledJobRequest request);

    /// <summary>
    /// Get job execution status
    /// </summary>
    Task<JobExecutionStatus> GetJobStatusAsync(string jobId);

    /// <summary>
    /// Cancel a running job
    /// </summary>
    Task<bool> CancelJobAsync(string jobId);

    /// <summary>
    /// Get available orchestration engines
    /// </summary>
    Task<List<OrchestrationEngine>> GetAvailableEnginesAsync();
}

/// <summary>
/// Job submission request
/// </summary>
public class JobSubmissionRequest
{
    /// <summary>
    /// Entity type to transform
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Specific entity IDs (null = all entities)
    /// </summary>
    public List<int>? EntityIds { get; set; }

    /// <summary>
    /// Transformation rule IDs to apply (null = all active rules)
    /// </summary>
    public List<int>? RuleIds { get; set; }

    /// <summary>
    /// Preferred orchestration engine
    /// </summary>
    public OrchestrationEngineType PreferredEngine { get; set; } = OrchestrationEngineType.Auto;

    /// <summary>
    /// Job priority (1-10, higher = more priority)
    /// </summary>
    public int Priority { get; set; } = 5;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Scheduled job request
/// </summary>
public class ScheduledJobRequest : JobSubmissionRequest
{
    /// <summary>
    /// Cron expression for scheduling
    /// </summary>
    public string CronExpression { get; set; } = string.Empty;

    /// <summary>
    /// Schedule name/identifier
    /// </summary>
    public string ScheduleName { get; set; } = string.Empty;

    /// <summary>
    /// Whether schedule is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Job submission result
/// </summary>
public class JobSubmissionResult
{
    public bool Success { get; set; }
    public string JobId { get; set; } = string.Empty;
    public OrchestrationEngineType EngineUsed { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Job execution status
/// </summary>
public class JobExecutionStatus
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // Pending, Running, Completed, Failed
    public int Progress { get; set; } // 0-100
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int EntitiesProcessed { get; set; }
    public int EntitiesSucceeded { get; set; }
    public int EntitiesFailed { get; set; }
    public string? ErrorMessage { get; set; }
    public OrchestrationEngineType Engine { get; set; }
}

/// <summary>
/// Orchestration engine types
/// </summary>
public enum OrchestrationEngineType
{
    Auto,           // Automatically choose best available
    LocalQueue,     // Local database queue
    Hangfire,       // Hangfire background jobs
    Airflow,        // Apache Airflow
    Spark,          // Apache Spark
    Remote          // Remote TransformationService
}

/// <summary>
/// Orchestration engine info
/// </summary>
public class OrchestrationEngine
{
    public OrchestrationEngineType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool SupportsScheduling { get; set; }
    public bool SupportsBatchProcessing { get; set; }
    public string? EndpointUrl { get; set; }
}
