using System.Text.RegularExpressions;
using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.Parser;

/// <summary>
/// Parser for Cedar-like policy DSL
/// Supports permit/deny statements with principal, action, resource constraints and when clauses
/// </summary>
public class CedarLikeParser
{
    /// <summary>
    /// Parse a Cedar-like policy DSL string
    /// </summary>
    /// <param name="policyDsl">The policy DSL string</param>
    /// <returns>Parsed policy</returns>
    public ParsedPolicy Parse(string policyDsl)
    {
        if (string.IsNullOrWhiteSpace(policyDsl))
        {
            throw new PolicyParseException("Policy DSL cannot be empty");
        }

        var policy = new ParsedPolicy();
        var normalized = NormalizeWhitespace(policyDsl);

        // Determine effect (permit or deny)
        if (normalized.StartsWith("permit", StringComparison.OrdinalIgnoreCase))
        {
            policy.Effect = PermissionEffect.Allow;
            normalized = normalized[6..].TrimStart();
        }
        else if (normalized.StartsWith("deny", StringComparison.OrdinalIgnoreCase))
        {
            policy.Effect = PermissionEffect.Deny;
            normalized = normalized[4..].TrimStart();
        }
        else
        {
            throw new PolicyParseException("Policy must start with 'permit' or 'deny'");
        }

        // Extract the main clause (inside parentheses) and when clause
        var (mainClause, whenClause) = ExtractClauses(normalized);

        // Parse the main clause
        ParseMainClause(mainClause, policy);

        // Parse when clause if present
        if (!string.IsNullOrWhiteSpace(whenClause))
        {
            policy.WhenCondition = whenClause.Trim();
        }

        return policy;
    }

    /// <summary>
    /// Validate a policy DSL string
    /// </summary>
    public PolicyValidationResult Validate(string policyDsl)
    {
        var result = new PolicyValidationResult { IsValid = true };

        if (string.IsNullOrWhiteSpace(policyDsl))
        {
            result.IsValid = false;
            result.Errors.Add("Policy DSL cannot be empty");
            return result;
        }

        try
        {
            var policy = Parse(policyDsl);

            // Additional semantic validation
            if (policy.PrincipalConstraint == null && policy.ActionConstraint == null && policy.ResourceConstraint == null)
            {
                result.Warnings.Add("Policy has no constraints - will apply to all principals, actions, and resources");
            }
        }
        catch (PolicyParseException ex)
        {
            result.IsValid = false;
            result.Errors.Add(ex.Message);
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Unexpected error: {ex.Message}");
        }

        return result;
    }

    private static string NormalizeWhitespace(string input)
    {
        // Replace multiple whitespace with single space
        return Regex.Replace(input.Trim(), @"\s+", " ");
    }

    private static (string mainClause, string? whenClause) ExtractClauses(string input)
    {
        // Look for pattern: (...) when {...}
        var depth = 0;
        var mainStart = -1;
        var mainEnd = -1;

        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == '(')
            {
                if (mainStart == -1) mainStart = i + 1;
                depth++;
            }
            else if (input[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    mainEnd = i;
                    break;
                }
            }
        }

        if (mainStart == -1 || mainEnd == -1)
        {
            throw new PolicyParseException("Could not find policy clause in parentheses");
        }

        var mainClause = input[mainStart..mainEnd];
        var remaining = input[(mainEnd + 1)..].Trim();

        // Check for when clause
        string? whenClause = null;
        if (remaining.StartsWith("when", StringComparison.OrdinalIgnoreCase))
        {
            remaining = remaining[4..].Trim();
            if (remaining.StartsWith("{") && remaining.EndsWith("}"))
            {
                whenClause = remaining[1..^1].Trim();
            }
            else
            {
                whenClause = remaining;
            }
        }

        return (mainClause, whenClause);
    }

    private static void ParseMainClause(string clause, ParsedPolicy policy)
    {
        // Split on commas (respecting nested structures)
        var parts = SplitRespectingNesting(clause, ',');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();

            if (trimmed.StartsWith("principal", StringComparison.OrdinalIgnoreCase))
            {
                policy.PrincipalConstraint = ParseConstraint(trimmed["principal".Length..].Trim());
            }
            else if (trimmed.StartsWith("action", StringComparison.OrdinalIgnoreCase))
            {
                policy.ActionConstraint = ParseConstraint(trimmed["action".Length..].Trim());
            }
            else if (trimmed.StartsWith("resource", StringComparison.OrdinalIgnoreCase))
            {
                policy.ResourceConstraint = ParseConstraint(trimmed["resource".Length..].Trim());
            }
        }
    }

    private static PolicyConstraint ParseConstraint(string constraint)
    {
        var result = new PolicyConstraint { RawExpression = constraint.Trim() };

        if (string.IsNullOrWhiteSpace(constraint))
        {
            return result;
        }

        // Handle "is Type" pattern
        var isMatch = Regex.Match(constraint, @"^\s*is\s+(\w+)", RegexOptions.IgnoreCase);
        if (isMatch.Success)
        {
            result.EntityType = isMatch.Groups[1].Value;
            constraint = constraint[isMatch.Length..].Trim();
        }

        // Handle "in [...]" pattern for multiple values
        var inMatch = Regex.Match(constraint, @"\bin\s*\[([^\]]+)\]", RegexOptions.IgnoreCase);
        if (inMatch.Success)
        {
            var values = inMatch.Groups[1].Value
                .Split(',')
                .Select(v => v.Trim().Trim('"', '\''))
                .ToList();
            result.AllowedValues = values;
        }

        // Handle "== value" pattern
        var eqMatch = Regex.Match(constraint, @"==\s*[""']?(\w+)[""']?", RegexOptions.IgnoreCase);
        if (eqMatch.Success)
        {
            result.AllowedValues = new List<string> { eqMatch.Groups[1].Value };
        }

        // Handle attribute expressions (e.g., principal.department == "Engineering")
        if (constraint.Contains("&&") || constraint.Contains("||") ||
            constraint.Contains("==") || constraint.Contains("!="))
        {
            result.AttributeExpression = constraint;
        }

        return result;
    }

    private static List<string> SplitRespectingNesting(string input, char delimiter)
    {
        var result = new List<string>();
        var current = "";
        var depth = 0;

        foreach (var c in input)
        {
            if (c == '(' || c == '[' || c == '{')
            {
                depth++;
                current += c;
            }
            else if (c == ')' || c == ']' || c == '}')
            {
                depth--;
                current += c;
            }
            else if (c == delimiter && depth == 0)
            {
                result.Add(current);
                current = "";
            }
            else
            {
                current += c;
            }
        }

        if (!string.IsNullOrEmpty(current))
        {
            result.Add(current);
        }

        return result;
    }
}

/// <summary>
/// Parsed policy structure
/// </summary>
public class ParsedPolicy
{
    /// <summary>
    /// Effect of the policy (Allow or Deny)
    /// </summary>
    public PermissionEffect Effect { get; set; } = PermissionEffect.Allow;

    /// <summary>
    /// Constraint on principal
    /// </summary>
    public PolicyConstraint? PrincipalConstraint { get; set; }

    /// <summary>
    /// Constraint on action
    /// </summary>
    public PolicyConstraint? ActionConstraint { get; set; }

    /// <summary>
    /// Constraint on resource
    /// </summary>
    public PolicyConstraint? ResourceConstraint { get; set; }

    /// <summary>
    /// When condition expression
    /// </summary>
    public string? WhenCondition { get; set; }

    /// <summary>
    /// Check if this policy applies to the given context
    /// </summary>
    public bool Matches(PermissionEvaluationContext context, Func<string, PermissionEvaluationContext, bool> expressionEvaluator)
    {
        // Check principal constraint
        if (PrincipalConstraint != null)
        {
            if (!MatchesConstraint(PrincipalConstraint, context.Principal.Type,
                context.Principal.Id.ToString(), context.Principal.Attributes, expressionEvaluator, context))
            {
                return false;
            }
        }

        // Check action constraint
        if (ActionConstraint != null)
        {
            if (ActionConstraint.AllowedValues != null &&
                !ActionConstraint.AllowedValues.Contains(context.Action, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check resource constraint
        if (ResourceConstraint != null)
        {
            if (!MatchesConstraint(ResourceConstraint, context.Resource.Type,
                context.Resource.Id, context.Resource.Attributes, expressionEvaluator, context))
            {
                return false;
            }
        }

        // Check when condition
        if (!string.IsNullOrWhiteSpace(WhenCondition))
        {
            if (!expressionEvaluator(WhenCondition, context))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesConstraint(
        PolicyConstraint constraint,
        string entityType,
        string entityId,
        Dictionary<string, object> attributes,
        Func<string, PermissionEvaluationContext, bool> expressionEvaluator,
        PermissionEvaluationContext context)
    {
        // Check entity type
        if (!string.IsNullOrEmpty(constraint.EntityType) &&
            !constraint.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check allowed values (for id matching)
        if (constraint.AllowedValues != null && constraint.AllowedValues.Count > 0)
        {
            if (!constraint.AllowedValues.Contains(entityId, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check attribute expression
        if (!string.IsNullOrEmpty(constraint.AttributeExpression))
        {
            if (!expressionEvaluator(constraint.AttributeExpression, context))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Constraint on a policy element (principal, action, or resource)
/// </summary>
public class PolicyConstraint
{
    /// <summary>
    /// Raw expression string
    /// </summary>
    public string RawExpression { get; set; } = string.Empty;

    /// <summary>
    /// Entity type constraint (e.g., "User", "Application")
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Allowed values (for "in" or "==" constraints)
    /// </summary>
    public List<string>? AllowedValues { get; set; }

    /// <summary>
    /// Attribute expression (e.g., "principal.department == 'Engineering'")
    /// </summary>
    public string? AttributeExpression { get; set; }
}

/// <summary>
/// Result of policy validation
/// </summary>
public class PolicyValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Exception thrown when policy parsing fails
/// </summary>
public class PolicyParseException : Exception
{
    public PolicyParseException(string message) : base(message) { }
    public PolicyParseException(string message, Exception innerException) : base(message, innerException) { }
}
