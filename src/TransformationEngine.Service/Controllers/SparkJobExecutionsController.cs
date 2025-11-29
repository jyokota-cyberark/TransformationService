using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Data;
using TransformationEngine.Core.Models;

namespace TransformationEngine.Controllers;

/// <summary>
/// API controller for managing and tracking Spark job executions
/// </summary>
[ApiController]
[Route("api/spark-job-executions")]
public class SparkJobExecutionsController : ControllerBase
{
    private readonly TransformationEngineDbContext _context;
    private readonly ILogger<SparkJobExecutionsController> _logger;

    public SparkJobExecutionsController(
        TransformationEngineDbContext context,
        ILogger<SparkJobExecutionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all Spark job executions with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetExecutions(
        [FromQuery] string? status = null,
        [FromQuery] int? jobDefinitionId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var query = _context.SparkJobExecutions
                .Include(e => e.JobDefinition)
                .Include(e => e.Schedule)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(e => e.Status == status);
            }

            if (jobDefinitionId.HasValue)
            {
                query = query.Where(e => e.JobDefinitionId == jobDefinitionId.Value);
            }

            if (!string.IsNullOrEmpty(entityType))
            {
                query = query.Where(e => e.EntityType == entityType);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var executions = await query
                .OrderByDescending(e => e.QueuedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new
                {
                    e.Id,
                    e.ExecutionId,
                    JobDefinition = new
                    {
                        e.JobDefinition!.Id,
                        e.JobDefinition.JobKey,
                        e.JobDefinition.JobName
                    },
                    Schedule = e.Schedule != null ? new
                    {
                        e.Schedule.Id,
                        e.Schedule.ScheduleKey,
                        e.Schedule.ScheduleName
                    } : null,
                    e.TriggerType,
                    e.TriggeredBy,
                    e.SparkJobId,
                    e.Status,
                    e.Progress,
                    e.CurrentStage,
                    e.QueuedAt,
                    e.SubmittedAt,
                    e.StartedAt,
                    e.CompletedAt,
                    e.DurationSeconds,
                    e.RowsProcessed,
                    e.RecordsWritten,
                    e.ErrorMessage,
                    e.EntityType,
                    e.EntityId
                })
                .ToListAsync();

            return Ok(new
            {
                Items = executions,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Spark job executions");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific Spark job execution by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetExecution(int id)
    {
        try
        {
            var execution = await _context.SparkJobExecutions
                .Include(e => e.JobDefinition)
                .Include(e => e.Schedule)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (execution == null)
            {
                return NotFound(new { error = "Execution not found", id });
            }

            return Ok(new
            {
                execution.Id,
                execution.ExecutionId,
                JobDefinition = execution.JobDefinition != null ? new
                {
                    execution.JobDefinition.Id,
                    execution.JobDefinition.JobKey,
                    execution.JobDefinition.JobName,
                    execution.JobDefinition.JobType,
                    execution.JobDefinition.Language
                } : null,
                Schedule = execution.Schedule != null ? new
                {
                    execution.Schedule.Id,
                    execution.Schedule.ScheduleKey,
                    execution.Schedule.ScheduleName
                } : null,
                execution.TriggerType,
                execution.TriggeredBy,
                execution.SparkJobId,
                execution.SparkSubmissionCommand,
                ExecutorCores = execution.ExecutorCores,
                ExecutorMemoryMb = execution.ExecutorMemoryMb,
                NumExecutors = execution.NumExecutors,
                Arguments = execution.Arguments,
                execution.Status,
                execution.Progress,
                execution.CurrentStage,
                execution.QueuedAt,
                execution.SubmittedAt,
                execution.StartedAt,
                execution.CompletedAt,
                execution.DurationSeconds,
                execution.OutputPath,
                execution.RowsProcessed,
                execution.RecordsWritten,
                execution.BytesRead,
                execution.BytesWritten,
                ResultSummary = execution.ResultSummary,
                execution.ErrorMessage,
                execution.ErrorStackTrace,
                execution.RetryCount,
                execution.SparkDriverLog,
                execution.ApplicationLog,
                execution.LogPath,
                execution.EntityType,
                execution.EntityId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Spark job execution {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get execution by execution ID (unique identifier)
    /// </summary>
    [HttpGet("by-execution-id/{executionId}")]
    public async Task<IActionResult> GetExecutionByExecutionId(string executionId)
    {
        try
        {
            var execution = await _context.SparkJobExecutions
                .Include(e => e.JobDefinition)
                .Include(e => e.Schedule)
                .FirstOrDefaultAsync(e => e.ExecutionId == executionId);

            if (execution == null)
            {
                return NotFound(new { error = "Execution not found", executionId });
            }

            return Ok(execution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Spark job execution {ExecutionId}", executionId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a running Spark job execution
    /// </summary>
    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelExecution(int id)
    {
        try
        {
            var execution = await _context.SparkJobExecutions.FindAsync(id);
            if (execution == null)
            {
                return NotFound(new { error = "Execution not found", id });
            }

            if (execution.Status == "Succeeded" || execution.Status == "Failed" || execution.Status == "Cancelled")
            {
                return BadRequest(new { error = $"Cannot cancel execution with status: {execution.Status}" });
            }

            // Update status to Cancelled
            execution.Status = "Cancelled";
            execution.CompletedAt = DateTime.UtcNow;
            execution.ErrorMessage = "Cancelled by user";

            await _context.SaveChangesAsync();

            _logger.LogInformation("Spark job execution {Id} cancelled", id);

            return Ok(new { message = "Execution cancelled successfully", executionId = execution.ExecutionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling Spark job execution {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get execution statistics
    /// </summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] string? entityType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var query = _context.SparkJobExecutions.AsQueryable();

            if (!string.IsNullOrEmpty(entityType))
            {
                query = query.Where(e => e.EntityType == entityType);
            }

            if (startDate.HasValue)
            {
                query = query.Where(e => e.QueuedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(e => e.QueuedAt <= endDate.Value);
            }

            var total = await query.CountAsync();
            var byStatus = await query
                .GroupBy(e => e.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync();

            var successful = await query.Where(e => e.Status == "Succeeded").CountAsync();
            var failed = await query.Where(e => e.Status == "Failed").CountAsync();
            var running = await query.Where(e => e.Status == "Running" || e.Status == "Submitting").CountAsync();

            var avgDuration = await query
                .Where(e => e.DurationSeconds.HasValue)
                .Select(e => e.DurationSeconds!.Value)
                .DefaultIfEmpty(0)
                .AverageAsync();

            var totalRowsProcessed = await query
                .Where(e => e.RowsProcessed.HasValue)
                .SumAsync(e => e.RowsProcessed!.Value);

            return Ok(new
            {
                Total = total,
                Successful = successful,
                Failed = failed,
                Running = running,
                AverageDurationSeconds = avgDuration,
                TotalRowsProcessed = totalRowsProcessed,
                ByStatus = byStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution statistics");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

