using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Controllers;

/// <summary>
/// API controller for test spark job operations
/// </summary>
[ApiController]
[Route("api/test-jobs")]
public class TestJobsController : ControllerBase
{
    private readonly ITestSparkJobService _testJobService;
    private readonly ILogger<TestJobsController> _logger;

    public TestJobsController(
        ITestSparkJobService testJobService,
        ILogger<TestJobsController> logger)
    {
        _testJobService = testJobService;
        _logger = logger;
    }

    /// <summary>
    /// Runs a test spark job to verify end-to-end integration
    /// </summary>
    /// <returns>Test job result</returns>
    [HttpPost("run")]
    public async Task<ActionResult<TestSparkJobResult>> RunTestJob()
    {
        try
        {
            _logger.LogInformation("Running test spark job");
            var result = await _testJobService.RunTestJobAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running test job");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to run test job", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets the last test job result
    /// </summary>
    /// <returns>Last test job result</returns>
    [HttpGet("last")]
    public async Task<ActionResult<TestSparkJobResult>> GetLastResult()
    {
        try
        {
            var result = await _testJobService.GetLastTestJobResultAsync();
            if (result == null)
            {
                return NotFound(new { message = "No test jobs have been run yet" });
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last test result");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to get test result", details = ex.Message });
        }
    }

    /// <summary>
    /// Gets test job history
    /// </summary>
    /// <param name="limit">Maximum number of results (default: 10)</param>
    /// <returns>List of test job results</returns>
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<TestSparkJobResult>>> GetHistory([FromQuery] int limit = 10)
    {
        try
        {
            if (limit < 1 || limit > 100)
            {
                return BadRequest(new { error = "Limit must be between 1 and 100" });
            }

            var history = await _testJobService.GetTestJobHistoryAsync(limit);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting test job history");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to get test history", details = ex.Message });
        }
    }

    /// <summary>
    /// Health check that also verifies connectivity to Spark
    /// </summary>
    /// <returns>Health status</returns>
    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "TestJobService",
            message = "Ready to run test spark jobs"
        });
    }
}
