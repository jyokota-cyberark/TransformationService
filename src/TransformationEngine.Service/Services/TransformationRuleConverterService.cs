using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TransformationEngine.Core.Models;
using TransformationEngine.Data;
using TransformationEngine.Models;
using TransformationEngine.Services;

namespace TransformationEngine.Services;

public class TransformationRuleConverterService : ITransformationRuleConverterService
{
    private readonly TransformationEngineDbContext _context;
    private readonly ILogger<TransformationRuleConverterService> _logger;

    public TransformationRuleConverterService(
        TransformationEngineDbContext context,
        ILogger<TransformationRuleConverterService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> ConvertRulesToSparkCodeAsync(string entityType, string language)
    {
        var result = await ConvertRulesWithValidationAsync(entityType, language);
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"Rule conversion failed: {string.Join("; ", result.Errors)}");
        }
        return result.GeneratedCode;
    }

    public async Task<RuleConversionResult> ConvertRulesWithValidationAsync(string entityType, string language)
    {
        _logger.LogInformation("Converting transformation rules for {EntityType} to {Language}", entityType, language);

        var result = new RuleConversionResult
        {
            Success = true,
            Language = language
        };

        // Get all transformation rules for entity type
        var rules = await _context.TransformationRules
            .Where(r => r.InventoryTypeId.ToString() == entityType || r.FieldName.Contains(entityType))
            .ToListAsync();

        if (!rules.Any())
        {
            result.Success = false;
            result.Errors.Add($"No transformation rules found for entity type: {entityType}");
            return result;
        }

        result.TransformationRuleCount = rules.Count;

        // Generate code based on language
        try
        {
            result.GeneratedCode = language.ToLower() switch
            {
                "python" => await GeneratePythonTransformCodeAsync(rules),
                "csharp" => await GenerateCSharpTransformCodeAsync(rules),
                _ => throw new NotSupportedException($"Language not supported: {language}")
            };

            // Compute hash of rules
            result.RuleSetHash = await ComputeRuleSetHashAsync(entityType);
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add(ex.Message);
            _logger.LogError(ex, "Failed to convert rules for {EntityType}", entityType);
        }

        return result;
    }

    public async Task<RuleAnalysis> AnalyzeRulesAsync(string entityType)
    {
        var rules = await _context.TransformationRules
            .Where(r => r.InventoryTypeId.ToString() == entityType || r.FieldName.Contains(entityType))
            .ToListAsync();

        var analysis = new RuleAnalysis
        {
            EntityType = entityType,
            TotalRules = rules.Count,
            TransformationRules = rules.Count(r => !string.IsNullOrEmpty(r.RuleType)),
            ValidationRules = 0, // Would count validation rules if we had them
            SourceFields = rules.Select(r => r.FieldName).Distinct().ToList(),
            TargetFields = rules.Select(r => r.FieldName).Distinct().ToList(),
            UsedOperations = rules.Select(r => r.RuleType).Distinct().ToList(),
            RuleTypeDistribution = rules.GroupBy(r => r.RuleType)
                .ToDictionary(g => g.Key, g => g.Count()),
            HasComplexTransformations = rules.Any(r => r.RuleType == "Custom" || r.RuleType == "Lookup"),
            RequiredFunctions = rules.Select(r => r.RuleType).Distinct().ToList(),
            RecommendedLanguage = rules.Any(r => r.RuleType == "Custom") ? "Python" : "Python"
        };

        return analysis;
    }

    public async Task<bool> HasRulesChangedAsync(string entityType, string previousRuleSetHash)
    {
        var currentHash = await ComputeRuleSetHashAsync(entityType);
        return currentHash != previousRuleSetHash;
    }

    public async Task<string> ComputeRuleSetHashAsync(string entityType)
    {
        var rules = await _context.TransformationRules
            .Where(r => r.InventoryTypeId.ToString() == entityType || r.FieldName.Contains(entityType))
            .OrderBy(r => r.Id)
            .ToListAsync();

        var rulesJson = JsonSerializer.Serialize(rules);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rulesJson));
        return Convert.ToHexString(hashBytes);
    }

    public async Task<TransformationRuleToSparkJobMapping> CreateOrUpdateMappingAsync(
        string entityType,
        int jobDefinitionId,
        string generationStrategy)
    {
        var ruleSetHash = await ComputeRuleSetHashAsync(entityType);
        var rules = await _context.TransformationRules
            .Where(r => r.InventoryTypeId.ToString() == entityType || r.FieldName.Contains(entityType))
            .ToListAsync();

        var rulesSnapshot = JsonSerializer.Serialize(rules);

        var existingMapping = await _context.TransformationRuleToSparkJobMappings
            .FirstOrDefaultAsync(m => m.EntityType == entityType);

        if (existingMapping != null)
        {
            existingMapping.JobDefinitionId = jobDefinitionId;
            existingMapping.RuleSetHash = ruleSetHash;
            existingMapping.RulesSnapshotJson = rulesSnapshot;
            existingMapping.GeneratedAt = DateTime.UtcNow;
            existingMapping.IsStale = false;
            existingMapping.GenerationStrategy = generationStrategy;
            await _context.SaveChangesAsync();
            return existingMapping;
        }

        var newMapping = new TransformationRuleToSparkJobMapping
        {
            EntityType = entityType,
            JobDefinitionId = jobDefinitionId,
            RuleSetHash = ruleSetHash,
            RulesSnapshotJson = rulesSnapshot,
            GeneratedAt = DateTime.UtcNow,
            IsStale = false,
            GenerationStrategy = generationStrategy
        };

        _context.TransformationRuleToSparkJobMappings.Add(newMapping);
        await _context.SaveChangesAsync();

        return newMapping;
    }

    public async Task<TransformationRuleToSparkJobMapping?> GetMappingByEntityTypeAsync(string entityType)
    {
        return await _context.TransformationRuleToSparkJobMappings
            .Include(m => m.JobDefinition)
            .FirstOrDefaultAsync(m => m.EntityType == entityType);
    }

    public async Task<List<TransformationRuleToSparkJobMapping>> GetStaleMappingsAsync()
    {
        var mappings = await _context.TransformationRuleToSparkJobMappings
            .Include(m => m.JobDefinition)
            .ToListAsync();

        var staleMappings = new List<TransformationRuleToSparkJobMapping>();

        foreach (var mapping in mappings)
        {
            var currentHash = await ComputeRuleSetHashAsync(mapping.EntityType);
            if (currentHash != mapping.RuleSetHash)
            {
                staleMappings.Add(mapping);
            }
        }

        return staleMappings;
    }

    public async Task MarkMappingAsStaleAsync(string entityType)
    {
        var mapping = await _context.TransformationRuleToSparkJobMappings
            .FirstOrDefaultAsync(m => m.EntityType == entityType);

        if (mapping != null)
        {
            mapping.IsStale = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task RefreshStaleMappingsAsync()
    {
        var staleMappings = await GetStaleMappingsAsync();

        foreach (var mapping in staleMappings)
        {
            mapping.IsStale = true;
            _logger.LogWarning("Marked mapping for {EntityType} as stale - rules have changed", mapping.EntityType);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<string> GeneratePythonTransformCodeAsync(List<TransformationRule> rules)
    {
        var transformations = new StringBuilder();

        foreach (var rule in rules)
        {
            var expression = ConvertRuleToPythonExpression(rule);
            transformations.AppendLine($"    df = df.withColumn(\"{rule.FieldName}\", {expression})");
        }

        return transformations.ToString();
    }

    public async Task<string> GenerateCSharpTransformCodeAsync(List<TransformationRule> rules)
    {
        var transformations = new StringBuilder();

        foreach (var rule in rules)
        {
            var expression = ConvertRuleToCSharpExpression(rule);
            transformations.AppendLine($"        df = df.WithColumn(\"{rule.FieldName}\", {expression});");
        }

        return await Task.FromResult(transformations.ToString());
    }

    public Task<string> GenerateValidationCodeAsync(List<TransformationRule> validationRules, string language)
    {
        // Placeholder for validation code generation
        throw new NotImplementedException("Validation code generation not yet implemented");
    }

    // Helper methods

    private string ConvertRuleToPythonExpression(TransformationRule rule)
    {
        return rule.RuleType switch
        {
            "Replace" when !string.IsNullOrEmpty(rule.SourcePattern) && !string.IsNullOrEmpty(rule.TargetPattern) =>
                $"regexp_replace(col('{rule.FieldName}'), r'{rule.SourcePattern}', '{rule.TargetPattern}')",

            "RegexReplace" when !string.IsNullOrEmpty(rule.SourcePattern) && !string.IsNullOrEmpty(rule.TargetPattern) =>
                $"regexp_replace(col('{rule.FieldName}'), r'{rule.SourcePattern}', '{rule.TargetPattern}')",

            "Upper" => $"upper(col('{rule.FieldName}'))",
            "Lower" => $"lower(col('{rule.FieldName}'))",
            "Trim" => $"trim(col('{rule.FieldName}'))",
            "Concat" => $"concat(col('{rule.FieldName}'), lit('{rule.TargetPattern}'))",

            "Format" when !string.IsNullOrEmpty(rule.TargetPattern) =>
                $"format_string('{rule.TargetPattern}', col('{rule.FieldName}'))",

            "Cast" when !string.IsNullOrEmpty(rule.TargetPattern) =>
                $"col('{rule.FieldName}').cast('{rule.TargetPattern}')",

            "Lookup" when !string.IsNullOrEmpty(rule.LookupTableJson) =>
                GenerateLookupExpression(rule, "python"),

            _ => $"col('{rule.FieldName}')"
        };
    }

    private string ConvertRuleToCSharpExpression(TransformationRule rule)
    {
        return rule.RuleType switch
        {
            "Replace" when !string.IsNullOrEmpty(rule.SourcePattern) && !string.IsNullOrEmpty(rule.TargetPattern) =>
                $"RegexpReplace(Col(\"{rule.FieldName}\"), \"{rule.SourcePattern}\", \"{rule.TargetPattern}\")",

            "RegexReplace" when !string.IsNullOrEmpty(rule.SourcePattern) && !string.IsNullOrEmpty(rule.TargetPattern) =>
                $"RegexpReplace(Col(\"{rule.FieldName}\"), \"{rule.SourcePattern}\", \"{rule.TargetPattern}\")",

            "Upper" => $"Upper(Col(\"{rule.FieldName}\"))",
            "Lower" => $"Lower(Col(\"{rule.FieldName}\"))",
            "Trim" => $"Trim(Col(\"{rule.FieldName}\"))",
            "Concat" => $"Concat(Col(\"{rule.FieldName}\"), Lit(\"{rule.TargetPattern}\"))",

            "Format" when !string.IsNullOrEmpty(rule.TargetPattern) =>
                $"FormatString(\"{rule.TargetPattern}\", Col(\"{rule.FieldName}\"))",

            "Cast" when !string.IsNullOrEmpty(rule.TargetPattern) =>
                $"Col(\"{rule.FieldName}\").Cast(\"{rule.TargetPattern}\")",

            "Lookup" when !string.IsNullOrEmpty(rule.LookupTableJson) =>
                GenerateLookupExpression(rule, "csharp"),

            _ => $"Col(\"{rule.FieldName}\")"
        };
    }

    private string GenerateLookupExpression(TransformationRule rule, string language)
    {
        // Simplified lookup - in reality would generate more complex when/otherwise chains
        if (language == "python")
        {
            return $"when(col('{rule.FieldName}').isNotNull(), col('{rule.FieldName}')).otherwise(lit(''))";
        }
        else
        {
            return $"When(Col(\"{rule.FieldName}\").IsNotNull(), Col(\"{rule.FieldName}\")).Otherwise(Lit(\"\"))";
        }
    }
}
