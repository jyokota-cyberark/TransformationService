using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core;

namespace TransformationEngine.Transformers;

/// <summary>
/// Transformer that maps field names from source to target format
/// </summary>
public class FieldMappingTransformer<T> : ITransformer<T, T>
{
    private readonly Dictionary<string, string> _fieldMappings;
    private readonly ILogger<FieldMappingTransformer<T>>? _logger;

    public int Order => 10; // Early in pipeline
    public string Name => "FieldMapping";

    public FieldMappingTransformer(
        Dictionary<string, string>? fieldMappings = null,
        ILogger<FieldMappingTransformer<T>>? logger = null)
    {
        _fieldMappings = fieldMappings ?? new Dictionary<string, string>();
        _logger = logger;
    }

    public bool CanTransform(Type inputType, Type outputType)
    {
        return inputType == typeof(T) && outputType == typeof(T);
    }

    public async Task<T> TransformAsync(T input, TransformationContext context)
    {
        if (input == null)
            return input!;

        if (_fieldMappings.Count == 0)
        {
            _logger?.LogDebug("No field mappings configured, skipping transformation");
            return input;
        }

        _logger?.LogDebug("Applying {Count} field mappings", _fieldMappings.Count);

        try
        {
            // Convert to JSON for manipulation
            var json = JsonSerializer.Serialize(input);
            var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            // Create new dictionary with mapped fields
            var mappedDict = new Dictionary<string, object?>();

            foreach (var property in root.EnumerateObject())
            {
                var sourceName = property.Name;
                var targetName = _fieldMappings.TryGetValue(sourceName, out var mapped) 
                    ? mapped 
                    : sourceName;

                // Convert value based on type
                object? value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetInt32(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    JsonValueKind.Object => property.Value.GetRawText(),
                    JsonValueKind.Array => property.Value.GetRawText(),
                    _ => property.Value.GetRawText()
                };

                mappedDict[targetName] = value;
            }

            // Deserialize back to type T
            var jsonString = JsonSerializer.Serialize(mappedDict);
            var result = JsonSerializer.Deserialize<T>(jsonString);

            if (result == null)
            {
                _logger?.LogWarning("Failed to deserialize mapped object, returning original");
                return input;
            }

            _logger?.LogDebug("Successfully mapped {Count} fields", _fieldMappings.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during field mapping transformation");
            throw new TransformationException("Field mapping transformation failed", ex);
        }
    }
}

