using Microsoft.Extensions.Logging;
using System.Text.Json;
using TransformationEngine.Core.Models;
using TransformationEngine.Services;

namespace TransformationEngine.Services;

public class SparkJobBuilderService : ISparkJobBuilderService
{
    private readonly ISparkJobTemplateService _templateService;
    private readonly ISparkJobLibraryService _jobLibraryService;
    private readonly ILogger<SparkJobBuilderService> _logger;
    private readonly string _jobsBasePath;

    public SparkJobBuilderService(
        ISparkJobTemplateService templateService,
        ISparkJobLibraryService jobLibraryService,
        ILogger<SparkJobBuilderService> logger)
    {
        _templateService = templateService;
        _jobLibraryService = jobLibraryService;
        _logger = logger;
        _jobsBasePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../spark-jobs"));
    }

    public async Task<SparkJobDefinition> CreateJobFromTemplateAsync(JobFromTemplateRequest request)
    {
        _logger.LogInformation("Creating job {JobKey} from template {TemplateKey}", request.JobKey, request.TemplateKey);

        // Get template
        var template = await _templateService.GetTemplateAsync(request.TemplateKey);
        if (template == null)
            throw new KeyNotFoundException($"Template not found: {request.TemplateKey}");

        // Validate variables
        var validationResult = await _templateService.ValidateTemplateAsync(request.TemplateKey, request.Variables);
        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                $"Template validation failed: {string.Join("; ", validationResult.Errors)}");
        }

        // Generate code from template
        var generatedCode = await _templateService.RenderTemplateAsync(request.TemplateKey, request.Variables);

        // Create job definition
        var jobDefinition = new SparkJobDefinition
        {
            JobKey = request.JobKey,
            JobName = request.JobName,
            Description = request.Description,
            JobType = "Template",
            Language = template.Language,
            IsGeneric = string.IsNullOrEmpty(request.EntityType),
            EntityType = request.EntityType,
            Category = request.Category ?? template.Category,
            Version = "1.0.0",
            IsActive = true,
            IsTemplate = false,
            DefaultExecutorCores = 2,
            DefaultExecutorMemoryMb = 2048,
            DefaultNumExecutors = 2,
            DefaultDriverMemoryMb = 1024
        };

        // Store based on storage type
        if (request.SaveToFileSystem || request.SaveToDatabase)
        {
            var filePath = await SaveJobToFileSystemAsync(jobDefinition, generatedCode, template.Language);

            jobDefinition.StorageType = request.SaveToDatabase && request.SaveToFileSystem ? "Both" :
                                       request.SaveToDatabase ? "Database" : "FileSystem";
            jobDefinition.FilePath = filePath;

            if (template.Language == "Python")
            {
                jobDefinition.PyFile = Path.GetFileName(filePath);
            }
            else if (template.Language == "CSharp")
            {
                jobDefinition.SourceCode = generatedCode;
                // Would compile here if request.CompileAfterGeneration is true
            }

            // Store template metadata
            jobDefinition.TemplateEngine = "Scriban";
            jobDefinition.TemplateVariables = new Dictionary<string, object>
            {
                { "template_key", request.TemplateKey },
                { "variables", request.Variables }
            };
        }

        // Save to database if requested
        if (request.SaveToDatabase)
        {
            jobDefinition = await _jobLibraryService.CreateJobAsync(jobDefinition);
            _logger.LogInformation("Saved job {JobKey} to database with ID {JobId}", jobDefinition.JobKey, jobDefinition.Id);
        }

        return jobDefinition;
    }

    public async Task<string> GenerateJobCodeFromTemplateAsync(string templateKey, Dictionary<string, object> variables)
    {
        return await _templateService.RenderTemplateAsync(templateKey, variables);
    }

    public async Task<JobBuildResult> BuildAndCompileJobAsync(SparkJobDefinition jobDefinition)
    {
        var result = new JobBuildResult { Success = false };
        var startTime = DateTime.UtcNow;

        try
        {
            if (jobDefinition.Language == "CSharp")
            {
                // For C# jobs, would invoke dotnet build here
                // This is a placeholder for actual compilation
                result.Warnings.Add("C# compilation not yet implemented");
                result.Success = true;
            }
            else if (jobDefinition.Language == "Python")
            {
                // Python doesn't need compilation, just syntax check
                result.Success = true;
            }
            else if (jobDefinition.Language == "Scala")
            {
                // Scala would need sbt or maven build
                result.Warnings.Add("Scala compilation not yet implemented");
                result.Success = true;
            }

            result.BuildDuration = DateTime.UtcNow - startTime;
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
            _logger.LogError(ex, "Build failed for job {JobKey}", jobDefinition.JobKey);
        }

        return result;
    }

    public Task<SparkJobDefinition> GenerateJobFromRulesAsync(RuleToJobRequest request)
    {
        throw new NotImplementedException("Rule-to-job conversion will be implemented in TransformationRuleConverterService");
    }

    public Task<List<SparkJobDefinition>> GenerateJobsForAllEntityTypesAsync(string jobNamePrefix)
    {
        throw new NotImplementedException("Batch job generation not yet implemented");
    }

    public async Task<string> GeneratePythonJobCodeAsync(JobGenerationContext context)
    {
        var variables = new Dictionary<string, object>
        {
            { "job_name", context.JobName },
            { "description", $"ETL job for {context.EntityType}" },
            { "generation_date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
            { "entity_type", context.EntityType },
            { "input_format", context.InputFormat },
            { "output_format", context.OutputFormat }
        };

        // Add transformation rules if provided
        if (context.TransformationRules.Any())
        {
            var transformations = context.TransformationRules.Select(r => new
            {
                target_field = r.FieldName,
                expression = ConvertRuleToPythonExpression(r)
            }).ToList();
            variables["transformations"] = transformations;
        }

        // Merge additional variables
        foreach (var kvp in context.AdditionalVariables)
        {
            variables[kvp.Key] = kvp.Value;
        }

        return await GenerateJobCodeFromTemplateAsync("python-etl-generic", variables);
    }

    public async Task<string> GenerateCSharpJobCodeAsync(JobGenerationContext context)
    {
        var variables = new Dictionary<string, object>
        {
            { "job_name", context.JobName },
            { "description", $"ETL job for {context.EntityType}" },
            { "generation_date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
            { "entity_type", context.EntityType },
            { "namespace", $"SparkJobs.{ToPascalCase(context.EntityType)}" },
            { "input_format", context.InputFormat },
            { "output_format", context.OutputFormat }
        };

        // Add transformation rules if provided
        if (context.TransformationRules.Any())
        {
            var transformations = context.TransformationRules.Select(r => new
            {
                target_field = r.FieldName,
                expression = ConvertRuleToCSharpExpression(r)
            }).ToList();
            variables["transformations"] = transformations;
        }

        // Merge additional variables
        foreach (var kvp in context.AdditionalVariables)
        {
            variables[kvp.Key] = kvp.Value;
        }

        return await GenerateJobCodeFromTemplateAsync("csharp-etl-generic", variables);
    }

    public Task<string> GenerateScalaJobCodeAsync(JobGenerationContext context)
    {
        throw new NotImplementedException("Scala job generation not yet implemented");
    }

    public Task<JobValidationResult> ValidateGeneratedCodeAsync(string code, string language)
    {
        // Basic validation - could be enhanced with actual syntax parsing
        var result = new JobValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(code))
        {
            result.IsValid = false;
            result.Errors.Add("Generated code is empty");
        }

        return Task.FromResult(result);
    }

    public async Task<JobPreviewResult> PreviewJobFromTemplateAsync(string templateKey, Dictionary<string, object> variables)
    {
        var template = await _templateService.GetTemplateAsync(templateKey);
        if (template == null)
            throw new KeyNotFoundException($"Template not found: {templateKey}");

        var validationResult = await _templateService.ValidateTemplateAsync(templateKey, variables);
        var generatedCode = validationResult.IsValid
            ? await _templateService.RenderTemplateAsync(templateKey, variables)
            : string.Empty;

        return new JobPreviewResult
        {
            GeneratedCode = generatedCode,
            Language = template.Language,
            ResolvedVariables = variables,
            ValidationResult = new JobValidationResult
            {
                IsValid = validationResult.IsValid,
                Errors = validationResult.Errors,
                Warnings = validationResult.Warnings
            }
        };
    }

    // Helper methods

    private async Task<string> SaveJobToFileSystemAsync(SparkJobDefinition job, string code, string language)
    {
        string languageFolder = language.ToLower() switch
        {
            "python" => "python",
            "csharp" => "csharp",
            "scala" => "scala",
            _ => throw new ArgumentException($"Unsupported language: {language}")
        };

        string extension = language.ToLower() switch
        {
            "python" => ".py",
            "csharp" => ".cs",
            "scala" => ".scala",
            _ => ".txt"
        };

        var jobFolder = Path.Combine(_jobsBasePath, languageFolder, job.JobKey);
        Directory.CreateDirectory(jobFolder);

        var fileName = $"{job.JobKey}{extension}";
        var filePath = Path.Combine(jobFolder, fileName);

        await File.WriteAllTextAsync(filePath, code);
        _logger.LogInformation("Saved job code to {FilePath}", filePath);

        // Return relative path from spark-jobs root
        return $"{languageFolder}/{job.JobKey}/{fileName}";
    }

    private string ConvertRuleToPythonExpression(TransformationEngine.Models.TransformationRule rule)
    {
        return rule.RuleType switch
        {
            "Replace" => $"regexp_replace(col('{rule.FieldName}'), '{rule.SourcePattern}', '{rule.TargetPattern}')",
            "Upper" => $"upper(col('{rule.FieldName}'))",
            "Lower" => $"lower(col('{rule.FieldName}'))",
            "Trim" => $"trim(col('{rule.FieldName}'))",
            _ => $"col('{rule.FieldName}')"
        };
    }

    private string ConvertRuleToCSharpExpression(TransformationEngine.Models.TransformationRule rule)
    {
        return rule.RuleType switch
        {
            "Replace" => $"RegexpReplace(Col(\"{rule.FieldName}\"), \"{rule.SourcePattern}\", \"{rule.TargetPattern}\")",
            "Upper" => $"Upper(Col(\"{rule.FieldName}\"))",
            "Lower" => $"Lower(Col(\"{rule.FieldName}\"))",
            "Trim" => $"Trim(Col(\"{rule.FieldName}\"))",
            _ => $"Col(\"{rule.FieldName}\")"
        };
    }

    private string ToPascalCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return char.ToUpper(text[0]) + text.Substring(1);
    }
}
