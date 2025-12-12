using Microsoft.AspNetCore.Mvc;
using TransformationEngine.Core.Models.DTOs;
using TransformationEngine.Service.Services;

namespace TransformationEngine.Service.Controllers;

[ApiController]
[Route("api/transformation-projects")]
[Produces("application/json")]
public class TransformationProjectsController : ControllerBase
{
    private readonly ITransformationProjectService _projectService;
    private readonly ILogger<TransformationProjectsController> _logger;

    public TransformationProjectsController(
        ITransformationProjectService projectService,
        ILogger<TransformationProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new transformation project
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TransformationProjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransformationProjectDto>> CreateProject([FromBody] CreateProjectRequest request)
    {
        try
        {
            var project = await _projectService.CreateProjectAsync(request);
            return CreatedAtAction(nameof(GetProjectById), new { id = project.Id }, project);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project: {Name}", request.Name);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get all transformation projects
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TransformationProjectDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TransformationProjectDto>>> GetAllProjects([FromQuery] string? entityType = null)
    {
        var projects = await _projectService.GetAllProjectsAsync(entityType);
        return Ok(projects);
    }

    /// <summary>
    /// Get a specific project by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TransformationProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransformationProjectDto>> GetProjectById(int id)
    {
        var project = await _projectService.GetProjectByIdAsync(id);
        if (project == null)
        {
            return NotFound(new { error = $"Project with ID {id} not found" });
        }
        return Ok(project);
    }

    /// <summary>
    /// Update a project
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TransformationProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TransformationProjectDto>> UpdateProject(int id, [FromBody] UpdateProjectRequest request)
    {
        try
        {
            var project = await _projectService.UpdateProjectAsync(id, request);
            return Ok(project);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project: {Id}", id);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a project
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var deleted = await _projectService.DeleteProjectAsync(id);
        if (!deleted)
        {
            return NotFound(new { error = $"Project with ID {id} not found" });
        }
        return NoContent();
    }

    /// <summary>
    /// Add a rule to a project
    /// </summary>
    [HttpPost("{id}/rules")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddRuleToProject(int id, [FromBody] AddRuleRequest request)
    {
        try
        {
            var added = await _projectService.AddRuleToProjectAsync(id, request.RuleId, request.ExecutionOrder);
            if (!added)
            {
                return BadRequest(new { error = "Rule already exists in project" });
            }
            return Ok(new { message = "Rule added successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove a rule from a project
    /// </summary>
    [HttpDelete("{id}/rules/{ruleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveRuleFromProject(int id, int ruleId)
    {
        var removed = await _projectService.RemoveRuleFromProjectAsync(id, ruleId);
        if (!removed)
        {
            return NotFound(new { error = "Rule not found in project" });
        }
        return NoContent();
    }

    /// <summary>
    /// Reorder rules in a project
    /// </summary>
    [HttpPut("{id}/rules/order")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ReorderRules(int id, [FromBody] Dictionary<int, int> ruleOrders)
    {
        await _projectService.ReorderProjectRulesAsync(id, ruleOrders);
        return Ok(new { message = "Rules reordered successfully" });
    }

    /// <summary>
    /// Execute a transformation project
    /// </summary>
    [HttpPost("{id}/execute")]
    [ProducesResponseType(typeof(ProjectExecutionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectExecutionDto>> ExecuteProject(int id, [FromBody] ExecuteProjectRequest request)
    {
        try
        {
            var execution = await _projectService.ExecuteProjectAsync(id, request);
            return Ok(execution);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing project: {Id}", id);
            return StatusCode(500, new { error = "An error occurred while executing the project" });
        }
    }

    /// <summary>
    /// Get execution history for a project
    /// </summary>
    [HttpGet("{id}/executions")]
    [ProducesResponseType(typeof(List<ProjectExecutionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProjectExecutionDto>>> GetProjectExecutions(int id, [FromQuery] int limit = 50)
    {
        var executions = await _projectService.GetProjectExecutionsAsync(id, limit);
        return Ok(executions);
    }
}

public class AddRuleRequest
{
    public int RuleId { get; set; }
    public int ExecutionOrder { get; set; } = 0;
}

