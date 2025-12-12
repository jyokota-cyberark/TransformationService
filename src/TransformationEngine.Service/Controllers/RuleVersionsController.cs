using Microsoft.AspNetCore.Mvc;
using TransformationEngine.Core.Models.DTOs;
using TransformationEngine.Service.Services;

namespace TransformationEngine.Service.Controllers;

[ApiController]
[Route("api/transformation-rules/{ruleId}/versions")]
[Produces("application/json")]
public class RuleVersionsController : ControllerBase
{
    private readonly IRuleVersioningService _versioningService;
    private readonly ILogger<RuleVersionsController> _logger;

    public RuleVersionsController(
        IRuleVersioningService versioningService,
        ILogger<RuleVersionsController> logger)
    {
        _versioningService = versioningService;
        _logger = logger;
    }

    /// <summary>
    /// Get version history for a rule
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<RuleVersionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RuleVersionDto>>> GetRuleVersions(int ruleId)
    {
        var versions = await _versioningService.GetRuleVersionsAsync(ruleId);
        return Ok(versions);
    }

    /// <summary>
    /// Get a specific version of a rule
    /// </summary>
    [HttpGet("{version}")]
    [ProducesResponseType(typeof(RuleVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RuleVersionDto>> GetRuleVersion(int ruleId, int version)
    {
        var ruleVersion = await _versioningService.GetRuleVersionAsync(ruleId, version);
        if (ruleVersion == null)
        {
            return NotFound(new { error = $"Version {version} not found for rule {ruleId}" });
        }
        return Ok(ruleVersion);
    }

    /// <summary>
    /// Rollback a rule to a previous version
    /// </summary>
    [HttpPost("rollback/{targetVersion}")]
    [ProducesResponseType(typeof(RuleVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RuleVersionDto>> RollbackToVersion(
        int ruleId, 
        int targetVersion, 
        [FromBody] RollbackRuleRequest? request = null)
    {
        try
        {
            var version = await _versioningService.RollbackToVersionAsync(
                ruleId, 
                targetVersion, 
                request?.ChangedBy, 
                request?.Reason);
            return Ok(version);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back rule {RuleId} to version {TargetVersion}", 
                ruleId, targetVersion);
            return StatusCode(500, new { error = "An error occurred while rolling back the rule" });
        }
    }

    /// <summary>
    /// Compare two versions of a rule
    /// </summary>
    [HttpGet("diff/{version1}/{version2}")]
    [ProducesResponseType(typeof(RuleVersionDiffDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RuleVersionDiffDto>> CompareVersions(
        int ruleId, 
        int version1, 
        int version2)
    {
        try
        {
            var diff = await _versioningService.CompareVersionsAsync(ruleId, version1, version2);
            return Ok(diff);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing versions for rule {RuleId}", ruleId);
            return StatusCode(500, new { error = "An error occurred while comparing versions" });
        }
    }
}

