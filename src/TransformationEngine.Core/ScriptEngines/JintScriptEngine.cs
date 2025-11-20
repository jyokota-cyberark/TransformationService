using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core;
using TransformationEngine.Transformers;

namespace TransformationEngine.ScriptEngines;

/// <summary>
/// JavaScript engine implementation using Jint
/// </summary>
public class JintScriptEngine : IScriptEngine
{
    private readonly ILogger<JintScriptEngine>? _logger;

    public JintScriptEngine(ILogger<JintScriptEngine>? logger = null)
    {
        _logger = logger;
    }

    public async Task<Dictionary<string, object?>?> ExecuteAsync(
        string script,
        Dictionary<string, object?> inputData,
        string language,
        ScriptEnrichmentOptions options,
        TransformationContext context)
    {
        if (language != "javascript" && language != "js")
        {
            _logger?.LogWarning("JintScriptEngine only supports JavaScript, got: {Language}", language);
            return null;
        }

        try
        {
            // Create Jint engine - configure via Options property
            var engine = new Jint.Engine(cfg =>
            {
                // Note: Jint 3.x has different API for setting limits
                // These can be configured via the Constraints property if needed
            });

            // Add helper functions
            AddHelperFunctions(engine, options);

            // Set input data as global variable - convert to JsValue
            engine.SetValue("data", Jint.Native.JsValue.FromObject(engine, inputData));
            engine.SetValue("input", Jint.Native.JsValue.FromObject(engine, inputData));

            // Add context properties if available
            if (context != null)
            {
                engine.SetValue("context", new
                {
                    EventType = context.EventType,
                    Timestamp = context.Timestamp,
                    SourceService = context.SourceService,
                    Properties = context.Properties
                });
            }

            // Wrap script in a function if not already wrapped
            var wrappedScript = WrapScript(script);

            // Execute script
            var result = engine.Evaluate(wrappedScript);

            // Extract result
            if (result.Type == Jint.Runtime.Types.Object)
            {
                var resultDict = ConvertJsValueToDict(result);
                return resultDict;
            }
            else if (result.Type == Jint.Runtime.Types.Undefined || result.Type == Jint.Runtime.Types.Null)
            {
                // If script doesn't return anything, use modified data
                var dataValue = engine.GetValue("data");
                if (dataValue.Type == Jint.Runtime.Types.Object)
                {
                    var resultDict = ConvertJsValueToDict(dataValue);
                    return resultDict;
                }
            }

            _logger?.LogWarning("Script did not return an object, returning null");
            return null;
        }
        catch (Jint.Runtime.JavaScriptException ex)
        {
            _logger?.LogError(ex, "JavaScript execution error: {Error}", ex.Message);
            throw new TransformationException($"JavaScript execution failed: {ex.Message}", ex);
        }
        catch (TimeoutException ex)
        {
            _logger?.LogError(ex, "Script execution timeout after {Timeout}ms", options.TimeoutMs);
            throw new TransformationException($"Script execution timeout after {options.TimeoutMs}ms", ex);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing JavaScript script");
            throw new TransformationException("Script execution failed", ex);
        }
    }

    private string WrapScript(string script)
    {
        // Check if script is already a function
        var trimmed = script.Trim();
        if (trimmed.StartsWith("function", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("(function", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("(data) =>", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("data =>", StringComparison.OrdinalIgnoreCase))
        {
            // Script is already a function, just execute it
            return $@"
                (function() {{
                    {script}
                    if (typeof enrich === 'function') {{
                        return enrich(data);
                    }} else if (typeof transform === 'function') {{
                        return transform(data);
                    }} else {{
                        return data;
                    }}
                }})();
            ";
        }

        // Wrap in a function
        return $@"
            (function() {{
                {script}
                return data;
            }})();
        ";
    }

    private void AddHelperFunctions(Jint.Engine engine, ScriptEnrichmentOptions options)
    {
        // Date formatting helper
        engine.SetValue("formatDate", new Func<object?, string, string>((date, format) =>
        {
            if (date == null) return string.Empty;
            
            if (date is DateTime dt)
            {
                return dt.ToString(format);
            }
            
            if (date is string dateStr && DateTime.TryParse(dateStr, out var parsed))
            {
                return parsed.ToString(format);
            }
            
            return date.ToString() ?? string.Empty;
        }));

        // String helpers
        engine.SetValue("trim", new Func<string?, string>(s => s?.Trim() ?? string.Empty));
        engine.SetValue("upperCase", new Func<string?, string>(s => s?.ToUpperInvariant() ?? string.Empty));
        engine.SetValue("lowerCase", new Func<string?, string>(s => s?.ToLowerInvariant() ?? string.Empty));

        // Math helpers
        engine.SetValue("Math", new
        {
            round = new Func<double, int, double>((value, decimals) => Math.Round(value, decimals)),
            floor = new Func<double, double>(Math.Floor),
            ceil = new Func<double, double>(Math.Ceiling),
            abs = new Func<double, double>(Math.Abs),
            max = new Func<double, double, double>(Math.Max),
            min = new Func<double, double, double>(Math.Min)
        });

        // Logging helper (if not sandboxed)
        if (!options.Sandbox)
        {
            engine.SetValue("log", new Action<object?>(obj =>
            {
                _logger?.LogInformation("Script log: {Message}", obj?.ToString());
            }));
        }
    }

    private Dictionary<string, object?>? ConvertJsValueToDict(Jint.Native.JsValue value)
    {
        if (value.Type != Jint.Runtime.Types.Object)
            return null;

        // Try converting to dictionary via ToObject
        try
        {
            var obj = value.ToObject();
            if (obj is System.Collections.Generic.IDictionary<string, object?> dict)
            {
                return new Dictionary<string, object?>(dict);
            }

            // Serialize and deserialize for complex objects
            var json = JsonSerializer.Serialize(obj);
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        }
        catch
        {
            return null;
        }
    }

    private object? ConvertJsValue(Jint.Native.JsValue value)
    {
        switch (value.Type)
        {
            case Jint.Runtime.Types.Null:
            case Jint.Runtime.Types.Undefined:
                return null;
            
            case Jint.Runtime.Types.Boolean:
                return value.ToObject();
            
            case Jint.Runtime.Types.Number:
                return value.ToObject();
            
            case Jint.Runtime.Types.String:
                return value.ToObject();
            
            case Jint.Runtime.Types.Object:
                // Use ToObject for all object types - Jint will handle conversion
                var objValue = value.ToObject();
                
                // Handle lists/arrays
                if (objValue is System.Collections.IEnumerable enumerable && !(objValue is string))
                {
                    var list = new List<object?>();
                    foreach (var item in enumerable)
                    {
                        list.Add(item);
                    }
                    return list;
                }
                
                return objValue;
            
            default:
                return value.ToString();
        }
    }
}

