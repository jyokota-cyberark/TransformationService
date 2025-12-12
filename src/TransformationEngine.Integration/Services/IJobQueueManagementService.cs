namespace TransformationEngine.Integration.Services;

using TransformationEngine.Integration.Models;

/// <summary>
/// Service for managing transformation job queue and execution
/// Supports generic job types: Database Queue, Spark, Hangfire, Airflow
/// </summary>
public interface IJobQueueManagementService
{
    /// <summary>
    /// Get paginated job queue
    /// </summary>
    Task<PaginatedResult<TransformationJobQueue>> GetJobsAsync(
        JobStatus? status = null,
        string? entityType = null,
        int page = 1,
        int pageSize = 50);

    /// <summary>
    /// Get job by ID
    /// </summary>
    Task<TransformationJobQueue?> GetJobByIdAsync(int id);

    /// <summary>
    /// Get job count by status
    /// </summary>
    Task<int> GetJobCountAsync(JobStatus? status = null);

    /// <summary>
    /// Get job statistics
    /// </summary>
    Task<JobStatistics> GetStatisticsAsync();

    /// <summary>
    /// Cancel a pending job
    /// </summary>
    Task<bool> CancelJobAsync(int id);

    /// <summary>
    /// Trigger manual job processing
    /// </summary>
    Task<JobProcessingResult> ProcessJobsAsync(int batchSize = 10);

    /// <summary>
    /// Retry a failed job
    /// </summary>
    Task<bool> RetryJobAsync(int id);

    /// <summary>
    /// Clear completed/failed jobs older than specified days
    /// </summary>
    Task<int> CleanupOldJobsAsync(int olderThanDays = 30);

    /// <summary>
    /// Delete a job from the queue (only non-running jobs)
    /// </summary>
    Task<bool> DeleteJobAsync(int id);

    /// <summary>
    /// Delete all jobs from the queue (only non-running jobs)
    /// </summary>
    Task<int> DeleteAllJobsAsync();

    // ========== Orchestration Dashboard Methods ==========

    /// <summary>
    /// Get total jobs count
    /// </summary>
    Task<int> GetTotalJobsCountAsync();

    /// <summary>
    /// Get active jobs count (Pending + Processing)
    /// </summary>
    Task<int> GetActiveJobsCountAsync();

    /// <summary>
    /// Get completed jobs count within time range
    /// </summary>
    Task<int> GetCompletedJobsCountAsync(TimeSpan timeRange);

    /// <summary>
    /// Get failed jobs count within time range
    /// </summary>
    Task<int> GetFailedJobsCountAsync(TimeSpan timeRange);

    /// <summary>
    /// Get average duration in milliseconds within time range
    /// </summary>
    Task<double> GetAverageDurationAsync(TimeSpan timeRange);

    /// <summary>
    /// Get job counts grouped by orchestration engine
    /// </summary>
    Task<Dictionary<string, int>> GetJobCountsByEngineAsync();

    /// <summary>
    /// Get jobs filtered by orchestration engine
    /// </summary>
    Task<PaginatedResult<TransformationJobQueue>> GetJobsByEngineAsync(
        Models.OrchestrationEngine engine,
        int page = 1,
        int pageSize = 50);

    /// <summary>
    /// Get engine health status
    /// </summary>
    Task<EngineHealthStatus> GetEngineHealthAsync(Models.OrchestrationEngine engine);
}

/// <summary>
/// Job statistics
/// </summary>
public class JobStatistics
{
    public int TotalJobs { get; set; }
    public int PendingJobs { get; set; }
    public int ProcessingJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public int CancelledJobs { get; set; }
    public double AverageProcessingTimeMs { get; set; }
    public Dictionary<string, int> JobsByEntityType { get; set; } = new();
}

/// <summary>
/// Job processing result
/// </summary>
public class JobProcessingResult
{
    public int JobsProcessed { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
    public long TotalDurationMs { get; set; }
}

/// <summary>
/// Paginated result wrapper
/// </summary>
public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>
/// Engine health status
/// </summary>
public class EngineHealthStatus
{
    public string Engine { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public int TotalJobs { get; set; }
    public int ActiveJobs { get; set; }
    public int FailedJobs { get; set; }
    public double SuccessRate { get; set; }
    public int? AverageLatencyMs { get; set; }
    public string? StatusMessage { get; set; }
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
}
