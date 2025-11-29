namespace TransformationEngine.Integration.Services;

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using TransformationEngine.Integration.Data;
using TransformationEngine.Integration.Models;

/// <summary>
/// Implementation of transformation debug service
/// </summary>
public class DebugService : IDebugService
{
    private readonly IIntegratedTransformationService _transformationService;
    private readonly ITransformationConfigRepository _configRepository;
    private readonly ITransformationHealthCheck _healthCheck;
    private readonly ILogger<DebugService> _logger;

    public DebugService(
        IIntegratedTransformationService transformationService,
        ITransformationConfigRepository configRepository,
        ITransformationHealthCheck healthCheck,
        ILogger<DebugService> logger)
    {
        _transformationService = transformationService;
        _configRepository = configRepository;
        _healthCheck = healthCheck;
        _logger = logger;
    }

    public async Task<DryRunResult> DryRunTransformAsync(DryRunRequest request)
    {
        var sw = Stopwatch.StartNew();
        var result = new DryRunResult
        {
            RawData = request.RawDataJson
        };

        try
        {
            // Perform transformation
            var transformRequest = new TransformationRequest
            {
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                RawData = request.RawDataJson
            };

            var transformResult = await _transformationService.TransformAsync(transformRequest);

            result.Success = transformResult.Success;
            result.TransformedData = transformResult.TransformedData;
            result.ErrorMessage = transformResult.ErrorMessage;

            // Build rule application details
            if (transformResult.Success && !string.IsNullOrEmpty(transformResult.TransformedData))
            {
                try
                {
                    var rawObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.RawDataJson);
                    var transformedObj = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(transformResult.TransformedData);

                    if (rawObj != null && transformedObj != null)
                    {
                        foreach (var kvp in transformedObj)
                        {
                            var rawValue = rawObj.ContainsKey(kvp.Key)
                                ? rawObj[kvp.Key].ToString()
                                : null;
                            var transformedValue = kvp.Value.ToString();

                            if (rawValue != transformedValue)
                            {
                                result.RulesApplied.Add(new RuleApplication
                                {
                                    FieldName = kvp.Key,
                                    BeforeValue = rawValue,
                                    AfterValue = transformedValue,
                                    Applied = true
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not parse transformation results for diff");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dry run transformation");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        sw.Stop();
        result.DurationMs = sw.ElapsedMilliseconds;
        return result;
    }

    public async Task<DataComparisonResult> CompareRawVsTransformedAsync(
        string entityType,
        int entityId)
    {
        var result = new DataComparisonResult
        {
            EntityType = entityType,
            EntityId = entityId
        };

        try
        {
            // This would need to be implemented based on your data access pattern
            // For now, returning empty result
            _logger.LogWarning("CompareRawVsTransformedAsync not fully implemented - needs data access layer integration");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing raw vs transformed data");
        }

        return result;
    }

    public async Task<RuleTestResult> TestRulesAsync(RuleTestRequest request)
    {
        var result = new RuleTestResult
        {
            InputData = request.SampleDataJson
        };

        try
        {
            // Perform dry-run transformation
            var dryRunRequest = new DryRunRequest
            {
                EntityType = request.EntityType,
                EntityId = 0, // Test mode
                RawDataJson = request.SampleDataJson,
                SpecificRules = request.RuleIds?.Select(id => id.ToString()).ToList()
            };

            var dryRunResult = await DryRunTransformAsync(dryRunRequest);

            result.Success = dryRunResult.Success;
            result.OutputData = dryRunResult.TransformedData;
            result.RuleResults = dryRunResult.RulesApplied;
            result.ErrorMessage = dryRunResult.ErrorMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing rules");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<ConfigValidationResult> ValidateConfigAsync(string entityType)
    {
        var result = new ConfigValidationResult();

        try
        {
            // Check service health
            var health = await _healthCheck.GetHealthStatusAsync();
            result.ServiceHealthy = health.IsHealthy;

            if (!health.IsHealthy)
            {
                result.Warnings.Add($"Transformation service is unhealthy: {health.Message}");
            }

            // Get configuration
            var config = await _configRepository.GetEntityConfigAsync(entityType);
            if (config == null)
            {
                result.Errors.Add($"No configuration found for entity type: {entityType}");
                result.IsValid = false;
                return result;
            }

            result.ConfigMode = config.Mode.ToString();

            // Get rules
            var rules = await _configRepository.GetRulesAsync(entityType);
            result.RuleCount = rules.Count;

            if (rules.Count == 0)
            {
                result.Warnings.Add("No transformation rules configured");
            }

            // Validate each rule
            foreach (var rule in rules)
            {
                if (false) // Skip field validation
                    result.Errors.Add($"Rule '{rule.RuleName}' has no field name");

                if (string.IsNullOrEmpty(rule.RuleName))
                    result.Errors.Add($"Rule for field '{rule.RuleName}' has no name");

                // Type-specific validation
                if (rule.RuleType == "CustomScript" && string.IsNullOrEmpty(rule.Configuration))
                    result.Errors.Add($"Rule '{rule.RuleName}' is CustomScript but has no script");

                if ((rule.RuleType == "Replace" || rule.RuleType == "RegexReplace")
                    && string.IsNullOrEmpty(rule.Configuration))
                    result.Errors.Add($"Rule '{rule.RuleName}' is {rule.RuleType} but has no source pattern");

                if (rule.RuleType == "Lookup" && string.IsNullOrEmpty(rule.Configuration))
                    result.Errors.Add($"Rule '{rule.RuleName}' is Lookup but has no lookup table");
            }

            result.IsValid = result.Errors.Count == 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating configuration");
            result.Errors.Add($"Validation error: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }
}
