using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core.Models;
using TransformationEngine.Data;
using TransformationEngine.Services;

namespace TransformationEngine.Services;

public class SparkJobSchedulerService : ISparkJobSchedulerService
{
    private readonly TransformationEngineDbContext _context;
    private readonly ISparkJobSubmissionService _submissionService;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IRecurringJobManager _recurringJobs;
    private readonly ILogger<SparkJobSchedulerService> _logger;

    public SparkJobSchedulerService(
        TransformationEngineDbContext context,
        ISparkJobSubmissionService submissionService,
        IBackgroundJobClient backgroundJobs,
        IRecurringJobManager recurringJobs,
        ILogger<SparkJobSchedulerService> logger)
    {
        _context = context;
        _submissionService = submissionService;
        _backgroundJobs = backgroundJobs;
        _recurringJobs = recurringJobs;
        _logger = logger;
    }

    public async Task<SparkJobSchedule> CreateRecurringScheduleAsync(SparkJobSchedule schedule)
    {
        _logger.LogInformation("Creating recurring schedule {ScheduleKey} for job {JobId}", 
            schedule.ScheduleKey, schedule.JobDefinitionId);

        schedule.ScheduleType = "Recurring";
        schedule.RecurringJobId = $"recurring-{schedule.ScheduleKey}";
        
        // Save to database first
        _context.SparkJobSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Register with Hangfire
        _recurringJobs.AddOrUpdate(
            schedule.RecurringJobId,
            () => ExecuteScheduledJob(schedule.Id),
            schedule.CronExpression!,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone ?? "UTC")
            });

        _logger.LogInformation("Recurring schedule created with ID {ScheduleId}", schedule.Id);
        return schedule;
    }

    public async Task UpdateRecurringScheduleAsync(int scheduleId, string cronExpression, string? timeZone = null)
    {
        var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
        if (schedule == null)
            throw new KeyNotFoundException($"Schedule not found: {scheduleId}");

        schedule.CronExpression = cronExpression;
        schedule.TimeZone = timeZone ?? schedule.TimeZone;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Update Hangfire job
        _recurringJobs.AddOrUpdate(
            schedule.RecurringJobId!,
            () => ExecuteScheduledJob(schedule.Id),
            cronExpression,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone ?? "UTC")
            });

        _logger.LogInformation("Updated recurring schedule {ScheduleId}", scheduleId);
    }

    public async Task PauseRecurringScheduleAsync(int scheduleId)
    {
        var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
        if (schedule == null)
            throw new KeyNotFoundException($"Schedule not found: {scheduleId}");

        schedule.IsPaused = true;
        schedule.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(schedule.RecurringJobId))
        {
            _recurringJobs.RemoveIfExists(schedule.RecurringJobId);
        }

        _logger.LogInformation("Paused recurring schedule {ScheduleId}", scheduleId);
    }

    public async Task ResumeRecurringScheduleAsync(int scheduleId)
    {
        var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
        if (schedule == null)
            throw new KeyNotFoundException($"Schedule not found: {scheduleId}");

        schedule.IsPaused = false;
        schedule.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Re-add to Hangfire
        _recurringJobs.AddOrUpdate(
            schedule.RecurringJobId!,
            () => ExecuteScheduledJob(schedule.Id),
            schedule.CronExpression!,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZone ?? "UTC")
            });

        _logger.LogInformation("Resumed recurring schedule {ScheduleId}", scheduleId);
    }

    public async Task DeleteRecurringScheduleAsync(int scheduleId)
    {
        var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
        if (schedule == null)
            throw new KeyNotFoundException($"Schedule not found: {scheduleId}");

        if (!string.IsNullOrEmpty(schedule.RecurringJobId))
        {
            _recurringJobs.RemoveIfExists(schedule.RecurringJobId);
        }

        _context.SparkJobSchedules.Remove(schedule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted recurring schedule {ScheduleId}", scheduleId);
    }

    public async Task<SparkJobSchedule> ScheduleOneTimeJobAsync(SparkJobSchedule schedule)
    {
        _logger.LogInformation("Scheduling one-time job {ScheduleKey} for {ScheduledAt}", 
            schedule.ScheduleKey, schedule.ScheduledAt);

        schedule.ScheduleType = "OneTime";
        
        _context.SparkJobSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        // Schedule with Hangfire
        var delay = schedule.ScheduledAt!.Value - DateTime.UtcNow;
        schedule.HangfireJobId = _backgroundJobs.Schedule(
            () => ExecuteScheduledJob(schedule.Id),
            delay);

        await _context.SaveChangesAsync();

        _logger.LogInformation("One-time job scheduled with ID {ScheduleId}", schedule.Id);
        return schedule;
    }

    public async Task<SparkJobSchedule> ScheduleDelayedJobAsync(int jobDefinitionId, int delayMinutes, Dictionary<string, object>? parameters = null)
    {
        var schedule = new SparkJobSchedule
        {
            ScheduleKey = $"delayed-{jobDefinitionId}-{Guid.NewGuid():N}",
            ScheduleName = $"Delayed Execution - Job {jobDefinitionId}",
            JobDefinitionId = jobDefinitionId,
            ScheduleType = "Delayed",
            DelayMinutes = delayMinutes,
            JobParameters = parameters
        };

        _context.SparkJobSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        schedule.HangfireJobId = _backgroundJobs.Schedule(
            () => ExecuteScheduledJob(schedule.Id),
            TimeSpan.FromMinutes(delayMinutes));

        await _context.SaveChangesAsync();

        _logger.LogInformation("Delayed job scheduled for {DelayMinutes} minutes", delayMinutes);
        return schedule;
    }

    public async Task<string> ExecuteJobNowAsync(int jobDefinitionId, Dictionary<string, object>? parameters = null)
    {
        var jobId = _backgroundJobs.Enqueue(() => ExecuteJobById(jobDefinitionId, parameters));
        _logger.LogInformation("Job {JobId} queued for immediate execution", jobDefinitionId);
        return jobId;
    }

    public async Task<SparkJobSchedule?> GetScheduleAsync(int scheduleId)
    {
        return await _context.SparkJobSchedules
            .Include(s => s.JobDefinition)
            .FirstOrDefaultAsync(s => s.Id == scheduleId);
    }

    public async Task<List<SparkJobSchedule>> GetAllSchedulesAsync()
    {
        return await _context.SparkJobSchedules
            .Include(s => s.JobDefinition)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SparkJobSchedule>> GetSchedulesByJobAsync(int jobDefinitionId)
    {
        return await _context.SparkJobSchedules
            .Include(s => s.JobDefinition)
            .Where(s => s.JobDefinitionId == jobDefinitionId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SparkJobSchedule>> GetActiveSchedulesAsync()
    {
        return await _context.SparkJobSchedules
            .Include(s => s.JobDefinition)
            .Where(s => s.IsActive && !s.IsPaused)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task UpdateScheduleStatisticsAsync(int scheduleId, bool success)
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

    public async Task<Dictionary<string, object>> GetScheduleStatisticsAsync(int scheduleId)
    {
        var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
        if (schedule == null)
            throw new KeyNotFoundException($"Schedule not found: {scheduleId}");

        return new Dictionary<string, object>
        {
            { "scheduleId", schedule.Id },
            { "scheduleKey", schedule.ScheduleKey },
            { "executionCount", schedule.ExecutionCount },
            { "successCount", schedule.SuccessCount },
            { "failureCount", schedule.FailureCount },
            { "successRate", schedule.ExecutionCount > 0 ? (double)schedule.SuccessCount / schedule.ExecutionCount : 0 },
            { "lastExecutionAt", (object?)schedule.LastExecutionAt ?? DateTime.MinValue },
            { "nextExecutionAt", (object?)schedule.NextExecutionAt ?? DateTime.MinValue },
            { "isActive", schedule.IsActive },
            { "isPaused", schedule.IsPaused }
        };
    }

    // Hangfire job execution methods

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteScheduledJob(int scheduleId)
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
            _logger.LogInformation("Schedule {ScheduleId} is inactive or paused, skipping execution", scheduleId);
            return;
        }

        _logger.LogInformation("Executing scheduled job for schedule {ScheduleId}, job {JobId}", 
            scheduleId, schedule.JobDefinitionId);

        try
        {
            if (schedule.JobDefinition == null)
            {
                _logger.LogError("Job definition not found for schedule {ScheduleId}", scheduleId);
                return;
            }

            // Build submission request from job definition and schedule parameters
            var request = new SparkJobSubmissionRequest
            {
                JobId = $"scheduled-{schedule.Id}-{Guid.NewGuid():N}",
                Language = schedule.JobDefinition.Language,
                PythonScript = schedule.JobDefinition.Language == "Python" ? schedule.JobDefinition.FilePath : null,
                DllPath = schedule.JobDefinition.Language == "CSharp" ? schedule.JobDefinition.FilePath : null,
                JarPath = schedule.JobDefinition.Language == "Scala" ? schedule.JobDefinition.FilePath : null
            };

            // Apply Spark config overrides from schedule if provided
            if (schedule.SparkConfig != null)
            {
                request.AdditionalOptions = schedule.SparkConfig;
            }

            var jobId = await _submissionService.SubmitJobAsync(request);
            await UpdateScheduleStatisticsAsync(scheduleId, true);

            _logger.LogInformation("Scheduled job {ScheduleId} submitted successfully with Spark job ID {JobId}",
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
    public async Task ExecuteJobById(int jobDefinitionId, Dictionary<string, object>? parameters)
    {
        _logger.LogInformation("Executing job {JobId}", jobDefinitionId);

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
                PythonScript = jobDefinition.Language == "Python" ? jobDefinition.FilePath : null,
                DllPath = jobDefinition.Language == "CSharp" ? jobDefinition.FilePath : null,
                JarPath = jobDefinition.Language == "Scala" ? jobDefinition.FilePath : null
            };

            var jobId = await _submissionService.SubmitJobAsync(request);
            _logger.LogInformation("Job {JobId} submitted successfully with Spark job ID {JobId}",
                jobDefinitionId, jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing job {JobId}", jobDefinitionId);
            throw;
        }
    }
}
