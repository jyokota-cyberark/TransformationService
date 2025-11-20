using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core;

namespace TransformationEngine.Transformers;

/// <summary>
/// Transformer that executes JavaScript or other scripting languages for enrichment
/// </summary>
public class ScriptEnrichmentTransformer<T> : ITransformer<T, T>
{
    private readonly string _script;
    private readonly string _language;
    private readonly ScriptEnrichmentOptions _options;
    private readonly ILogger<ScriptEnrichmentTransformer<T>>? _logger;
    private readonly IScriptEngine? _scriptEngine;

    public int Order => 25; // After normalization, before validation
    public string Name => "ScriptEnrichment";

    public ScriptEnrichmentTransformer(
        string script,
        string language = "javascript",
        ScriptEnrichmentOptions? options = null,
        IScriptEngine? scriptEngine = null,
        ILogger<ScriptEnrichmentTransformer<T>>? logger = null)
    {
        _script = script ?? throw new ArgumentNullException(nameof(script));
        _language = language.ToLowerInvariant();
        _options = options ?? new ScriptEnrichmentOptions();
        _logger = logger;
        _scriptEngine = scriptEngine;
    }

    public bool CanTransform(Type inputType, Type outputType)
    {
        return inputType == typeof(T) && outputType == typeof(T);
    }

    public async Task<T> TransformAsync(T input, TransformationContext context)
    {
        if (input == null)
            return input!;

        if (string.IsNullOrWhiteSpace(_script))
        {
            _logger?.LogDebug("Script is empty, skipping enrichment");
            return input;
        }

        if (_scriptEngine == null)
        {
            _logger?.LogWarning("Script engine not available, skipping enrichment");
            return input;
        }

        _logger?.LogDebug("Executing {Language} script enrichment", _language);

        try
        {
            // Convert input to dictionary for script access
            var json = JsonSerializer.Serialize(input);
            var jsonDoc = JsonDocument.Parse(json);
            var dataDict = ConvertToDictionary(jsonDoc.RootElement);

            // Execute script
            var enrichedDict = await _scriptEngine.ExecuteAsync(
                _script,
                dataDict,
                _language,
                _options,
                context);

            if (enrichedDict == null)
            {
                _logger?.LogWarning("Script returned null, returning original input");
                return input;
            }

            // Convert back to type T
            var enrichedJson = JsonSerializer.Serialize(enrichedDict);
            var result = JsonSerializer.Deserialize<T>(enrichedJson);

            if (result == null)
            {
                _logger?.LogWarning("Failed to deserialize enriched object, returning original");
                return input;
            }

            _logger?.LogDebug("Successfully executed script enrichment");
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during script enrichment transformation");
            
            if (_options.FailOnError)
            {
                throw new TransformationException("Script enrichment transformation failed", ex);
            }
            
            _logger?.LogWarning("Script enrichment failed but FailOnError is false, returning original input");
            return input;
        }
    }

    private Dictionary<string, object?> ConvertToDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetInt32(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Object => ConvertToDictionary(property.Value),
                JsonValueKind.Array => property.Value.EnumerateArray().Select(ConvertToDictionary).ToList(),
                _ => property.Value.GetRawText()
            };
        }

        return dict;
    }
}

/// <summary>
/// Options for script enrichment
/// </summary>
public class ScriptEnrichmentOptions
{
    public bool Sandbox { get; set; } = true;
    public int TimeoutMs { get; set; } = 5000;
    public bool FailOnError { get; set; } = false;
    public Dictionary<string, object>? AdditionalContext { get; set; }
}

/// <summary>
/// Interface for script engines
/// </summary>
public interface IScriptEngine
{
    Task<Dictionary<string, object?>?> ExecuteAsync(
        string script,
        Dictionary<string, object?> inputData,
        string language,
        ScriptEnrichmentOptions options,
        TransformationContext context);
}

