using TransformationEngine.Core.Models;

namespace TransformationEngine.Services;

/// <summary>
/// Service for scheduling Spark job executions using Hangfire
/// </summary>
public interface ISparkJobSchedulerService
{
    // Recurring Jobs
    Task<SparkJobSchedule> CreateRecurringScheduleAsync(SparkJobSchedule schedule);
    Task UpdateRecurringScheduleAsync(int scheduleId, string cronExpression, string? timeZone = null);
    Task PauseRecurringScheduleAsync(int scheduleId);
    Task ResumeRecurringScheduleAsync(int scheduleId);
    Task DeleteRecurringScheduleAsync(int scheduleId);

    // One-Time Jobs
    Task<SparkJobSchedule> ScheduleOneTimeJobAsync(SparkJobSchedule schedule);
    
    // Delayed Jobs
    Task<SparkJobSchedule> ScheduleDelayedJobAsync(int jobDefinitionId, int delayMinutes, Dictionary<string, object>? parameters = null);

    // Manual Execution
    Task<string> ExecuteJobNowAsync(int jobDefinitionId, Dictionary<string, object>? parameters = null);

    // Schedule Management
    Task<SparkJobSchedule?> GetScheduleAsync(int scheduleId);
    Task<List<SparkJobSchedule>> GetAllSchedulesAsync();
    Task<List<SparkJobSchedule>> GetSchedulesByJobAsync(int jobDefinitionId);
    Task<List<SparkJobSchedule>> GetActiveSchedulesAsync();

    // Statistics
    Task UpdateScheduleStatisticsAsync(int scheduleId, bool success);
    Task<Dictionary<string, object>> GetScheduleStatisticsAsync(int scheduleId);
}
