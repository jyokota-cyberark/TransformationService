namespace TransformationEngine.Integration.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Data;
using TransformationEngine.Integration.Models;

/// <summary>
/// Implementation of transformation job service
/// </summary>
public class JobQueueManagementService : IJobQueueManagementService
{
    private readonly TransformationIntegrationDbContext _context;
    private readonly IIntegratedTransformationService _transformationService;
    private readonly ILogger<JobQueueManagementService> _logger;

    public JobQueueManagementService(
        TransformationIntegrationDbContext context,
        IIntegratedTransformationService transformationService,
        ILogger<JobQueueManagementService> logger)
    {
        _context = context;
        _transformationService = transformationService;
        _logger = logger;
    }

    public async Task<PaginatedResult<TransformationJobQueue>> GetJobsAsync(
        JobStatus? status = null,
        string? entityType = null,
        int page = 1,
        int pageSize = 50)
    {
        var query = _context.TransformationJobQueue.AsQueryable();

        if (status.HasValue)
            query = query.Where(j => j.Status == status.Value);

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(j => j.EntityType == entityType);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResult<TransformationJobQueue>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<TransformationJobQueue?> GetJobByIdAsync(int id)
    {
        return await _context.TransformationJobQueue.FindAsync(id);
    }

    public async Task<int> GetJobCountAsync(JobStatus? status = null)
    {
        if (status.HasValue)
            return await _context.TransformationJobQueue.CountAsync(j => j.Status == status.Value);

        return await _context.TransformationJobQueue.CountAsync();
    }

    public async Task<JobStatistics> GetStatisticsAsync()
    {
        var jobs = await _context.TransformationJobQueue.ToListAsync();

        var completedJobs = jobs.Where(j => j.Status == JobStatus.Completed && j.ProcessedAt.HasValue).ToList();
        var avgProcessingTime = completedJobs.Any()
            ? completedJobs.Average(j => (j.ProcessedAt!.Value - j.CreatedAt).TotalMilliseconds)
            : 0;

        var jobsByType = jobs
            .GroupBy(j => j.EntityType)
            .ToDictionary(g => g.Key, g => g.Count());

        return new JobStatistics
        {
            TotalJobs = jobs.Count,
            PendingJobs = jobs.Count(j => j.Status == JobStatus.Pending),
            ProcessingJobs = jobs.Count(j => j.Status == JobStatus.Processing),
            CompletedJobs = jobs.Count(j => j.Status == JobStatus.Completed),
            FailedJobs = jobs.Count(j => j.Status == JobStatus.Failed),
            CancelledJobs = jobs.Count(j => j.Status == JobStatus.Cancelled),
            AverageProcessingTimeMs = avgProcessingTime,
            JobsByEntityType = jobsByType
        };
    }

    public async Task<bool> CancelJobAsync(int id)
    {
        var job = await _context.TransformationJobQueue.FindAsync(id);
        if (job == null || job.Status != JobStatus.Pending)
            return false;

        job.Status = JobStatus.Cancelled;
        job.ProcessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cancelled job {JobId}", id);
        return true;
    }

    public async Task<JobProcessingResult> ProcessJobsAsync(int batchSize = 10)
    {
        var result = new JobProcessingResult();
        var startTime = DateTime.UtcNow;

        var pendingJobs = await _context.TransformationJobQueue
            .Where(j => j.Status == JobStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .Take(batchSize)
            .ToListAsync();

        foreach (var job in pendingJobs)
        {
            try
            {
                job.Status = JobStatus.Processing;
                await _context.SaveChangesAsync();

                var request = new TransformationRequest
                {
                    EntityType = job.EntityType,
                    EntityId = job.EntityId,
                    RawData = job.RawData
                };

                var transformResult = await _transformationService.TransformAsync(request);

                if (transformResult.Success)
                {
                    job.Status = JobStatus.Completed;
                    job.GeneratedFields = transformResult.TransformedData;
                    result.Succeeded++;
                }
                else
                {
                    job.Status = JobStatus.Failed;
                    job.ErrorMessage = transformResult.ErrorMessage;
                    result.Failed++;
                    result.Errors.Add($"Job {job.Id}: {transformResult.ErrorMessage}");
                }

                job.ProcessedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                result.JobsProcessed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job {JobId}", job.Id);
                job.Status = JobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.ProcessedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                result.Failed++;
                result.Errors.Add($"Job {job.Id}: {ex.Message}");
            }
        }

        result.TotalDurationMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return result;
    }

    public async Task<bool> RetryJobAsync(int id)
    {
        var job = await _context.TransformationJobQueue.FindAsync(id);
        if (job == null || job.Status != JobStatus.Failed)
            return false;

        job.Status = JobStatus.Pending;
        job.RetryCount++;
        job.ErrorMessage = null;
        job.ProcessedAt = null;
        job.NextRetryAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Retrying job {JobId} (attempt {RetryCount})", id, job.RetryCount);
        return true;
    }

    public async Task<int> CleanupOldJobsAsync(int olderThanDays = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);

        var oldJobs = await _context.TransformationJobQueue
            .Where(j => (j.Status == JobStatus.Completed || j.Status == JobStatus.Cancelled)
                     && j.CreatedAt < cutoffDate)
            .ToListAsync();

        _context.TransformationJobQueue.RemoveRange(oldJobs);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} old jobs", oldJobs.Count);
        return oldJobs.Count;
    }

    public async Task<bool> DeleteJobAsync(int id)
    {
        var job = await _context.TransformationJobQueue.FindAsync(id);
        if (job == null)
            return false;

        // Cannot delete processing/running jobs
        if (job.Status == JobStatus.Processing)
            return false;

        _context.TransformationJobQueue.Remove(job);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Job {JobId} deleted from queue", id);
        return true;
    }

    public async Task<int> DeleteAllJobsAsync()
    {
        // Get all jobs except processing ones
        var jobsToDelete = await _context.TransformationJobQueue
            .Where(j => j.Status != JobStatus.Processing)
            .ToListAsync();

        if (jobsToDelete.Count == 0)
        {
            _logger.LogInformation("No jobs to delete");
            return 0;
        }

        _context.TransformationJobQueue.RemoveRange(jobsToDelete);
        await _context.SaveChangesAsync();

        _logger.LogWarning("Deleted {Count} jobs from queue (all non-processing jobs)", jobsToDelete.Count);
        return jobsToDelete.Count;
    }

    // ========== Orchestration Dashboard Methods ==========

    public async Task<int> GetTotalJobsCountAsync()
    {
        return await _context.TransformationJobQueue.CountAsync();
    }

    public async Task<int> GetActiveJobsCountAsync()
    {
        return await _context.TransformationJobQueue
            .CountAsync(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Processing);
    }

    public async Task<int> GetCompletedJobsCountAsync(TimeSpan timeRange)
    {
        var cutoffDate = DateTime.UtcNow - timeRange;
        return await _context.TransformationJobQueue
            .CountAsync(j => j.Status == JobStatus.Completed
                          && j.ProcessedAt.HasValue
                          && j.ProcessedAt.Value >= cutoffDate);
    }

    public async Task<int> GetFailedJobsCountAsync(TimeSpan timeRange)
    {
        var cutoffDate = DateTime.UtcNow - timeRange;
        return await _context.TransformationJobQueue
            .CountAsync(j => j.Status == JobStatus.Failed
                          && j.ProcessedAt.HasValue
                          && j.ProcessedAt.Value >= cutoffDate);
    }

    public async Task<double> GetAverageDurationAsync(TimeSpan timeRange)
    {
        var cutoffDate = DateTime.UtcNow - timeRange;
        var completedJobs = await _context.TransformationJobQueue
            .Where(j => j.Status == JobStatus.Completed
                     && j.DurationMs.HasValue
                     && j.ProcessedAt.HasValue
                     && j.ProcessedAt.Value >= cutoffDate)
            .ToListAsync();

        return completedJobs.Any() ? completedJobs.Average(j => j.DurationMs!.Value) : 0;
    }

    public async Task<Dictionary<string, int>> GetJobCountsByEngineAsync()
    {
        var jobs = await _context.TransformationJobQueue
            .GroupBy(j => j.Engine)
            .Select(g => new { Engine = g.Key, Count = g.Count() })
            .ToListAsync();

        return jobs.ToDictionary(
            j => j.Engine.ToString(),
            j => j.Count
        );
    }

    public async Task<PaginatedResult<TransformationJobQueue>> GetJobsByEngineAsync(
        Models.OrchestrationEngine engine,
        int page = 1,
        int pageSize = 50)
    {
        var query = _context.TransformationJobQueue
            .Where(j => j.Engine.Equals(engine));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResult<TransformationJobQueue>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<EngineHealthStatus> GetEngineHealthAsync(Models.OrchestrationEngine engine)
    {
        var engineName = engine.ToString();
        var jobs = await _context.TransformationJobQueue
            .Where(j => j.Engine.Equals(engine))
            .ToListAsync();

        var totalJobs = jobs.Count;
        var activeJobs = jobs.Count(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Processing);
        var failedJobs = jobs.Count(j => j.Status == JobStatus.Failed);
        var completedJobs = jobs.Count(j => j.Status == JobStatus.Completed);

        var successRate = totalJobs > 0
            ? (double)completedJobs / totalJobs * 100
            : 0;

        var recentJobs = jobs
            .Where(j => j.DurationMs.HasValue && j.ProcessedAt.HasValue)
            .OrderByDescending(j => j.ProcessedAt)
            .Take(100)
            .ToList();

        var avgLatency = recentJobs.Any()
            ? (int?)recentJobs.Average(j => j.DurationMs!.Value)
            : null;

        // Determine health status based on failure rate and active jobs
        var recentFailureRate = totalJobs > 0
            ? (double)failedJobs / totalJobs * 100
            : 0;

        var isHealthy = recentFailureRate < 20; // Less than 20% failure rate = healthy
        var statusMessage = isHealthy
            ? "Operating normally"
            : $"High failure rate: {recentFailureRate:F1}%";

        return new EngineHealthStatus
        {
            Engine = engineName,
            IsHealthy = isHealthy,
            TotalJobs = totalJobs,
            ActiveJobs = activeJobs,
            FailedJobs = failedJobs,
            SuccessRate = successRate,
            AverageLatencyMs = avgLatency,
            StatusMessage = statusMessage,
            LastChecked = DateTime.UtcNow
        };
    }
}
