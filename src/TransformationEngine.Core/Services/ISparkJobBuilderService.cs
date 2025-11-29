using TransformationEngine.Core.Models;
using TransformationEngine.Models;

namespace TransformationEngine.Services;

/// <summary>
/// Service for building Spark jobs from templates and generating code
/// </summary>
public interface ISparkJobBuilderService
{
    // Template-based Job Creation
    Task<SparkJobDefinition> CreateJobFromTemplateAsync(JobFromTemplateRequest request);
    Task<string> GenerateJobCodeFromTemplateAsync(string templateKey, Dictionary<string, object> variables);
    Task<JobBuildResult> BuildAndCompileJobAsync(SparkJobDefinition jobDefinition);

    // Rule-based Job Generation
    Task<SparkJobDefinition> GenerateJobFromRulesAsync(RuleToJobRequest request);
    Task<List<SparkJobDefinition>> GenerateJobsForAllEntityTypesAsync(string jobNamePrefix);

    // Job Code Generation
    Task<string> GeneratePythonJobCodeAsync(JobGenerationContext context);
    Task<string> GenerateCSharpJobCodeAsync(JobGenerationContext context);
    Task<string> GenerateScalaJobCodeAsync(JobGenerationContext context);

    // Validation and Preview
    Task<JobValidationResult> ValidateGeneratedCodeAsync(string code, string language);
    Task<JobPreviewResult> PreviewJobFromTemplateAsync(string templateKey, Dictionary<string, object> variables);
}

public class JobFromTemplateRequest
{
    public string TemplateKey { get; set; } = string.Empty;
    public string JobKey { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, object> Variables { get; set; } = new();
    public string? EntityType { get; set; }
    public string? Category { get; set; }
    public bool SaveToFileSystem { get; set; } = true;
    public bool SaveToDatabase { get; set; } = true;
    public bool CompileAfterGeneration { get; set; } = false;
}

public class RuleToJobRequest
{
    public string EntityType { get; set; } = string.Empty;
    public string JobKey { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string Language { get; set; } = "Python";
    public string GenerationStrategy { get; set; } = "DirectConversion"; // 'DirectConversion', 'TemplateInjection'
    public string? TemplateKey { get; set; }
    public bool IncludeValidationRules { get; set; } = true;
    public bool IncludeTransformationRules { get; set; } = true;
    public bool SaveToFileSystem { get; set; } = true;
    public bool SaveToDatabase { get; set; } = true;
}

public class JobGenerationContext
{
    public string EntityType { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public List<TransformationRule> TransformationRules { get; set; } = new();
    public Dictionary<string, object> AdditionalVariables { get; set; } = new();
    public string InputPath { get; set; } = "/data/input";
    public string OutputPath { get; set; } = "/data/output";
    public string InputFormat { get; set; } = "json";
    public string OutputFormat { get; set; } = "parquet";
}

public class JobBuildResult
{
    public bool Success { get; set; }
    public string? CompiledArtifactPath { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? BuildOutput { get; set; }
    public TimeSpan BuildDuration { get; set; }
}

public class JobPreviewResult
{
    public string GeneratedCode { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public Dictionary<string, object> ResolvedVariables { get; set; } = new();
    public List<string> RequiredDependencies { get; set; } = new();
    public JobValidationResult ValidationResult { get; set; } = new();
}
