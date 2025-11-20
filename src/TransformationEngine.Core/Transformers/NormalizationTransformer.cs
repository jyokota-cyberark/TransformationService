using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core;

namespace TransformationEngine.Transformers;

/// <summary>
/// Transformer that normalizes data formats (case, dates, nulls, etc.)
/// </summary>
public class NormalizationTransformer<T> : ITransformer<T, T>
{
    private readonly NormalizationOptions _options;
    private readonly ILogger<NormalizationTransformer<T>>? _logger;

    public int Order => 20; // After field mapping
    public string Name => "Normalization";

    public NormalizationTransformer(
        NormalizationOptions? options = null,
        ILogger<NormalizationTransformer<T>>? logger = null)
    {
        _options = options ?? new NormalizationOptions();
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

        _logger?.LogDebug("Normalizing data with options: CaseStyle={CaseStyle}, DateFormat={DateFormat}, NullHandling={NullHandling}",
            _options.FieldNameCaseStyle, _options.DateFormat, _options.NullHandling);

        try
        {
            var json = JsonSerializer.Serialize(input);
            var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            var normalizedDict = new Dictionary<string, object?>();

            foreach (var property in root.EnumerateObject())
            {
                var fieldName = NormalizeFieldName(property.Name);
                var value = NormalizeValue(property.Value);

                normalizedDict[fieldName] = value;
            }

            var jsonString = JsonSerializer.Serialize(normalizedDict);
            var result = JsonSerializer.Deserialize<T>(jsonString);

            if (result == null)
            {
                _logger?.LogWarning("Failed to deserialize normalized object, returning original");
                return input;
            }

            _logger?.LogDebug("Successfully normalized data");
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during normalization transformation");
            throw new TransformationException("Normalization transformation failed", ex);
        }
    }

    private string NormalizeFieldName(string name)
    {
        return _options.FieldNameCaseStyle switch
        {
            CaseStyle.PascalCase => ToPascalCase(name),
            CaseStyle.CamelCase => ToCamelCase(name),
            CaseStyle.LowerCase => name.ToLowerInvariant(),
            CaseStyle.UpperCase => name.ToUpperInvariant(),
            _ => name
        };
    }

    private object? NormalizeValue(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            return _options.NullHandling switch
            {
                NullHandling.EmptyString => "",
                NullHandling.DefaultValue => null,
                NullHandling.KeepNull => null,
                _ => null
            };
        }

        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            
            // Normalize dates if configured
            if (_options.NormalizeDates && DateTime.TryParse(value, out var date))
            {
                return date.ToString(_options.DateFormat);
            }

            // Trim whitespace if configured
            if (_options.TrimStrings)
            {
                return value?.Trim();
            }

            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetInt32(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => element.GetRawText(),
            JsonValueKind.Array => element.GetRawText(),
            _ => element.GetRawText()
        };
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpperInvariant(input[0]) + input.Substring(1);
    }

    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToLowerInvariant(input[0]) + input.Substring(1);
    }
}

/// <summary>
/// Options for normalization
/// </summary>
public class NormalizationOptions
{
    public CaseStyle FieldNameCaseStyle { get; set; } = CaseStyle.PascalCase;
    public string DateFormat { get; set; } = "yyyy-MM-ddTHH:mm:ss.fffZ";
    public NullHandling NullHandling { get; set; } = NullHandling.KeepNull;
    public bool NormalizeDates { get; set; } = true;
    public bool TrimStrings { get; set; } = true;
}

/// <summary>
/// Case style for field names
/// </summary>
public enum CaseStyle
{
    PascalCase,
    CamelCase,
    LowerCase,
    UpperCase,
    KeepOriginal
}

/// <summary>
/// How to handle null values
/// </summary>
public enum NullHandling
{
    KeepNull,
    EmptyString,
    DefaultValue
}

