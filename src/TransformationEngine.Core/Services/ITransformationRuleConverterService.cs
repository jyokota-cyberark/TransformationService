using TransformationEngine.Core.Models;
using TransformationEngine.Models;

namespace TransformationEngine.Services;

/// <summary>
/// Service for converting transformation rules to Spark job code
/// </summary>
public interface ITransformationRuleConverterService
{
    // Rule Conversion
    Task<string> ConvertRulesToSparkCodeAsync(string entityType, string language);
    Task<RuleConversionResult> ConvertRulesWithValidationAsync(string entityType, string language);

    // Rule Analysis
    Task<RuleAnalysis> AnalyzeRulesAsync(string entityType);
    Task<bool> HasRulesChangedAsync(string entityType, string previousRuleSetHash);
    Task<string> ComputeRuleSetHashAsync(string entityType);

    // Mapping Management
    Task<TransformationRuleToSparkJobMapping> CreateOrUpdateMappingAsync(
        string entityType,
        int jobDefinitionId,
        string generationStrategy);
    Task<TransformationRuleToSparkJobMapping?> GetMappingByEntityTypeAsync(string entityType);
    Task<List<TransformationRuleToSparkJobMapping>> GetStaleMappingsAsync();
    Task MarkMappingAsStaleAsync(string entityType);
    Task RefreshStaleMappingsAsync();

    // Code Generation from Rules
    Task<string> GeneratePythonTransformCodeAsync(List<TransformationRule> rules);
    Task<string> GenerateCSharpTransformCodeAsync(List<TransformationRule> rules);
    Task<string> GenerateValidationCodeAsync(List<TransformationRule> validationRules, string language);
}

public class RuleConversionResult
{
    public bool Success { get; set; }
    public string GeneratedCode { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string RuleSetHash { get; set; } = string.Empty;
    public int TransformationRuleCount { get; set; }
    public int ValidationRuleCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class RuleAnalysis
{
    public string EntityType { get; set; } = string.Empty;
    public int TotalRules { get; set; }
    public int TransformationRules { get; set; }
    public int ValidationRules { get; set; }
    public List<string> SourceFields { get; set; } = new();
    public List<string> TargetFields { get; set; } = new();
    public List<string> UsedOperations { get; set; } = new();
    public Dictionary<string, int> RuleTypeDistribution { get; set; } = new();
    public bool HasComplexTransformations { get; set; }
    public List<string> RequiredFunctions { get; set; } = new();
    public string RecommendedLanguage { get; set; } = "Python";
}
