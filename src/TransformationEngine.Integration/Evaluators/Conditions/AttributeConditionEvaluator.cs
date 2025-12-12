using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.Evaluators.Conditions;

/// <summary>
/// Evaluates attribute-based conditions (ABAC expressions)
/// Supports Cedar-like expressions for principal, resource, and context attributes
/// </summary>
public class AttributeConditionEvaluator : ConditionEvaluatorBase
{
    private readonly ILogger<AttributeConditionEvaluator> _logger;

    public override string ConditionType => "Attribute";

    public AttributeConditionEvaluator(ILogger<AttributeConditionEvaluator> logger)
    {
        _logger = logger;
    }

    public override Task<ConditionEvaluationResult> EvaluateAsync(
        ConditionConfig condition,
        PermissionEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var expression = condition.Expression;
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult(Failed("No attribute expression provided"));
        }

        try
        {
            var result = EvaluateExpression(expression, context);
            return Task.FromResult(result
                ? Passed($"Expression '{expression}' evaluated to true")
                : Failed($"Expression '{expression}' evaluated to false"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating expression: {Expression}", expression);
            return Task.FromResult(Failed($"Error evaluating expression: {ex.Message}"));
        }
    }

    /// <summary>
    /// Evaluate a Cedar-like expression
    /// Supports: principal.*, resource.*, context.*, ==, !=, in, &&, ||, >=, <=, >, <
    /// </summary>
    private bool EvaluateExpression(string expression, PermissionEvaluationContext context)
    {
        // Handle compound expressions (&&, ||)
        if (expression.Contains("&&"))
        {
            var parts = SplitOnOperator(expression, "&&");
            return parts.All(p => EvaluateExpression(p.Trim(), context));
        }

        if (expression.Contains("||"))
        {
            var parts = SplitOnOperator(expression, "||");
            return parts.Any(p => EvaluateExpression(p.Trim(), context));
        }

        // Handle negation
        expression = expression.Trim();
        if (expression.StartsWith("!") || expression.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
        {
            var inner = expression.StartsWith("!") ? expression[1..].Trim() : expression[4..].Trim();
            return !EvaluateExpression(inner, context);
        }

        // Handle parentheses
        if (expression.StartsWith("(") && expression.EndsWith(")"))
        {
            return EvaluateExpression(expression[1..^1], context);
        }

        // Handle comparison operators
        return EvaluateComparison(expression, context);
    }

    private bool EvaluateComparison(string expression, PermissionEvaluationContext context)
    {
        // Try different operators in order of specificity
        var operators = new[] { "==", "!=", ">=", "<=", ">", "<", " in ", " contains " };

        foreach (var op in operators)
        {
            var index = expression.IndexOf(op, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                var leftExpr = expression[..index].Trim();
                var rightExpr = expression[(index + op.Length)..].Trim();

                var leftValue = ResolveValue(leftExpr, context);
                var rightValue = ResolveValue(rightExpr, context);

                return op.Trim() switch
                {
                    "==" => AreEqual(leftValue, rightValue),
                    "!=" => !AreEqual(leftValue, rightValue),
                    ">=" => Compare(leftValue, rightValue) >= 0,
                    "<=" => Compare(leftValue, rightValue) <= 0,
                    ">" => Compare(leftValue, rightValue) > 0,
                    "<" => Compare(leftValue, rightValue) < 0,
                    "in" => IsIn(leftValue, rightValue),
                    "contains" => Contains(leftValue, rightValue),
                    _ => throw new InvalidOperationException($"Unknown operator: {op}")
                };
            }
        }

        // If no operator found, try to evaluate as boolean value
        var value = ResolveValue(expression, context);
        return value is bool b ? b : value != null;
    }

    private object? ResolveValue(string expression, PermissionEvaluationContext context)
    {
        expression = expression.Trim();

        // String literal
        if ((expression.StartsWith("\"") && expression.EndsWith("\"")) ||
            (expression.StartsWith("'") && expression.EndsWith("'")))
        {
            return expression[1..^1];
        }

        // Array literal [value1, value2, ...]
        if (expression.StartsWith("[") && expression.EndsWith("]"))
        {
            var inner = expression[1..^1];
            var elements = inner.Split(',')
                .Select(e => ResolveValue(e.Trim(), context))
                .ToList();
            return elements;
        }

        // Numeric literal
        if (int.TryParse(expression, out var intVal))
            return intVal;
        if (decimal.TryParse(expression, out var decVal))
            return decVal;

        // Boolean literal
        if (bool.TryParse(expression, out var boolVal))
            return boolVal;

        // Null literal
        if (expression.Equals("null", StringComparison.OrdinalIgnoreCase))
            return null;

        // Attribute path (principal.department, resource.classification, context.ip, etc.)
        return ResolveAttributePath(expression, context);
    }

    private object? ResolveAttributePath(string path, PermissionEvaluationContext context)
    {
        var parts = path.Split('.');
        if (parts.Length < 2)
        {
            _logger.LogWarning("Invalid attribute path: {Path}", path);
            return null;
        }

        var root = parts[0].ToLowerInvariant();
        var attributePath = string.Join(".", parts.Skip(1));

        Dictionary<string, object>? attributes = root switch
        {
            "principal" => GetPrincipalAttributes(context, attributePath),
            "resource" => GetResourceAttributes(context, attributePath),
            "context" => context.Attributes,
            _ => null
        };

        if (attributes == null)
        {
            _logger.LogDebug("Unknown attribute root: {Root}", root);
            return null;
        }

        return GetNestedValue(attributes, attributePath);
    }

    private Dictionary<string, object>? GetPrincipalAttributes(PermissionEvaluationContext context, string attributePath)
    {
        // Build combined attributes including built-in fields
        var attrs = new Dictionary<string, object>(context.Principal.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = context.Principal.Id,
            ["type"] = context.Principal.Type,
            ["roleIds"] = context.Principal.RoleIds,
            ["groupIds"] = context.Principal.GroupIds
        };
        return attrs;
    }

    private Dictionary<string, object>? GetResourceAttributes(PermissionEvaluationContext context, string attributePath)
    {
        var attrs = new Dictionary<string, object>(context.Resource.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = context.Resource.Id,
            ["type"] = context.Resource.Type
        };

        if (context.Resource.OwnerId.HasValue)
            attrs["ownerId"] = context.Resource.OwnerId.Value;
        if (!string.IsNullOrEmpty(context.Resource.Classification))
            attrs["classification"] = context.Resource.Classification;

        return attrs;
    }

    private object? GetNestedValue(Dictionary<string, object> dict, string path)
    {
        var parts = path.Split('.');
        object? current = dict;

        foreach (var part in parts)
        {
            if (current is Dictionary<string, object> d)
            {
                // Case-insensitive lookup
                var key = d.Keys.FirstOrDefault(k => k.Equals(part, StringComparison.OrdinalIgnoreCase));
                if (key == null)
                {
                    _logger.LogDebug("Attribute not found: {Part} in path {Path}", part, path);
                    return null;
                }
                current = d[key];
            }
            else if (current is IDictionary<string, object> id)
            {
                var key = id.Keys.FirstOrDefault(k => k.Equals(part, StringComparison.OrdinalIgnoreCase));
                if (key == null)
                    return null;
                current = id[key];
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private static bool AreEqual(object? left, object? right)
    {
        if (left == null && right == null) return true;
        if (left == null || right == null) return false;

        // Handle numeric comparison
        if (IsNumeric(left) && IsNumeric(right))
        {
            return Convert.ToDecimal(left) == Convert.ToDecimal(right);
        }

        // String comparison (case-insensitive)
        if (left is string ls && right is string rs)
        {
            return ls.Equals(rs, StringComparison.OrdinalIgnoreCase);
        }

        return left.Equals(right);
    }

    private static int Compare(object? left, object? right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;

        if (IsNumeric(left) && IsNumeric(right))
        {
            return Convert.ToDecimal(left).CompareTo(Convert.ToDecimal(right));
        }

        if (left is string ls && right is string rs)
        {
            return string.Compare(ls, rs, StringComparison.OrdinalIgnoreCase);
        }

        if (left is IComparable lc)
        {
            return lc.CompareTo(right);
        }

        throw new InvalidOperationException($"Cannot compare {left.GetType()} and {right.GetType()}");
    }

    private static bool IsIn(object? item, object? collection)
    {
        if (item == null || collection == null) return false;

        if (collection is IEnumerable<object> enumerable)
        {
            return enumerable.Any(e => AreEqual(item, e));
        }

        if (collection is string s && item is string itemStr)
        {
            return s.Contains(itemStr, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool Contains(object? collection, object? item)
    {
        return IsIn(item, collection);
    }

    private static bool IsNumeric(object? value)
    {
        return value is int or long or float or double or decimal or short or byte;
    }

    private static List<string> SplitOnOperator(string expression, string op)
    {
        var result = new List<string>();
        var depth = 0;
        var current = "";

        for (int i = 0; i < expression.Length; i++)
        {
            var c = expression[i];

            if (c == '(' || c == '[') depth++;
            else if (c == ')' || c == ']') depth--;

            if (depth == 0 && expression[i..].StartsWith(op))
            {
                result.Add(current);
                current = "";
                i += op.Length - 1;
            }
            else
            {
                current += c;
            }
        }

        if (!string.IsNullOrEmpty(current))
            result.Add(current);

        return result;
    }

    public override IEnumerable<string> Validate(ConditionConfig condition)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(condition.Expression))
        {
            errors.Add("Attribute expression is required");
            return errors;
        }

        // Basic syntax validation
        var expression = condition.Expression;

        // Check for balanced parentheses
        var parenCount = expression.Count(c => c == '(') - expression.Count(c => c == ')');
        if (parenCount != 0)
        {
            errors.Add("Unbalanced parentheses in expression");
        }

        // Check for balanced brackets
        var bracketCount = expression.Count(c => c == '[') - expression.Count(c => c == ']');
        if (bracketCount != 0)
        {
            errors.Add("Unbalanced brackets in expression");
        }

        // Check for valid attribute references
        var attributePattern = new Regex(@"\b(principal|resource|context)\.\w+");
        if (!attributePattern.IsMatch(expression))
        {
            // May not be an error if using literals, but worth a warning
            _logger.LogDebug("Expression doesn't contain standard attribute paths: {Expression}", expression);
        }

        return errors;
    }
}
