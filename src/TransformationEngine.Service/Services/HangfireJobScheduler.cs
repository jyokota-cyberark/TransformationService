using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core.Models;
using TransformationEngine.Data;
using TransformationEngine.Interfaces.Services;
using TransformationEngine.Services;

namespace TransformationEngine.Services;

/// <summary>
/// Hangfire-based implementation of IJobScheduler
/// Uses Hangfire background job processing for scheduled and immediate job execution
/// </summary>
public class HangfireJobScheduler : IJobScheduler
{
    private readonly ILogger<HangfireJobScheduler> _logger;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IRecurringJobManager _recurringJobs;
    private readonly TransformationEngineDbContext _context;
    private readonly ISparkJobSubmissionService _submissionService;

    public SchedulerType SchedulerType => SchedulerType.Hangfire;

    public HangfireJobScheduler(
        ILogger<HangfireJobScheduler> logger,
        IBackgroundJobClient backgroundJobs,
        IRecurringJobManager recurringJobs,
        TransformationEngineDbContext context,
        ISparkJobSubmissionService submissionService)
    {
        _logger = logger;
        _backgroundJobs = backgroundJobs;
        _recurringJobs = recurringJobs;
        _context = context;
        _submissionService = submissionService;
    }

    public async Task<SchedulerHealthStatus> HealthCheckAsync()
    {
        try
        {
            _logger.LogDebug("Performing Hangfire health check");

            // Attempt to enqueue a test job to verify Hangfire is operational
            var testJobId = _backgroundJobs.Enqueue(() => TestJobAsync());

            var isHealthy = !string.IsNullOrEmpty(testJobId);
            var message = isHealthy ? "Hangfire is healthy" : "Failed to enqueue test job";

            return new SchedulerHealthStatus
            {
                SchedulerType = SchedulerType.Hangfire,
                IsHealthy = isHealthy,
                CheckedAt = DateTime.UtcNow,
                Message = message,
                Diagnostics = new Dictionary<string, object>
                {
                    { "TestJobId", testJobId ?? "unknown" }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hangfire health check failed");
            return new SchedulerHealthStatus
            {
                SchedulerType = SchedulerType.Hangfire,
                IsHealthy = false,
                CheckedAt = DateTime.UtcNow,
                Message = $"Health check failed: {ex.Message}",
                Diagnostics = new Dictionary<string, object>
                {
                    { "Exception", ex.GetType().Name },
                    { "Message", ex.Message }
                }
            };
        }
    }

    public async Task<string> CreateRecurringScheduleAsync(
        int scheduleId,
        string scheduleKey,
        string cronExpression,
        string timeZone,
        int jobDefinitionId,
        string? jobParameters = null,
        string? sparkConfig = null)
    {
        try
        {
            _logger.LogInformation(
                "Creating recurring Hangfire schedule: key={ScheduleKey}, cron={Cron}, tz={TimeZone}",
                scheduleKey, cronExpression, timeZone);

            var recurringJobId = $"recurring-{scheduleKey}";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone ?? "UTC");

            _recurringJobs.AddOrUpdate(
                recurringJobId,
                () => ExecuteScheduledJobAsync(scheduleId, jobDefinitionId, jobParameters, sparkConfig),
                cronExpression,
                new RecurringJobOptions { TimeZone = tz });

            _logger.LogInformation("Created Hangfire recurring job: {RecurringJobId}", recurringJobId);
            return recurringJobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create recurring schedule: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task UpdateRecurringScheduleAsync(
        int scheduleId,
        string scheduleKey,
        string cronExpression,
        string? timeZone = null)
    {
        try
        {
            _logger.LogInformation(
                "Updating recurring Hangfire schedule: key={ScheduleKey}, cron={Cron}",
                scheduleKey, cronExpression);

            var recurringJobId = $"recurring-{scheduleKey}";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZone ?? "UTC");

            // Hangfire doesn't have a direct update, so we remove and re-add
            _recurringJobs.RemoveIfExists(recurringJobId);

            var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
            if (schedule != null)
            {
                _recurringJobs.AddOrUpdate(
                    recurringJobId,
                    () => ExecuteScheduledJobAsync(
                        scheduleId, 
                        schedule.JobDefinitionId, 
                        schedule.JobParametersJson,
                        schedule.SparkConfigJson),
                    cronExpression,
                    new RecurringJobOptions { TimeZone = tz });
            }

            _logger.LogInformation("Updated Hangfire recurring job: {RecurringJobId}", recurringJobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update recurring schedule: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task PauseScheduleAsync(int scheduleId, string scheduleKey)
    {
        try
        {
            _logger.LogInformation("Pausing Hangfire schedule: key={ScheduleKey}", scheduleKey);

            var recurringJobId = $"recurring-{scheduleKey}";
            _recurringJobs.RemoveIfExists(recurringJobId);

            var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
            if (schedule != null)
            {
                schedule.IsPaused = true;
                schedule.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Paused Hangfire schedule: {ScheduleKey}", scheduleKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause schedule: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task ResumeScheduleAsync(int scheduleId, string scheduleKey)
    {
        try
        {
            _logger.LogInformation("Resuming Hangfire schedule: key={ScheduleKey}", scheduleKey);

            var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
            if (schedule == null) return;

            schedule.IsPaused = false;
            schedule.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Re-add to Hangfire
            var recurringJobId = $"recurring-{scheduleKey}";
            var tz = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone ?? "UTC");

            _recurringJobs.AddOrUpdate(
                recurringJobId,
                () => ExecuteScheduledJobAsync(
                    scheduleId,
                    schedule.JobDefinitionId,
                    schedule.JobParametersJson,
                    schedule.SparkConfigJson),
                schedule.CronExpression!,
                new RecurringJobOptions { TimeZone = tz });

            _logger.LogInformation("Resumed Hangfire schedule: {ScheduleKey}", scheduleKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume schedule: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task DeleteScheduleAsync(int scheduleId, string scheduleKey)
    {
        try
        {
            _logger.LogInformation("Deleting Hangfire schedule: key={ScheduleKey}", scheduleKey);

            var recurringJobId = $"recurring-{scheduleKey}";
            _recurringJobs.RemoveIfExists(recurringJobId);

            _logger.LogInformation("Deleted Hangfire schedule: {ScheduleKey}", scheduleKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task<string> ScheduleOneTimeJobAsync(
        int scheduleId,
        string scheduleKey,
        DateTime scheduledAt,
        int jobDefinitionId,
        string? jobParameters = null,
        string? sparkConfig = null)
    {
        try
        {
            _logger.LogInformation(
                "Scheduling one-time Hangfire job: key={ScheduleKey}, scheduledAt={ScheduledAt}",
                scheduleKey, scheduledAt);

            var delay = scheduledAt - DateTime.UtcNow;
            var jobId = _backgroundJobs.Schedule(
                () => ExecuteScheduledJobAsync(scheduleId, jobDefinitionId, jobParameters, sparkConfig),
                delay);

            _logger.LogInformation("Scheduled one-time Hangfire job: {JobId}", jobId);
            return jobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule one-time job: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task<string> ScheduleDelayedJobAsync(
        int scheduleId,
        string scheduleKey,
        TimeSpan delay,
        int jobDefinitionId,
        string? jobParameters = null,
        string? sparkConfig = null)
    {
        try
        {
            _logger.LogInformation(
                "Scheduling delayed Hangfire job: key={ScheduleKey}, delay={Delay}",
                scheduleKey, delay);

            var jobId = _backgroundJobs.Schedule(
                () => ExecuteScheduledJobAsync(scheduleId, jobDefinitionId, jobParameters, sparkConfig),
                delay);

            _logger.LogInformation("Scheduled delayed Hangfire job: {JobId}", jobId);
            return jobId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule delayed job: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task<ScheduledJobExecution> ExecuteNowAsync(
        int jobDefinitionId,
        string? jobParameters = null,
        string? sparkConfig = null)
    {
        try
        {
            _logger.LogInformation("Executing Hangfire job immediately: jobId={JobId}", jobDefinitionId);

            var executionId = Guid.NewGuid().ToString("N");
            var jobId = _backgroundJobs.Enqueue(
                () => ExecuteJobByDefinitionAsync(jobDefinitionId, jobParameters, sparkConfig));

            _logger.LogInformation("Enqueued immediate Hangfire execution: {JobId}", jobId);

            return new ScheduledJobExecution
            {
                ExecutionId = executionId,
                ScheduleId = jobDefinitionId,
                Status = JobExecutionStatus.Pending,
                TriggeredAt = DateTime.UtcNow,
                SchedulerJobId = jobId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute job immediately: jobId={JobId}", jobDefinitionId);
            throw;
        }
    }

    public async Task<ScheduledJobExecution?> GetScheduleAsync(int scheduleId, string scheduleKey)
    {
        try
        {
            var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
            if (schedule == null) return null;

            return new ScheduledJobExecution
            {
                ExecutionId = scheduleId.ToString(),
                ScheduleId = scheduleId,
                Status = schedule.IsPaused ? JobExecutionStatus.Pending : JobExecutionStatus.Running,
                TriggeredAt = DateTime.UtcNow,
                SchedulerJobId = $"recurring-{scheduleKey}",
                Metrics = new Dictionary<string, object>
                {
                    { "executionCount", schedule.ExecutionCount },
                    { "successCount", schedule.SuccessCount },
                    { "failureCount", schedule.FailureCount },
                    { "lastExecutionAt", schedule.LastExecutionAt ?? DateTime.MinValue }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schedule: {ScheduleKey}", scheduleKey);
            return null;
        }
    }

    public async Task<IEnumerable<ScheduledJobExecution>> GetExecutionHistoryAsync(
        int scheduleId,
        int limit = 100,
        int offset = 0)
    {
        try
        {
            var executions = new List<ScheduledJobExecution>();
            
            // Hangfire doesn't provide direct execution history access via API
            // This would typically be retrieved from application logs or monitoring system
            // For now, return empty list as a placeholder
            _logger.LogDebug(
                "Execution history retrieval for Hangfire would require custom logging/monitoring");

            return executions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get execution history: {ScheduleId}", scheduleId);
            return new List<ScheduledJobExecution>();
        }
    }

    public async Task<DateTime?> GetNextExecutionTimeAsync(int scheduleId, string scheduleKey)
    {
        try
        {
            var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
            if (schedule == null) return null;

            return schedule.NextExecutionAt;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get next execution time: {ScheduleKey}", scheduleKey);
            return null;
        }
    }

    public async Task<ScheduledJobExecution?> GetExecutionAsync(string executionId)
    {
        try
        {
            // Hangfire doesn't provide direct access to execution history
            // This would be implemented with custom monitoring
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get execution: {ExecutionId}", executionId);
            return null;
        }
    }

    public async Task<bool> CancelExecutionAsync(string executionId, string? reason = null)
    {
        try
        {
            _logger.LogInformation("Cancelling Hangfire execution: {ExecutionId}", executionId);
            
            // Hangfire doesn't support canceling queued jobs directly
            // Would need custom implementation with state tracking
            _logger.LogWarning("Hangfire doesn't support job cancellation via scheduler");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling execution: {ExecutionId}", executionId);
            return false;
        }
    }

    public async Task<Dictionary<string, object>> GetScheduleStatisticsAsync(int scheduleId, string scheduleKey)
    {
        try
        {
            var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
            if (schedule == null)
            {
                return new Dictionary<string, object>
                {
                    { "scheduleId", scheduleId },
                    { "scheduleKey", scheduleKey },
                    { "error", "Schedule not found" }
                };
            }

            return new Dictionary<string, object>
            {
                { "scheduleId", scheduleId },
                { "scheduleKey", scheduleKey },
                { "executionCount", schedule.ExecutionCount },
                { "successCount", schedule.SuccessCount },
                { "failureCount", schedule.FailureCount },
                { "successRate", schedule.ExecutionCount > 0 
                    ? (double)schedule.SuccessCount / schedule.ExecutionCount 
                    : 0.0 },
                { "lastExecutionAt", schedule.LastExecutionAt ?? DateTime.MinValue },
                { "nextExecutionAt", schedule.NextExecutionAt ?? DateTime.MinValue },
                { "isActive", schedule.IsActive },
                { "isPaused", schedule.IsPaused }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schedule statistics: {ScheduleKey}", scheduleKey);
            return new Dictionary<string, object>
            {
                { "scheduleId", scheduleId },
                { "scheduleKey", scheduleKey },
                { "error", ex.Message }
            };
        }
    }

    public async Task<IEnumerable<(int ScheduleId, string ScheduleKey, DateTime? NextExecution)>> ListActiveSchedulesAsync()
    {
        try
        {
            var activeSchedules = await _context.SparkJobSchedules
                .Where(s => s.IsActive && !s.IsPaused)
                .Select(s => new { s.Id, s.ScheduleKey, s.NextExecutionAt })
                .ToListAsync();

            return activeSchedules.Select(s => (s.Id, s.ScheduleKey, s.NextExecutionAt));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list active schedules");
            return new List<(int, string, DateTime?)>();
        }
    }

    // ============== HANGFIRE JOB EXECUTION METHODS ==============

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteScheduledJobAsync(
        int scheduleId,
        int jobDefinitionId,
        string? jobParameters,
        string? sparkConfig)
    {
        var schedule = await _context.SparkJobSchedules
            .Include(s => s.JobDefinition)
            .FirstOrDefaultAsync(s => s.Id == scheduleId);

        if (schedule == null)
        {
            _logger.LogError("Schedule {ScheduleId} not found", scheduleId);
            return;
        }

        if (!schedule.IsActive || schedule.IsPaused)
        {
            _logger.LogInformation(
                "Schedule {ScheduleId} is inactive or paused, skipping execution", scheduleId);
            return;
        }

        _logger.LogInformation(
            "Executing scheduled job for schedule {ScheduleId}, job {JobId}",
            scheduleId, schedule.JobDefinitionId);

        try
        {
            if (schedule.JobDefinition == null)
            {
                _logger.LogError("Job definition not found for schedule {ScheduleId}", scheduleId);
                await UpdateScheduleStatisticsAsync(scheduleId, false);
                return;
            }

            // Build submission request from job definition and schedule parameters
            var request = new SparkJobSubmissionRequest
            {
                JobId = $"scheduled-{schedule.Id}-{Guid.NewGuid():N}",
                Language = schedule.JobDefinition.Language,
                PythonScript = schedule.JobDefinition.Language == "Python" 
                    ? schedule.JobDefinition.FilePath 
                    : null,
                DllPath = schedule.JobDefinition.Language == "CSharp" 
                    ? schedule.JobDefinition.FilePath 
                    : null,
                JarPath = schedule.JobDefinition.Language == "Scala" 
                    ? schedule.JobDefinition.FilePath 
                    : null
            };

            var jobId = await _submissionService.SubmitJobAsync(request);
            await UpdateScheduleStatisticsAsync(scheduleId, true);

            _logger.LogInformation(
                "Scheduled job {ScheduleId} submitted successfully with Spark job ID {JobId}",
                scheduleId, jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scheduled job {ScheduleId}", scheduleId);
            await UpdateScheduleStatisticsAsync(scheduleId, false);
            throw;
        }
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteJobByDefinitionAsync(
        int jobDefinitionId,
        string? jobParameters,
        string? sparkConfig)
    {
        _logger.LogInformation("Executing job {JobId} immediately", jobDefinitionId);

        try
        {
            var jobDefinition = await _context.SparkJobDefinitions.FindAsync(jobDefinitionId);
            if (jobDefinition == null)
            {
                _logger.LogError("Job definition {JobId} not found", jobDefinitionId);
                return;
            }

            var request = new SparkJobSubmissionRequest
            {
                JobId = $"manual-{jobDefinitionId}-{Guid.NewGuid():N}",
                Language = jobDefinition.Language,
                PythonScript = jobDefinition.Language == "Python" 
                    ? jobDefinition.FilePath 
                    : null,
                DllPath = jobDefinition.Language == "CSharp" 
                    ? jobDefinition.FilePath 
                    : null,
                JarPath = jobDefinition.Language == "Scala" 
                    ? jobDefinition.FilePath 
                    : null
            };

            var jobId = await _submissionService.SubmitJobAsync(request);
            _logger.LogInformation(
                "Job {JobId} submitted successfully with Spark job ID {JobId}",
                jobDefinitionId, jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing job {JobId}", jobDefinitionId);
            throw;
        }
    }

    public async Task TestJobAsync()
    {
        _logger.LogDebug("Executing Hangfire test job");
        await Task.Delay(100);
    }

    private async Task UpdateScheduleStatisticsAsync(int scheduleId, bool success)
    {
        var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
        if (schedule == null) return;

        schedule.ExecutionCount++;
        schedule.LastExecutionAt = DateTime.UtcNow;

        if (success)
            schedule.SuccessCount++;
        else
            schedule.FailureCount++;

        await _context.SaveChangesAsync();
    }
}
