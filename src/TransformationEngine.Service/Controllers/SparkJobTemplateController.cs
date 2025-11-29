using Microsoft.AspNetCore.Mvc;
using TransformationEngine.Core.Models;
using TransformationEngine.Services;

namespace TransformationEngine.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SparkJobTemplateController : ControllerBase
{
    private readonly ISparkJobTemplateService _templateService;
    private readonly ILogger<SparkJobTemplateController> _logger;

    public SparkJobTemplateController(
        ISparkJobTemplateService templateService,
        ILogger<SparkJobTemplateController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllTemplates()
    {
        try
        {
            var templates = await _templateService.GetAllTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve templates");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("built-in")]
    public async Task<IActionResult> GetBuiltInTemplates()
    {
        try
        {
            var templates = await _templateService.GetBuiltInTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve built-in templates");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("language/{language}")]
    public async Task<IActionResult> GetTemplatesByLanguage(string language)
    {
        try
        {
            var templates = await _templateService.GetTemplatesByLanguageAsync(language);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve templates for language {Language}", language);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{templateKey}")]
    public async Task<IActionResult> GetTemplate(string templateKey)
    {
        try
        {
            var template = await _templateService.GetTemplateAsync(templateKey);
            if (template == null)
                return NotFound(new { error = "Template not found", templateKey });

            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve template {TemplateKey}", templateKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{templateKey}/render")]
    public async Task<IActionResult> RenderTemplate(string templateKey, [FromBody] Dictionary<string, object> variables)
    {
        try
        {
            var result = await _templateService.RenderTemplateAsync(templateKey, variables);
            return Ok(new { renderedCode = result });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template {TemplateKey}", templateKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{templateKey}/validate")]
    public async Task<IActionResult> ValidateTemplate(string templateKey, [FromBody] Dictionary<string, object> variables)
    {
        try
        {
            var result = await _templateService.ValidateTemplateAsync(templateKey, variables);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate template {TemplateKey}", templateKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{templateKey}/preview")]
    public async Task<IActionResult> PreviewTemplate(string templateKey, [FromBody] Dictionary<string, object> variables)
    {
        try
        {
            var result = await _templateService.PreviewTemplateAsync(templateKey, variables);
            return Ok(new { preview = result });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview template {TemplateKey}", templateKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("{templateKey}/statistics")]
    public async Task<IActionResult> GetTemplateStatistics(string templateKey)
    {
        try
        {
            var stats = await _templateService.GetTemplateStatisticsAsync(templateKey);
            return Ok(stats);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get template statistics for {TemplateKey}", templateKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get template by ID (for UI editing)
    /// </summary>
    [HttpGet("by-id/{id}")]
    public async Task<IActionResult> GetTemplateById(int id)
    {
        try
        {
            var templates = await _templateService.GetAllTemplatesAsync();
            var template = templates.FirstOrDefault(t => t.Id == id);
            if (template == null)
                return NotFound(new { error = "Template not found", id });

            return Ok(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve template {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new template
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTemplate([FromBody] SparkJobTemplate template)
    {
        try
        {
            var created = await _templateService.CreateTemplateAsync(template);
            return CreatedAtAction(nameof(GetTemplate), new { templateKey = created.TemplateKey }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create template");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing template
    /// </summary>
    [HttpPut("{templateKey}")]
    public async Task<IActionResult> UpdateTemplate(string templateKey, [FromBody] SparkJobTemplate template)
    {
        try
        {
            var updated = await _templateService.UpdateTemplateAsync(templateKey, template);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update template {TemplateKey}", templateKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update template by ID (for UI editing)
    /// </summary>
    [HttpPut("by-id/{id}")]
    public async Task<IActionResult> UpdateTemplateById(int id, [FromBody] SparkJobTemplate template)
    {
        try
        {
            var templates = await _templateService.GetAllTemplatesAsync();
            var existing = templates.FirstOrDefault(t => t.Id == id);
            if (existing == null)
                return NotFound(new { error = "Template not found", id });

            var updated = await _templateService.UpdateTemplateAsync(existing.TemplateKey, template);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update template {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a template
    /// </summary>
    [HttpDelete("{templateKey}")]
    public async Task<IActionResult> DeleteTemplate(string templateKey)
    {
        try
        {
            var deleted = await _templateService.DeleteTemplateAsync(templateKey);
            if (!deleted)
                return NotFound(new { error = "Template not found", templateKey });

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete template {TemplateKey}", templateKey);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete template by ID (for UI)
    /// </summary>
    [HttpDelete("by-id/{id}")]
    public async Task<IActionResult> DeleteTemplateById(int id)
    {
        try
        {
            var templates = await _templateService.GetAllTemplatesAsync();
            var template = templates.FirstOrDefault(t => t.Id == id);
            if (template == null)
                return NotFound(new { error = "Template not found", id });

            var deleted = await _templateService.DeleteTemplateAsync(template.TemplateKey);
            if (!deleted)
                return NotFound(new { error = "Template not found", id });

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete template {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
