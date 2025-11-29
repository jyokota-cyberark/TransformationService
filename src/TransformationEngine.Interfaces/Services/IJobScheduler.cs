namespace TransformationEngine.Interfaces.Services;

/// <summary>
/// Enumeration of supported scheduler backends
/// </summary>
public enum SchedulerType
{
    /// <summary>Hangfire - In-process background job processor</summary>
    Hangfire = 0,

    /// <summary>Airflow - Enterprise orchestration platform</summary>
    Airflow = 1,

    /// <summary>Quartz.NET - In-process job scheduler (future)</summary>
    Quartz = 2,

    /// <summary>AWS EventBridge - Serverless event scheduler (future)</summary>
    EventBridge = 3,

    /// <summary>Kubernetes CronJobs - Native Kubernetes scheduling (future)</summary>
    Kubernetes = 4
}

/// <summary>
/// Status of a scheduled job execution
/// </summary>
public enum JobExecutionStatus
{
    /// <summary>Job scheduled but not yet started</summary>
    Pending,

    /// <summary>Job currently running</summary>
    Running,

    /// <summary>Job completed successfully</summary>
    Success,

    /// <summary>Job completed with errors</summary>
    Failed,

    /// <summary>Job was cancelled</summary>
    Cancelled,

    /// <summary>Job status unknown (scheduler unreachable)</summary>
    Unknown
}

/// <summary>
/// Represents a scheduled job execution result
/// </summary>
public class ScheduledJobExecution
{
    /// <summary>Unique identifier for this execution</summary>
    public string ExecutionId { get; set; } = string.Empty;

    /// <summary>Unique identifier of the parent schedule</summary>
    public int ScheduleId { get; set; }

    /// <summary>Current execution status</summary>
    public JobExecutionStatus Status { get; set; }

    /// <summary>When the execution was triggered</summary>
    public DateTime TriggeredAt { get; set; }

    /// <summary>When the execution started (null if not yet started)</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>When the execution completed (null if still running)</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Error message if execution failed</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>External job ID from the scheduler (e.g., Spark job ID)</summary>
    public string? ExternalJobId { get; set; }

    /// <summary>Scheduler-specific reference ID (e.g., Airflow run_id, Hangfire job_id)</summary>
    public string? SchedulerJobId { get; set; }

    /// <summary>Execution metrics and statistics</summary>
    public Dictionary<string, object>? Metrics { get; set; }
}

/// <summary>
/// Represents a scheduler health check result
/// </summary>
public class SchedulerHealthStatus
{
    /// <summary>The scheduler type</summary>
    public SchedulerType SchedulerType { get; set; }

    /// <summary>Whether the scheduler is accessible and healthy</summary>
    public bool IsHealthy { get; set; }

    /// <summary>Health check timestamp</summary>
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Human-readable status message</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Additional diagnostics</summary>
    public Dictionary<string, object>? Diagnostics { get; set; }
}

/// <summary>
/// Core interface for pluggable job scheduler implementations
/// Supports multiple backends: Airflow, Hangfire, Quartz, etc.
/// </summary>
public interface IJobScheduler
{
    /// <summary>Get the scheduler type this implementation handles</summary>
    SchedulerType SchedulerType { get; }

    /// <summary>Check if scheduler is available and healthy</summary>
    Task<SchedulerHealthStatus> HealthCheckAsync();

    // ============== RECURRING JOB SCHEDULING ==============

    /// <summary>
    /// Create a recurring schedule that executes on a cron expression
    /// </summary>
    /// <param name="scheduleId">Database schedule ID</param>
    /// <param name="scheduleKey">Unique key for the schedule</param>
    /// <param name="cronExpression">Cron expression (CRON format)</param>
    /// <param name="timeZone">IANA timezone identifier</param>
    /// <param name="jobDefinitionId">Associated Spark job definition ID</param>
    /// <param name="jobParameters">Job parameters as JSON</param>
    /// <param name="sparkConfig">Spark configuration overrides as JSON</param>
    /// <returns>Scheduler-specific job ID</returns>
    Task<string> CreateRecurringScheduleAsync(
        int scheduleId,
        string scheduleKey,
        string cronExpression,
        string timeZone,
        int jobDefinitionId,
        string? jobParameters = null,
        string? sparkConfig = null);

    /// <summary>Update a recurring schedule's cron expression and timezone</summary>
    Task UpdateRecurringScheduleAsync(
        int scheduleId,
        string scheduleKey,
        string cronExpression,
        string? timeZone = null);

    /// <summary>Pause a recurring schedule (no new executions)</summary>
    Task PauseScheduleAsync(int scheduleId, string scheduleKey);

    /// <summary>Resume a paused recurring schedule</summary>
    Task ResumeScheduleAsync(int scheduleId, string scheduleKey);

    /// <summary>Delete a recurring schedule entirely</summary>
    Task DeleteScheduleAsync(int scheduleId, string scheduleKey);

    // ============== ONE-TIME/DELAYED JOB SCHEDULING ==============

    /// <summary>
    /// Schedule a job to execute once at a specific time
    /// </summary>
    Task<string> ScheduleOneTimeJobAsync(
        int scheduleId,
        string scheduleKey,
        DateTime scheduledAt,
        int jobDefinitionId,
        string? jobParameters = null,
        string? sparkConfig = null);

    /// <summary>
    /// Schedule a job to execute after a delay
    /// </summary>
    Task<string> ScheduleDelayedJobAsync(
        int scheduleId,
        string scheduleKey,
        TimeSpan delay,
        int jobDefinitionId,
        string? jobParameters = null,
        string? sparkConfig = null);

    // ============== IMMEDIATE EXECUTION ==============

    /// <summary>
    /// Execute a job immediately without scheduling
    /// </summary>
    Task<ScheduledJobExecution> ExecuteNowAsync(
        int jobDefinitionId,
        string? jobParameters = null,
        string? sparkConfig = null);

    // ============== SCHEDULE MANAGEMENT ==============

    /// <summary>Get a specific schedule by ID and key</summary>
    Task<ScheduledJobExecution?> GetScheduleAsync(int scheduleId, string scheduleKey);

    /// <summary>Get execution history for a schedule</summary>
    Task<IEnumerable<ScheduledJobExecution>> GetExecutionHistoryAsync(
        int scheduleId,
        int limit = 100,
        int offset = 0);

    /// <summary>Get the next scheduled execution time</summary>
    Task<DateTime?> GetNextExecutionTimeAsync(int scheduleId, string scheduleKey);

    // ============== JOB EXECUTION MONITORING ==============

    /// <summary>Get status of a specific execution</summary>
    Task<ScheduledJobExecution?> GetExecutionAsync(string executionId);

    /// <summary>Cancel a running execution</summary>
    Task<bool> CancelExecutionAsync(string executionId, string? reason = null);

    // ============== STATISTICS & MONITORING ==============

    /// <summary>Get execution statistics for a schedule</summary>
    Task<Dictionary<string, object>> GetScheduleStatisticsAsync(int scheduleId, string scheduleKey);

    /// <summary>Get all active schedules managed by this scheduler</summary>
    Task<IEnumerable<(int ScheduleId, string ScheduleKey, DateTime? NextExecution)>> ListActiveSchedulesAsync();
}

/// <summary>
/// Factory for creating scheduler instances based on configuration
/// </summary>
public interface ISchedulerFactory
{
    /// <summary>Create a scheduler instance for the specified type</summary>
    IJobScheduler CreateScheduler(SchedulerType schedulerType);

    /// <summary>Get the default/primary scheduler from configuration</summary>
    IJobScheduler GetDefaultScheduler();

    /// <summary>Get scheduler for specific schedule from configuration</summary>
    IJobScheduler GetSchedulerForSchedule(int scheduleId);
}

/// <summary>
/// Scheduler configuration for DI and runtime selection
/// </summary>
public interface ISchedulerConfiguration
{
    /// <summary>Primary scheduler type to use</summary>
    SchedulerType PrimaryScheduler { get; }

    /// <summary>Secondary scheduler for failover (null if no failover)</summary>
    SchedulerType? FailoverScheduler { get; }

    /// <summary>Whether to run schedules on both primary and failover</summary>
    bool DualScheduleMode { get; }

    /// <summary>Timeout for scheduler operations</summary>
    TimeSpan OperationTimeout { get; }

    /// <summary>Whether to use database for schedule persistence</summary>
    bool PersistToDatabase { get; }

    /// <summary>Get specific scheduler configuration by type</summary>
    Dictionary<string, object>? GetSchedulerConfig(SchedulerType schedulerType);
}
