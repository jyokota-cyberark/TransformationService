using Microsoft.AspNetCore.Mvc;
using TransformationEngine.Core.Models.DTOs;
using TransformationEngine.Service.Services;

namespace TransformationEngine.Service.Controllers;

[ApiController]
[Route("api/airflow/dags")]
[Produces("application/json")]
public class AirflowDagController : ControllerBase
{
    private readonly IAirflowDagGeneratorService _dagGeneratorService;
    private readonly ILogger<AirflowDagController> _logger;

    public AirflowDagController(
        IAirflowDagGeneratorService dagGeneratorService,
        ILogger<AirflowDagController> logger)
    {
        _dagGeneratorService = dagGeneratorService;
        _logger = logger;
    }

    /// <summary>
    /// Generate a new Airflow DAG
    /// </summary>
    /// <param name="request">DAG generation request</param>
    /// <returns>Generated DAG definition</returns>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(AirflowDagDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AirflowDagDto>> GenerateDag([FromBody] GenerateDagRequest request)
    {
        try
        {
            var dag = await _dagGeneratorService.GenerateDagAsync(request);
            return CreatedAtAction(nameof(GetDagById), new { id = dag.Id }, dag);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating DAG: {DagId}", request.DagId);
            return StatusCode(500, new { error = "An error occurred while generating the DAG" });
        }
    }

    /// <summary>
    /// Preview a DAG without saving it
    /// </summary>
    /// <param name="request">DAG generation request</param>
    /// <returns>DAG preview with generated code</returns>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(DagPreviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DagPreviewResponse>> PreviewDag([FromBody] GenerateDagRequest request)
    {
        var preview = await _dagGeneratorService.PreviewDagAsync(request);
        return Ok(preview);
    }

    /// <summary>
    /// Get all DAG definitions
    /// </summary>
    /// <returns>List of DAG definitions</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<AirflowDagDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AirflowDagDto>>> GetAllDags()
    {
        var dags = await _dagGeneratorService.GetAllDagsAsync();
        return Ok(dags);
    }

    /// <summary>
    /// Get a specific DAG definition by ID
    /// </summary>
    /// <param name="id">DAG definition ID</param>
    /// <returns>DAG definition</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AirflowDagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AirflowDagDto>> GetDagById(int id)
    {
        var dag = await _dagGeneratorService.GetDagByIdAsync(id);
        if (dag == null)
        {
            return NotFound(new { error = $"DAG definition with ID {id} not found" });
        }
        return Ok(dag);
    }

    /// <summary>
    /// Regenerate an existing DAG
    /// </summary>
    /// <param name="id">DAG definition ID</param>
    /// <returns>Updated DAG definition</returns>
    [HttpPost("{id}/regenerate")]
    [ProducesResponseType(typeof(AirflowDagDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AirflowDagDto>> RegenerateDag(int id)
    {
        try
        {
            var dag = await _dagGeneratorService.RegenerateDagAsync(id);
            return Ok(dag);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error regenerating DAG: {Id}", id);
            return StatusCode(500, new { error = "An error occurred while regenerating the DAG" });
        }
    }

    /// <summary>
    /// Delete a DAG definition
    /// </summary>
    /// <param name="id">DAG definition ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDag(int id)
    {
        var deleted = await _dagGeneratorService.DeleteDagAsync(id);
        if (!deleted)
        {
            return NotFound(new { error = $"DAG definition with ID {id} not found" });
        }
        return NoContent();
    }
}

