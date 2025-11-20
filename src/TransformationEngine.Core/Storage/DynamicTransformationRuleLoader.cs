using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransformationEngine.Builders;
using TransformationEngine.Core;
using TransformationEngine.ScriptEngines;
using TransformationEngine.Transformers;

namespace TransformationEngine.Storage;

/// <summary>
/// Dynamically loads and applies transformation rules from the repository
/// </summary>
public class DynamicTransformationRuleLoader
{
    private readonly ITransformationRuleRepository _repository;
    private readonly ILogger<DynamicTransformationRuleLoader>? _logger;
    private readonly Dictionary<string, ITransformationEngine<object>> _engines = new();
    private readonly object _lock = new();

    public DynamicTransformationRuleLoader(
        ITransformationRuleRepository repository,
        ILogger<DynamicTransformationRuleLoader>? logger = null)
    {
        _repository = repository;
        _logger = logger;

        // Subscribe to rule changes for hot-reload
        _repository.RuleChanged += OnRuleChanged;
    }

    /// <summary>
    /// Loads transformation rules for an event type and builds an engine
    /// </summary>
    public async Task<ITransformationEngine<T>> LoadEngineAsync<T>()
    {
        var eventType = typeof(T).Name;
        var cacheKey = eventType;

        lock (_lock)
        {
            // Check if engine is already cached
            if (_engines.TryGetValue(cacheKey, out var cached) && cached is ITransformationEngine<T> typedEngine)
            {
                return typedEngine;
            }
        }

        // Load rules from repository
        var rules = await _repository.GetRulesAsync(eventType);
        
        // Filter to only enabled rules
        var enabledRules = rules.Where(r => r.Enabled).OrderBy(r => r.Order).ToList();

        _logger?.LogInformation("Loading {Count} transformation rules for {EventType}", enabledRules.Count, eventType);

        // Build pipeline from rules
        var builder = new TransformationPipelineBuilder<T>();
        
        foreach (var rule in enabledRules)
        {
            try
            {
                ApplyRuleToBuilder(builder, rule);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error applying rule {RuleId} to builder: {Error}", rule.Id, ex.Message);
            }
        }

        var engine = builder.Build();

        // Cache the engine
        lock (_lock)
        {
            _engines[cacheKey] = (ITransformationEngine<object>)engine;
        }

        return engine;
    }

    /// <summary>
    /// Applies a transformation rule to the builder
    /// </summary>
    private void ApplyRuleToBuilder<T>(TransformationPipelineBuilder<T> builder, TransformationRule rule)
    {
        switch (rule.TransformerType.ToLowerInvariant())
        {
            case "fieldmapping":
                var mappings = rule.Config.TryGetValue("Mappings", out var mappingsObj) && mappingsObj is JsonElement mappingsJson
                    ? mappingsJson.Deserialize<Dictionary<string, string>>()
                    : new Dictionary<string, string>();

                if (mappings != null && mappings.Count > 0)
                {
                    builder.MapFields(m =>
                    {
                        foreach (var mapping in mappings)
                        {
                            m.From(mapping.Key).To(mapping.Value);
                        }
                    });
                }
                break;

            case "normalization":
                var normOptions = new NormalizationOptions();
                if (rule.Config.TryGetValue("FieldNameCaseStyle", out var caseStyleObj))
                {
                    if (Enum.TryParse<CaseStyle>(caseStyleObj.ToString(), true, out var caseStyle))
                    {
                        normOptions.FieldNameCaseStyle = caseStyle;
                    }
                }
                if (rule.Config.TryGetValue("DateFormat", out var dateFormatObj))
                {
                    normOptions.DateFormat = dateFormatObj.ToString() ?? normOptions.DateFormat;
                }
                if (rule.Config.TryGetValue("NullHandling", out var nullHandlingObj))
                {
                    if (Enum.TryParse<NullHandling>(nullHandlingObj.ToString(), true, out var nullHandling))
                    {
                        normOptions.NullHandling = nullHandling;
                    }
                }
                if (rule.Config.TryGetValue("TrimStrings", out var trimStringsObj))
                {
                    normOptions.TrimStrings = Convert.ToBoolean(trimStringsObj);
                }

                builder.Normalize(o =>
                {
                    o.FieldNameCaseStyle = normOptions.FieldNameCaseStyle;
                    o.DateFormat = normOptions.DateFormat;
                    o.NullHandling = normOptions.NullHandling;
                    o.TrimStrings = normOptions.TrimStrings;
                });
                break;

            case "validation":
                // Validation requires field accessors, so it's limited in dynamic loading
                // Could be extended with expression trees or reflection
                _logger?.LogWarning("Validation rules require code-based configuration. Skipping rule {RuleId}", rule.Id);
                break;

            case "scriptenrichment":
            case "script":
            case "javascript":
                var script = rule.Config.TryGetValue("Script", out var scriptObj) 
                    ? scriptObj.ToString() 
                    : string.Empty;
                var scriptLanguage = rule.Config.TryGetValue("Language", out var langObj)
                    ? langObj.ToString() ?? "javascript"
                    : "javascript";

                var scriptOptions = new ScriptEnrichmentOptions();
                if (rule.Config.TryGetValue("Options", out var optionsObj) && optionsObj is JsonElement optionsJson)
                {
                    if (optionsJson.TryGetProperty("Sandbox", out var sandboxProp))
                    {
                        scriptOptions.Sandbox = sandboxProp.GetBoolean();
                    }
                    if (optionsJson.TryGetProperty("TimeoutMs", out var timeoutProp))
                    {
                        scriptOptions.TimeoutMs = timeoutProp.GetInt32();
                    }
                    if (optionsJson.TryGetProperty("FailOnError", out var failOnErrorProp))
                    {
                        scriptOptions.FailOnError = failOnErrorProp.GetBoolean();
                    }
                }

                // Get script engine from service provider if available
                // For now, create a default Jint engine (pass null for logger to avoid type mismatch)
                var scriptEngine = new ScriptEngines.JintScriptEngine(null);
                
                // Ensure script is not null
                if (string.IsNullOrEmpty(script))
                {
                    _logger?.LogWarning("Script is empty for rule {RuleId}. Skipping.", rule.Id);
                    break;
                }
                
                var scriptTransformer = new ScriptEnrichmentTransformer<T>(
                    script,
                    scriptLanguage,
                    scriptOptions,
                    scriptEngine,
                    null);
                
                builder.AddTransformer(scriptTransformer);
                break;

            case "enrichment":
                // Enrichment requires IEnrichmentSource, so it's limited in dynamic loading
                _logger?.LogWarning("Enrichment rules require code-based configuration. Skipping rule {RuleId}", rule.Id);
                break;

            default:
                _logger?.LogWarning("Unknown transformer type: {Type} for rule {RuleId}", rule.TransformerType, rule.Id);
                break;
        }
    }

    /// <summary>
    /// Clears the engine cache (forces reload on next request)
    /// </summary>
    public void ClearCache(string? eventType = null)
    {
        lock (_lock)
        {
            if (eventType == null)
            {
                _engines.Clear();
                _logger?.LogInformation("Cleared all transformation engine cache");
            }
            else
            {
                _engines.Remove(eventType);
                _logger?.LogInformation("Cleared transformation engine cache for {EventType}", eventType);
            }
        }
    }

    private void OnRuleChanged(object? sender, TransformationRuleChangedEventArgs e)
    {
        _logger?.LogInformation("Rule changed detected: {EventType}/{RuleId} ({ChangeType})", 
            e.EventType, e.RuleId, e.ChangeType);

        // Clear cache for the affected event type to force reload
        ClearCache(e.EventType);
    }
}

