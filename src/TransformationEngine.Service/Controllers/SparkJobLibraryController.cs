using Microsoft.AspNetCore.Mvc;
using TransformationEngine.Core.Models;
using TransformationEngine.Services;

namespace TransformationEngine.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SparkJobLibraryController : ControllerBase
{
    private readonly ISparkJobLibraryService _jobLibraryService;
    private readonly ILogger<SparkJobLibraryController> _logger;

    public SparkJobLibraryController(
        ISparkJobLibraryService jobLibraryService,
        ILogger<SparkJobLibraryController> logger)
    {
        _jobLibraryService = jobLibraryService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllJobs()
    {
        try
        {
            var jobs = await _jobLibraryService.GetAllJobsAsync();
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve jobs");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{jobKey}")]
    public async Task<IActionResult> GetJob(string jobKey)
    {
        try
        {
            var job = await _jobLibraryService.GetJobAsync(jobKey);
            if (job == null)
                return NotFound(new { error = "Job not found", jobKey });

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve job {JobKey}", jobKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateJob([FromBody] SparkJobDefinition jobDefinition)
    {
        try
        {
            var createdJob = await _jobLibraryService.CreateJobAsync(jobDefinition);
            return CreatedAtAction(nameof(GetJob), new { jobKey = createdJob.JobKey }, createdJob);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create job");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPut("{jobKey}")]
    public async Task<IActionResult> UpdateJob(string jobKey, [FromBody] SparkJobDefinition jobDefinition)
    {
        try
        {
            var updatedJob = await _jobLibraryService.UpdateJobAsync(jobKey, jobDefinition);
            return Ok(updatedJob);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update job {JobKey}", jobKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpDelete("{jobKey}")]
    public async Task<IActionResult> DeleteJob(string jobKey)
    {
        try
        {
            var deleted = await _jobLibraryService.DeleteJobAsync(jobKey);
            if (!deleted)
                return NotFound(new { error = "Job not found", jobKey });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete job {JobKey}", jobKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("search")]
    public async Task<IActionResult> SearchJobs([FromBody] JobSearchCriteria criteria)
    {
        try
        {
            var result = await _jobLibraryService.SearchJobsAsync(criteria);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search jobs");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{jobKey}/statistics")]
    public async Task<IActionResult> GetJobStatistics(string jobKey)
    {
        try
        {
            var stats = await _jobLibraryService.GetJobStatisticsAsync(jobKey);
            return Ok(stats);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job statistics for {JobKey}", jobKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{jobKey}/validate")]
    public async Task<IActionResult> ValidateJob(string jobKey)
    {
        try
        {
            var validationResult = await _jobLibraryService.ValidateJobAsync(jobKey);
            return Ok(validationResult);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate job {JobKey}", jobKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{jobKey}/activate")]
    public async Task<IActionResult> ActivateJob(string jobKey)
    {
        try
        {
            var job = await _jobLibraryService.ActivateJobAsync(jobKey);
            return Ok(job);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate job {JobKey}", jobKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{jobKey}/deactivate")]
    public async Task<IActionResult> DeactivateJob(string jobKey)
    {
        try
        {
            var job = await _jobLibraryService.DeactivateJobAsync(jobKey);
            return Ok(job);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate job {JobKey}", jobKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
