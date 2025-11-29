using Microsoft.AspNetCore.Mvc;
using TransformationEngine.Core.Models;
using TransformationEngine.Services;

namespace TransformationEngine.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SparkJobSchedulerController : ControllerBase
{
    private readonly ISparkJobSchedulerService _schedulerService;
    private readonly ILogger<SparkJobSchedulerController> _logger;

    public SparkJobSchedulerController(
        ISparkJobSchedulerService schedulerService,
        ILogger<SparkJobSchedulerController> logger)
    {
        _schedulerService = schedulerService;
        _logger = logger;
    }

    [HttpPost("recurring")]
    public async Task<IActionResult> CreateRecurringSchedule([FromBody] CreateRecurringScheduleRequest request)
    {
        try
        {
            var schedule = new SparkJobSchedule
            {
                ScheduleKey = request.ScheduleKey,
                ScheduleName = request.ScheduleName,
                Description = request.Description,
                JobDefinitionId = request.JobDefinitionId,
                CronExpression = request.CronExpression,
                TimeZone = request.TimeZone ?? "UTC",
                JobParameters = request.JobParameters,
                SparkConfig = request.SparkConfig,
                CreatedBy = request.CreatedBy ?? "API"
            };

            var result = await _schedulerService.CreateRecurringScheduleAsync(schedule);
            return CreatedAtAction(nameof(GetSchedule), new { scheduleId = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create recurring schedule");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPut("recurring/{scheduleId}")]
    public async Task<IActionResult> UpdateRecurringSchedule(int scheduleId, [FromBody] UpdateRecurringScheduleRequest request)
    {
        try
        {
            await _schedulerService.UpdateRecurringScheduleAsync(scheduleId, request.CronExpression, request.TimeZone);
            return Ok(new { message = "Schedule updated successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update recurring schedule {ScheduleId}", scheduleId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("recurring/{scheduleId}/pause")]
    public async Task<IActionResult> PauseRecurringSchedule(int scheduleId)
    {
        try
        {
            await _schedulerService.PauseRecurringScheduleAsync(scheduleId);
            return Ok(new { message = "Schedule paused successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause schedule {ScheduleId}", scheduleId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("recurring/{scheduleId}/resume")]
    public async Task<IActionResult> ResumeRecurringSchedule(int scheduleId)
    {
        try
        {
            await _schedulerService.ResumeRecurringScheduleAsync(scheduleId);
            return Ok(new { message = "Schedule resumed successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume schedule {ScheduleId}", scheduleId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("recurring/{scheduleId}")]
    public async Task<IActionResult> DeleteRecurringSchedule(int scheduleId)
    {
        try
        {
            await _schedulerService.DeleteRecurringScheduleAsync(scheduleId);
            return Ok(new { message = "Schedule deleted successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule {ScheduleId}", scheduleId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("one-time")]
    public async Task<IActionResult> ScheduleOneTimeJob([FromBody] ScheduleOneTimeJobRequest request)
    {
        try
        {
            var schedule = new SparkJobSchedule
            {
                ScheduleKey = request.ScheduleKey,
                ScheduleName = request.ScheduleName,
                Description = request.Description,
                JobDefinitionId = request.JobDefinitionId,
                ScheduledAt = request.ScheduledAt,
                JobParameters = request.JobParameters,
                SparkConfig = request.SparkConfig,
                CreatedBy = request.CreatedBy ?? "API"
            };

            var result = await _schedulerService.ScheduleOneTimeJobAsync(schedule);
            return CreatedAtAction(nameof(GetSchedule), new { scheduleId = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule one-time job");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("delayed")]
    public async Task<IActionResult> ScheduleDelayedJob([FromBody] ScheduleDelayedJobRequest request)
    {
        try
        {
            var result = await _schedulerService.ScheduleDelayedJobAsync(
                request.JobDefinitionId,
                request.DelayMinutes,
                request.JobParameters);

            return CreatedAtAction(nameof(GetSchedule), new { scheduleId = result.Id }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule delayed job");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("execute-now/{jobDefinitionId}")]
    public async Task<IActionResult> ExecuteJobNow(int jobDefinitionId, [FromBody] Dictionary<string, object>? parameters = null)
    {
        try
        {
            var jobId = await _schedulerService.ExecuteJobNowAsync(jobDefinitionId, parameters);
            return Ok(new { hangfireJobId = jobId, message = "Job queued for immediate execution" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute job {JobId} immediately", jobDefinitionId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{scheduleId}")]
    public async Task<IActionResult> GetSchedule(int scheduleId)
    {
        try
        {
            var schedule = await _schedulerService.GetScheduleAsync(scheduleId);
            if (schedule == null)
                return NotFound(new { error = "Schedule not found" });

            return Ok(schedule);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schedule {ScheduleId}", scheduleId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAllSchedules()
    {
        try
        {
            var schedules = await _schedulerService.GetAllSchedulesAsync();
            return Ok(schedules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all schedules");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("job/{jobDefinitionId}")]
    public async Task<IActionResult> GetSchedulesByJob(int jobDefinitionId)
    {
        try
        {
            var schedules = await _schedulerService.GetSchedulesByJobAsync(jobDefinitionId);
            return Ok(schedules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schedules for job {JobId}", jobDefinitionId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveSchedules()
    {
        try
        {
            var schedules = await _schedulerService.GetActiveSchedulesAsync();
            return Ok(schedules);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active schedules");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{scheduleId}/statistics")]
    public async Task<IActionResult> GetScheduleStatistics(int scheduleId)
    {
        try
        {
            var stats = await _schedulerService.GetScheduleStatisticsAsync(scheduleId);
            return Ok(stats);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get statistics for schedule {ScheduleId}", scheduleId);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

// Request DTOs
public class CreateRecurringScheduleRequest
{
    public string ScheduleKey { get; set; } = string.Empty;
    public string ScheduleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int JobDefinitionId { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public string? TimeZone { get; set; }
    public Dictionary<string, object>? JobParameters { get; set; }
    public Dictionary<string, string>? SparkConfig { get; set; }
    public string? CreatedBy { get; set; }
}

public class UpdateRecurringScheduleRequest
{
    public string CronExpression { get; set; } = string.Empty;
    public string? TimeZone { get; set; }
}

public class ScheduleOneTimeJobRequest
{
    public string ScheduleKey { get; set; } = string.Empty;
    public string ScheduleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int JobDefinitionId { get; set; }
    public DateTime ScheduledAt { get; set; }
    public Dictionary<string, object>? JobParameters { get; set; }
    public Dictionary<string, string>? SparkConfig { get; set; }
    public string? CreatedBy { get; set; }
}

public class ScheduleDelayedJobRequest
{
    public int JobDefinitionId { get; set; }
    public int DelayMinutes { get; set; }
    public Dictionary<string, object>? JobParameters { get; set; }
}
