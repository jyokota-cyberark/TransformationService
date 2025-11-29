using TransformationEngine.Core.Models;

namespace TransformationEngine.Services;

/// <summary>
/// Service for managing Spark job definitions (CRUD operations)
/// </summary>
public interface ISparkJobLibraryService
{
    // Job Definition CRUD
    Task<SparkJobDefinition> CreateJobAsync(SparkJobDefinition jobDefinition);
    Task<SparkJobDefinition> UpdateJobAsync(string jobKey, SparkJobDefinition jobDefinition);
    Task<bool> DeleteJobAsync(string jobKey);
    Task<SparkJobDefinition?> GetJobAsync(string jobKey);
    Task<SparkJobDefinition?> GetJobByIdAsync(int id);
    Task<List<SparkJobDefinition>> GetAllJobsAsync();
    Task<List<SparkJobDefinition>> GetJobsByEntityTypeAsync(string entityType);
    Task<List<SparkJobDefinition>> GetJobsByCategoryAsync(string category);
    Task<List<SparkJobDefinition>> GetGenericJobsAsync();

    // Job Validation
    Task<JobValidationResult> ValidateJobAsync(string jobKey);
    Task<JobValidationResult> ValidateJobDefinitionAsync(SparkJobDefinition jobDefinition);

    // Job Artifacts
    Task<byte[]?> DownloadJobArtifactAsync(string jobKey);
    Task UploadJobArtifactAsync(string jobKey, Stream artifactStream, string fileName);

    // Job Lifecycle
    Task<SparkJobDefinition> ActivateJobAsync(string jobKey);
    Task<SparkJobDefinition> DeactivateJobAsync(string jobKey);
    Task<SparkJobDefinition> CloneJobAsync(string sourceJobKey, string newJobKey, string newJobName);

    // Search and Filtering
    Task<PagedResult<SparkJobDefinition>> SearchJobsAsync(JobSearchCriteria criteria);

    // Statistics
    Task<JobStatistics> GetJobStatisticsAsync(string jobKey);
}

public class JobValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class JobSearchCriteria
{
    public string? SearchTerm { get; set; }
    public string? Language { get; set; }
    public string? JobType { get; set; }
    public string? Category { get; set; }
    public string? EntityType { get; set; }
    public bool? IsGeneric { get; set; }
    public bool? IsActive { get; set; }
    public List<string>? Tags { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public class JobStatistics
{
    public string JobKey { get; set; } = string.Empty;
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public int ActiveSchedules { get; set; }
    public DateTime? LastExecutionTime { get; set; }
    public DateTime? NextScheduledExecution { get; set; }
    public double AverageDurationSeconds { get; set; }
    public long TotalRowsProcessed { get; set; }
}
