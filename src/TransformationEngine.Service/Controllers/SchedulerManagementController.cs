using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core.Models;
using TransformationEngine.Data;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Controllers;

/// <summary>
/// API endpoints for managing job schedules across multiple schedulers
/// Supports Airflow, Hangfire, and other pluggable scheduling backends
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SchedulerManagementController : ControllerBase
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ISchedulerConfiguration _schedulerConfig;
    private readonly TransformationEngineDbContext _context;
    private readonly ILogger<SchedulerManagementController> _logger;

    public SchedulerManagementController(
        ISchedulerFactory schedulerFactory,
        ISchedulerConfiguration schedulerConfig,
        TransformationEngineDbContext context,
        ILogger<SchedulerManagementController> logger)
    {
        _schedulerFactory = schedulerFactory;
        _schedulerConfig = schedulerConfig;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get health status of all configured schedulers
    /// </summary>
    /// <returns>Health check results for primary and failover schedulers</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedulersHealthAsync()
    {
        _logger.LogInformation("Checking scheduler health status");
        
        var health = new Dictionary<string, object>();

        try
        {
            // Check primary scheduler
            var primaryScheduler = _schedulerFactory.GetDefaultScheduler();
            var primaryHealth = await primaryScheduler.HealthCheckAsync();
            health["primary"] = new
            {
                schedulerType = primaryHealth.SchedulerType.ToString(),
                isHealthy = primaryHealth.IsHealthy,
                checkedAt = primaryHealth.CheckedAt,
                message = primaryHealth.Message,
                diagnostics = primaryHealth.Diagnostics
            };

            // Check failover scheduler if configured
            if (_schedulerConfig.FailoverScheduler.HasValue)
            {
                try
                {
                    var failoverScheduler = _schedulerFactory.CreateScheduler(_schedulerConfig.FailoverScheduler.Value);
                    var failoverHealth = await failoverScheduler.HealthCheckAsync();
                    health["failover"] = new
                    {
                        schedulerType = failoverHealth.SchedulerType.ToString(),
                        isHealthy = failoverHealth.IsHealthy,
                        checkedAt = failoverHealth.CheckedAt,
                        message = failoverHealth.Message,
                        diagnostics = failoverHealth.Diagnostics
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check failover scheduler health");
                    health["failover"] = new
                    {
                        schedulerType = _schedulerConfig.FailoverScheduler.ToString(),
                        isHealthy = false,
                        message = $"Error: {ex.Message}"
                    };
                }
            }

            health["dualScheduleMode"] = _schedulerConfig.DualScheduleMode;
            health["timestamp"] = DateTime.UtcNow;

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking scheduler health");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get configuration for the active scheduler system
    /// </summary>
    /// <returns>Current scheduler configuration</returns>
    [HttpGet("config")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    public IActionResult GetSchedulerConfig()
    {
        var config = new Dictionary<string, object>
        {
            { "primaryScheduler", _schedulerConfig.PrimaryScheduler.ToString() },
            { "failoverScheduler", _schedulerConfig.FailoverScheduler?.ToString() ?? "None" },
            { "dualScheduleMode", _schedulerConfig.DualScheduleMode },
            { "operationTimeout", $"{_schedulerConfig.OperationTimeout.TotalSeconds}s" },
            { "persistToDatabase", _schedulerConfig.PersistToDatabase }
        };

        return Ok(config);
    }

    /// <summary>
    /// Execute a Spark job immediately without scheduling
    /// </summary>
    /// <param name="jobDefinitionId">The Spark job definition ID</param>
    /// <param name="jobParameters">Optional job parameters (JSON string)</param>
    /// <param name="sparkConfig">Optional Spark configuration overrides (JSON string)</param>
    /// <returns>Execution details including job ID and status</returns>
    [HttpPost("jobs/{jobDefinitionId}/execute-now")]
    [ProducesResponseType(typeof(ScheduledJobExecution), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExecuteJobNowAsync(
        int jobDefinitionId,
        [FromQuery] string? jobParameters = null,
        [FromQuery] string? sparkConfig = null)
    {
        try
        {
            _logger.LogInformation(
                "Executing job immediately: jobId={JobId}",
                jobDefinitionId);

            var scheduler = _schedulerFactory.GetDefaultScheduler();
            var execution = await scheduler.ExecuteNowAsync(
                jobDefinitionId, jobParameters, sparkConfig);

            return Accepted(execution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing job: {JobId}", jobDefinitionId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get list of all active schedules
    /// </summary>
    /// <returns>List of active schedules with next execution times</returns>
    [HttpGet("schedules/active")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveSchedulesAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving active schedules");

            var scheduler = _schedulerFactory.GetDefaultScheduler();
            var schedules = await scheduler.ListActiveSchedulesAsync();

            var result = schedules.Select(s => new
            {
                scheduleId = s.ScheduleId,
                scheduleKey = s.ScheduleKey,
                nextExecution = s.NextExecution
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active schedules");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get details of a specific schedule
    /// </summary>
    /// <param name="scheduleId">Schedule ID</param>
    /// <returns>Schedule details including status and statistics</returns>
    [HttpGet("schedules/{scheduleId}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScheduleDetailsAsync(int scheduleId)
    {
        try
        {
            _logger.LogInformation("Retrieving schedule details: scheduleId={ScheduleId}", scheduleId);

            var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
            if (schedule == null)
            {
                return NotFound(new { error = $"Schedule {scheduleId} not found" });
            }

            var scheduler = _schedulerFactory.GetDefaultScheduler();
            var execution = await scheduler.GetScheduleAsync(scheduleId, schedule.ScheduleKey);
            var stats = await scheduler.GetScheduleStatisticsAsync(scheduleId, schedule.ScheduleKey);
            var nextExecution = await scheduler.GetNextExecutionTimeAsync(scheduleId, schedule.ScheduleKey);

            var result = new
            {
                schedule.Id,
                schedule.ScheduleKey,
                schedule.ScheduleName,
                schedule.Description,
                schedule.ScheduleType,
                schedule.CronExpression,
                schedule.TimeZone,
                schedule.IsActive,
                schedule.IsPaused,
                execution = execution != null ? new
                {
                    status = execution.Status.ToString(),
                    triggeredAt = execution.TriggeredAt,
                    nextExecution = nextExecution
                } : null,
                statistics = stats,
                createdAt = schedule.CreatedAt,
                updatedAt = schedule.UpdatedAt
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedule details: {ScheduleId}", scheduleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get execution history for a schedule
    /// </summary>
    /// <param name="scheduleId">Schedule ID</param>
    /// <param name="limit">Maximum number of results (default: 100)</param>
    /// <param name="offset">Number of results to skip (default: 0)</param>
    /// <returns>List of recent executions</returns>
    [HttpGet("schedules/{scheduleId}/execution-history")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExecutionHistoryAsync(
        int scheduleId,
        [FromQuery] int limit = 100,
        [FromQuery] int offset = 0)
    {
        try
        {
            _logger.LogInformation(
                "Retrieving execution history: scheduleId={ScheduleId}, limit={Limit}, offset={Offset}",
                scheduleId, limit, offset);

            var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
            if (schedule == null)
            {
                return NotFound(new { error = $"Schedule {scheduleId} not found" });
            }

            var scheduler = _schedulerFactory.GetDefaultScheduler();
            var executions = await scheduler.GetExecutionHistoryAsync(scheduleId, limit, offset);

            var result = executions.Select(e => new
            {
                executionId = e.ExecutionId,
                status = e.Status.ToString(),
                triggeredAt = e.TriggeredAt,
                startedAt = e.StartedAt,
                completedAt = e.CompletedAt,
                errorMessage = e.ErrorMessage,
                externalJobId = e.ExternalJobId,
                schedulerJobId = e.SchedulerJobId,
                metrics = e.Metrics
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution history: {ScheduleId}", scheduleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get statistics for a schedule
    /// </summary>
    /// <param name="scheduleId">Schedule ID</param>
    /// <returns>Schedule statistics (execution count, success rate, etc.)</returns>
    [HttpGet("schedules/{scheduleId}/statistics")]
    [ProducesResponseType(typeof(Dictionary<string, object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetScheduleStatisticsAsync(int scheduleId)
    {
        try
        {
            _logger.LogInformation("Retrieving schedule statistics: scheduleId={ScheduleId}", scheduleId);

            var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
            if (schedule == null)
            {
                return NotFound(new { error = $"Schedule {scheduleId} not found" });
            }

            var scheduler = _schedulerFactory.GetDefaultScheduler();
            var stats = await scheduler.GetScheduleStatisticsAsync(scheduleId, schedule.ScheduleKey);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedule statistics: {ScheduleId}", scheduleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Pause a schedule (stop new executions)
    /// </summary>
    /// <param name="scheduleId">Schedule ID</param>
    /// <returns>Updated schedule details</returns>
    [HttpPost("schedules/{scheduleId}/pause")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> PauseScheduleAsync(int scheduleId)
    {
        try
        {
            _logger.LogInformation("Pausing schedule: scheduleId={ScheduleId}", scheduleId);

            var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
            if (schedule == null)
            {
                return NotFound(new { error = $"Schedule {scheduleId} not found" });
            }

            var scheduler = _schedulerFactory.GetDefaultScheduler();
            await scheduler.PauseScheduleAsync(scheduleId, schedule.ScheduleKey);

            return Ok(new { message = "Schedule paused successfully", scheduleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing schedule: {ScheduleId}", scheduleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Resume a paused schedule
    /// </summary>
    /// <param name="scheduleId">Schedule ID</param>
    /// <returns>Updated schedule details</returns>
    [HttpPost("schedules/{scheduleId}/resume")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResumeScheduleAsync(int scheduleId)
    {
        try
        {
            _logger.LogInformation("Resuming schedule: scheduleId={ScheduleId}", scheduleId);

            var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
            if (schedule == null)
            {
                return NotFound(new { error = $"Schedule {scheduleId} not found" });
            }

            var scheduler = _schedulerFactory.GetDefaultScheduler();
            await scheduler.ResumeScheduleAsync(scheduleId, schedule.ScheduleKey);

            return Ok(new { message = "Schedule resumed successfully", scheduleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming schedule: {ScheduleId}", scheduleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get the next scheduled execution time for a schedule
    /// </summary>
    /// <param name="scheduleId">Schedule ID</param>
    /// <returns>Next execution timestamp</returns>
    [HttpGet("schedules/{scheduleId}/next-execution")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNextExecutionTimeAsync(int scheduleId)
    {
        try
        {
            _logger.LogInformation("Getting next execution time: scheduleId={ScheduleId}", scheduleId);

            var schedule = await _context.SparkJobSchedules.FindAsync(scheduleId);
            if (schedule == null)
            {
                return NotFound(new { error = $"Schedule {scheduleId} not found" });
            }

            var scheduler = _schedulerFactory.GetDefaultScheduler();
            var nextExecution = await scheduler.GetNextExecutionTimeAsync(scheduleId, schedule.ScheduleKey);

            return Ok(new
            {
                scheduleId,
                scheduleKey = schedule.ScheduleKey,
                nextExecution = nextExecution,
                isScheduled = nextExecution.HasValue
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting next execution time: {ScheduleId}", scheduleId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }
}
