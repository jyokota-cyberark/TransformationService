using TransformationEngine.Core.Models;
using TransformationEngine.Integration.Models;
using TransformationEngine.Integration.Evaluators;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TransformationEngine.Integration.Processors;

/// <summary>
/// Processes field mappings with support for direct, rule-based, computed (Cedar DSL), and constant mappings
/// </summary>
public interface IFieldMappingProcessor
{
    /// <summary>
    /// Apply field mappings to an entity object
    /// </summary>
    Task<Dictionary<string, object?>> ApplyMappingsAsync(
        string entityType,
        object sourceEntity,
        List<FieldMapping> mappings,
        PermissionEvaluationContext? context = null);
}

/// <summary>
/// Implementation of field mapping processor
/// </summary>
public class FieldMappingProcessor : IFieldMappingProcessor
{
    private readonly IPermissionEvaluator? _permissionEvaluator;
    private readonly ILogger<FieldMappingProcessor> _logger;

    public FieldMappingProcessor(
        IPermissionEvaluator? permissionEvaluator,
        ILogger<FieldMappingProcessor> logger)
    {
        _permissionEvaluator = permissionEvaluator;
        _logger = logger;
    }

    /// <summary>
    /// Apply field mappings to transform source entity to target schema
    /// </summary>
    public async Task<Dictionary<string, object?>> ApplyMappingsAsync(
        string entityType,
        object sourceEntity,
        List<FieldMapping> mappings,
        PermissionEvaluationContext? context = null)
    {
        var result = new Dictionary<string, object?>();
        var sourceDict = ConvertToDict(sourceEntity);

        // Sort by priority
        var sortedMappings = mappings
            .Where(m => m.IsActive)
            .OrderBy(m => m.Priority)
            .ToList();

        foreach (var mapping in sortedMappings)
        {
            try
            {
                var value = await ApplySingleMappingAsync(mapping, sourceDict, context);
                result[mapping.TargetFieldName] = value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Error applying mapping from {Source} to {Target} using {Type}", 
                    mapping.SourceFieldName, mapping.TargetFieldName, mapping.MappingType);
                throw;
            }
        }

        return result;
    }

    /// <summary>
    /// Apply a single field mapping
    /// </summary>
    private async Task<object?> ApplySingleMappingAsync(
        FieldMapping mapping,
        Dictionary<string, object?> sourceData,
        PermissionEvaluationContext? context)
    {
        return mapping.MappingType.ToLower() switch
        {
            "direct" => ApplyDirectMapping(mapping, sourceData),
            "constant" => ApplyConstantMapping(mapping),
            "computed" => ApplyComputedMapping(mapping, sourceData, context),
            "rule" => await ApplyRuleMapping(mapping, sourceData, context),
            _ => throw new InvalidOperationException($"Unknown mapping type: {mapping.MappingType}")
        };
    }

    /// <summary>
    /// Direct mapping: copy source field to target field as-is
    /// </summary>
    private object? ApplyDirectMapping(FieldMapping mapping, Dictionary<string, object?> sourceData)
    {
        if (!sourceData.TryGetValue(mapping.SourceFieldName, out var value))
        {
            _logger.LogWarning("Source field not found: {SourceField}", mapping.SourceFieldName);
            return null;
        }

        // Convert value to target type if specified
        return ConvertValue(value, mapping.TargetDataType);
    }

    /// <summary>
    /// Constant mapping: return a constant value
    /// </summary>
    private object? ApplyConstantMapping(FieldMapping mapping)
    {
        if (string.IsNullOrEmpty(mapping.ConstantValue))
            return null;

        // Try to parse as JSON first for complex types
        try
        {
            return JsonSerializer.Deserialize<object>(mapping.ConstantValue);
        }
        catch
        {
            // Return as string if not valid JSON
            return mapping.ConstantValue;
        }
    }

    /// <summary>
    /// Computed mapping: evaluate Cedar DSL expression to compute value
    /// </summary>
    private object? ApplyComputedMapping(
        FieldMapping mapping,
        Dictionary<string, object?> sourceData,
        PermissionEvaluationContext? context)
    {
        if (string.IsNullOrEmpty(mapping.TransformationExpression))
            throw new InvalidOperationException("Computed mapping requires TransformationExpression");

        try
        {
            // For computed mappings, we use a simple Cedar-like expression evaluator
            // This could be enhanced to use the full permission evaluator if needed
            var result = EvaluateCedarExpression(mapping.TransformationExpression, sourceData, context);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating Cedar DSL expression: {Expression}", mapping.TransformationExpression);
            throw;
        }
    }

    /// <summary>
    /// Simple Cedar-like expression evaluator for field mapping computations
    /// </summary>
    private object? EvaluateCedarExpression(
        string expression,
        Dictionary<string, object?> sourceData,
        PermissionEvaluationContext? context)
    {
        try
        {
            // Replace principal.* references with values from sourceData
            var evaluatedExpr = expression;
            foreach (var kvp in sourceData)
            {
                var placeholder = $"principal.{kvp.Key}";
                var value = kvp.Value?.ToString() ?? "null";
                evaluatedExpr = evaluatedExpr.Replace(placeholder, $"\"{value}\"", StringComparison.OrdinalIgnoreCase);
            }

            // Add support for basic string concatenation (++)
            if (evaluatedExpr.Contains("++"))
            {
                // Simple concatenation: "value1" ++ "value2"
                evaluatedExpr = evaluatedExpr.Replace("++", "+");
            }

            // Add support for toUpperCase/toLowerCase
            evaluatedExpr = EvaluateStringMethods(evaluatedExpr);

            // For now, return the expression as a result
            // In a full implementation, this would use a proper expression evaluator
            return evaluatedExpr;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Cedar expression evaluation");
            return expression; // Return original expression on error
        }
    }

    /// <summary>
    /// Evaluate string methods like toUpperCase, toLowerCase
    /// </summary>
    private string EvaluateStringMethods(string expression)
    {
        // Handle .toUpperCase()
        var regex = new System.Text.RegularExpressions.Regex(@"([""'])([^""']+)\1\s*\.toUpperCase\(\)");
        expression = regex.Replace(expression, m => $"\"{m.Groups[2].Value.ToUpper()}\"");

        // Handle .toLowerCase()
        regex = new System.Text.RegularExpressions.Regex(@"([""'])([^""']+)\1\s*\.toLowerCase\(\)");
        expression = regex.Replace(expression, m => $"\"{m.Groups[2].Value.ToLower()}\"");

        return expression;
    }

    /// <summary>
    /// Rule mapping: apply a transformation rule to compute value
    /// </summary>
    private async Task<object?> ApplyRuleMapping(
        FieldMapping mapping,
        Dictionary<string, object?> sourceData,
        PermissionEvaluationContext? context)
    {
        if (string.IsNullOrEmpty(mapping.TransformationExpression))
        {
            _logger.LogWarning("Rule mapping requires TransformationExpression");
            return sourceData.TryGetValue(mapping.SourceFieldName, out var value) ? value : null;
        }

        // For rule mappings, we can reference transformations by name or ID
        // For now, treat similar to computed - could be enhanced to reference stored rules
        return ApplyComputedMapping(mapping, sourceData, context);
    }

    /// <summary>
    /// Convert an object to a dictionary for easier field access
    /// </summary>
    private Dictionary<string, object?> ConvertToDict(object entity)
    {
        if (entity is Dictionary<string, object?> dict)
            return dict;

        if (entity is JsonElement jsonElement)
            return JsonElementToDict(jsonElement);

        // Use reflection to get properties
        var dict2 = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in entity.GetType().GetProperties())
        {
            dict2[prop.Name] = prop.GetValue(entity);
        }
        return dict2;
    }

    /// <summary>
    /// Convert JsonElement to Dictionary
    /// </summary>
    private Dictionary<string, object?> JsonElementToDict(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();

        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => property.Value.TryGetInt32(out var i) ? (object)i : (object)property.Value.GetDouble(),
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Array => property.Value.EnumerateArray().ToList(),
                JsonValueKind.Object => JsonElementToDict(property.Value),
                _ => property.Value.GetRawText()
            };
        }

        return dict;
    }

    /// <summary>
    /// Convert value to target data type
    /// </summary>
    private object? ConvertValue(object? value, string? targetType)
    {
        if (value == null || string.IsNullOrEmpty(targetType))
            return value;

        try
        {
            return targetType.ToLower() switch
            {
                "int" or "integer" => Convert.ToInt32(value),
                "long" => Convert.ToInt64(value),
                "double" or "float" => Convert.ToDouble(value),
                "bool" or "boolean" => Convert.ToBoolean(value),
                "string" => value.ToString() ?? "",
                "datetime" => Convert.ToDateTime(value),
                "uuid" or "guid" => Guid.Parse(value.ToString() ?? ""),
                _ => value
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert value to type {TargetType}", targetType);
            return value;
        }
    }
}
