using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Controllers;

/// <summary>
/// API controller for transformation job submission, status, and management
/// </summary>
[ApiController]
[Route("api/transformation-jobs")]
public class TransformationJobsController : ControllerBase
{
    private readonly ITransformationJobService _jobService;
    private readonly ILogger<TransformationJobsController> _logger;

    public TransformationJobsController(
        ITransformationJobService jobService,
        ILogger<TransformationJobsController> logger)
    {
        _jobService = jobService;
        _logger = logger;
    }

    /// <summary>
    /// Submits a new transformation job
    /// </summary>
    /// <param name="request">Job submission request</param>
    /// <returns>Job submission response with job ID</returns>
    [HttpPost("submit")]
    public async Task<ActionResult<TransformationJobResponse>> SubmitJob([FromBody] TransformationJobRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (string.IsNullOrEmpty(request.InputData))
        {
            return BadRequest("InputData is required");
        }

        try
        {
            var response = await _jobService.SubmitJobAsync(request);
            _logger.LogInformation("Job submitted: {JobId} with mode {Mode}", response.JobId, request.ExecutionMode);
            return Accepted(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting transformation job");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "Failed to submit job", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the status of a transformation job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>Current job status</returns>
    [HttpGet("{jobId}/status")]
    public async Task<ActionResult<TransformationJobStatus>> GetJobStatus(string jobId)
    {
        if (string.IsNullOrEmpty(jobId))
        {
            return BadRequest("JobId is required");
        }

        try
        {
            var status = await _jobService.GetJobStatusAsync(jobId);
            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Job not found: {JobId}", jobId);
            return NotFound(new { error = $"Job {jobId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job status for {JobId}", jobId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to get job status", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the results of a completed transformation job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>Job results if completed</returns>
    [HttpGet("{jobId}/result")]
    public async Task<ActionResult<Interfaces.Services.TransformationJobResult>> GetJobResult(string jobId)
    {
        if (string.IsNullOrEmpty(jobId))
        {
            return BadRequest("JobId is required");
        }

        try
        {
            var result = await _jobService.GetJobResultAsync(jobId);
            
            if (result == null)
            {
                // Check if job exists and is still processing
                var status = await _jobService.GetJobStatusAsync(jobId);
                if (status.Status == "Completed" || status.Status == "Failed")
                {
                    return NotFound(new { error = "Result not yet available" });
                }
                return Accepted(new { message = $"Job {jobId} is still processing", status = status.Status });
            }

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            _logger.LogWarning("Job not found: {JobId}", jobId);
            return NotFound(new { error = $"Job {jobId} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job result for {JobId}", jobId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to get job result", details = ex.Message });
        }
    }

    /// <summary>
    /// Cancels a running transformation job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>Success or failure</returns>
    [HttpPost("{jobId}/cancel")]
    public async Task<ActionResult<object>> CancelJob(string jobId)
    {
        if (string.IsNullOrEmpty(jobId))
        {
            return BadRequest("JobId is required");
        }

        try
        {
            var cancelled = await _jobService.CancelJobAsync(jobId);
            
            if (cancelled)
            {
                _logger.LogInformation("Job cancelled: {JobId}", jobId);
                return Ok(new { message = $"Job {jobId} has been cancelled" });
            }
            else
            {
                return NotFound(new { error = $"Job {jobId} could not be cancelled (may have already completed)" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling job {JobId}", jobId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to cancel job", details = ex.Message });
        }
    }

    /// <summary>
    /// Lists all transformation jobs with optional filtering
    /// </summary>
    /// <param name="status">Filter by job status</param>
    /// <param name="executionMode">Filter by execution mode</param>
    /// <param name="jobName">Filter by job name (contains)</param>
    /// <returns>List of jobs matching criteria</returns>
    [HttpGet("list")]
    public async Task<ActionResult<IEnumerable<TransformationJobStatus>>> ListJobs(
        [FromQuery] string? status = null,
        [FromQuery] string? executionMode = null,
        [FromQuery] string? jobName = null)
    {
        try
        {
            var filter = new TransformationJobFilter
            {
                Status = status,
                ExecutionMode = executionMode,
                JobNameContains = jobName,
                MaxResults = 100
            };

            var jobs = await _jobService.ListJobsAsync(filter);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing jobs");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to list jobs", details = ex.Message });
        }
    }
}
