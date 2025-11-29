using Microsoft.AspNetCore.Mvc;
using TransformationEngine.Services;

namespace TransformationEngine.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SparkJobBuilderController : ControllerBase
{
    private readonly ISparkJobBuilderService _builderService;
    private readonly ITransformationRuleConverterService _ruleConverter;
    private readonly ILogger<SparkJobBuilderController> _logger;

    public SparkJobBuilderController(
        ISparkJobBuilderService builderService,
        ITransformationRuleConverterService ruleConverter,
        ILogger<SparkJobBuilderController> logger)
    {
        _builderService = builderService;
        _ruleConverter = ruleConverter;
        _logger = logger;
    }

    [HttpPost("from-template")]
    public async Task<IActionResult> CreateJobFromTemplate([FromBody] JobFromTemplateRequest request)
    {
        try
        {
            var jobDefinition = await _builderService.CreateJobFromTemplateAsync(request);
            return CreatedAtAction(nameof(GetJobPreview), new { templateKey = request.TemplateKey }, jobDefinition);
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
            _logger.LogError(ex, "Failed to create job from template");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("preview")]
    public async Task<IActionResult> PreviewJob([FromBody] JobPreviewRequest request)
    {
        try
        {
            var preview = await _builderService.PreviewJobFromTemplateAsync(request.TemplateKey, request.Variables);
            return Ok(preview);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview job");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("generate/python")]
    public async Task<IActionResult> GeneratePythonJob([FromBody] JobGenerationContext context)
    {
        try
        {
            var code = await _builderService.GeneratePythonJobCodeAsync(context);
            return Ok(new { generatedCode = code, language = "Python" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Python job");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("generate/csharp")]
    public async Task<IActionResult> GenerateCSharpJob([FromBody] JobGenerationContext context)
    {
        try
        {
            var code = await _builderService.GenerateCSharpJobCodeAsync(context);
            return Ok(new { generatedCode = code, language = "CSharp" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate C# job");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("from-rules")]
    public async Task<IActionResult> CreateJobFromRules([FromBody] RuleToJobRequest request)
    {
        try
        {
            var conversionResult = await _ruleConverter.ConvertRulesWithValidationAsync(request.EntityType, request.Language);

            if (!conversionResult.Success)
            {
                return BadRequest(new
                {
                    error = "Rule conversion failed",
                    errors = conversionResult.Errors,
                    warnings = conversionResult.Warnings
                });
            }

            return Ok(new
            {
                generatedCode = conversionResult.GeneratedCode,
                language = conversionResult.Language,
                ruleSetHash = conversionResult.RuleSetHash,
                transformationRuleCount = conversionResult.TransformationRuleCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create job from rules");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("rules/analyze/{entityType}")]
    public async Task<IActionResult> AnalyzeRules(string entityType)
    {
        try
        {
            var analysis = await _ruleConverter.AnalyzeRulesAsync(entityType);
            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze rules for {EntityType}", entityType);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("rules/mappings/stale")]
    public async Task<IActionResult> GetStaleMappings()
    {
        try
        {
            var staleMappings = await _ruleConverter.GetStaleMappingsAsync();
            return Ok(staleMappings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stale mappings");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("preview/{templateKey}")]
    public Task<IActionResult> GetJobPreview(string templateKey)
    {
        // Helper action for CreatedAtAction
        return Task.FromResult<IActionResult>(Ok(new { templateKey }));
    }
}

public class JobPreviewRequest
{
    public string TemplateKey { get; set; } = string.Empty;
    public Dictionary<string, object> Variables { get; set; } = new();
}
