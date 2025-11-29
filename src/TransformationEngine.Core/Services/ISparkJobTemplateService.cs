using TransformationEngine.Core.Models;

namespace TransformationEngine.Services;

/// <summary>
/// Service for managing Spark job templates
/// </summary>
public interface ISparkJobTemplateService
{
    // Template CRUD
    Task<SparkJobTemplate> CreateTemplateAsync(SparkJobTemplate template);
    Task<SparkJobTemplate> UpdateTemplateAsync(string templateKey, SparkJobTemplate template);
    Task<bool> DeleteTemplateAsync(string templateKey);
    Task<SparkJobTemplate?> GetTemplateAsync(string templateKey);
    Task<List<SparkJobTemplate>> GetAllTemplatesAsync();
    Task<List<SparkJobTemplate>> GetTemplatesByLanguageAsync(string language);
    Task<List<SparkJobTemplate>> GetTemplatesByCategoryAsync(string category);
    Task<List<SparkJobTemplate>> GetBuiltInTemplatesAsync();

    // Template Rendering
    Task<string> RenderTemplateAsync(string templateKey, Dictionary<string, object> variables);
    Task<TemplateValidationResult> ValidateTemplateAsync(string templateKey, Dictionary<string, object> variables);
    Task<string> PreviewTemplateAsync(string templateKey, Dictionary<string, object> variables);

    // Template Statistics
    Task IncrementUsageCountAsync(string templateKey);
    Task<TemplateStatistics> GetTemplateStatisticsAsync(string templateKey);
}

public class TemplateValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public List<string> MissingVariables { get; set; } = new();
    public List<string> UnusedVariables { get; set; } = new();
}

public class TemplateStatistics
{
    public string TemplateKey { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public int JobsCreatedFromTemplate { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public List<string> PopularVariableSets { get; set; } = new();
}
