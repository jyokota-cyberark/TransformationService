using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Evaluators.Conditions;
using TransformationEngine.Integration.Models;
using TransformationEngine.Integration.Parser;

namespace TransformationEngine.Integration.Evaluators;

/// <summary>
/// Evaluator for Permission rule type
/// Computes effective permissions based on Cedar-like policies and conditions
/// </summary>
public class PermissionEvaluator : IPermissionEvaluator
{
    private readonly ILogger<PermissionEvaluator> _logger;
    private readonly IConditionEvaluatorRegistry _conditionRegistry;
    private readonly CedarLikeParser _policyParser;
    private readonly AttributeConditionEvaluator _attributeEvaluator;

    public string RuleType => "Permission";

    public PermissionEvaluator(
        ILogger<PermissionEvaluator> logger,
        IConditionEvaluatorRegistry conditionRegistry,
        ILogger<AttributeConditionEvaluator> attributeLogger)
    {
        _logger = logger;
        _conditionRegistry = conditionRegistry;
        _policyParser = new CedarLikeParser();
        _attributeEvaluator = new AttributeConditionEvaluator(attributeLogger);
    }

    /// <summary>
    /// Evaluate a permission rule and return the decision
    /// </summary>
    public async Task<PermissionDecision> EvaluateAsync(
        TransformationRule rule,
        Dictionary<string, object?> entityData,
        PermissionEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var config = rule.GetConfigurationAs<PermissionRuleConfig>();
            if (config == null)
            {
                return PermissionDecision.Deny("Invalid permission rule configuration");
            }

            _logger.LogDebug("Evaluating permission rule '{RuleName}' for principal {PrincipalId}",
                rule.RuleName, context.Principal.Id);

            // Evaluate conditions first
            var conditionResults = await _conditionRegistry.EvaluateAllAsync(
                config.Conditions, context, cancellationToken);

            // Check if all conditions passed
            var failedConditions = conditionResults.Where(c => !c.Passed).ToList();
            if (failedConditions.Count > 0)
            {
                var decision = PermissionDecision.Deny(
                    $"Condition(s) not met: {string.Join(", ", failedConditions.Select(c => c.Reason))}");
                decision.ConditionsEvaluated = conditionResults;
                decision.AppliedRuleNames.Add(rule.RuleName);
                return decision;
            }

            // Parse and evaluate policy DSL if present
            if (!string.IsNullOrWhiteSpace(config.PolicyDsl))
            {
                var policyDecision = EvaluatePolicy(config.PolicyDsl, context, config);
                policyDecision.ConditionsEvaluated = conditionResults;
                policyDecision.AppliedPolicyIds.Add(rule.Id);
                policyDecision.AppliedRuleNames.Add(rule.RuleName);
                return policyDecision;
            }

            // If no policy DSL, grant all configured actions
            var decision2 = PermissionDecision.Allow(config.Actions, "All conditions passed");
            decision2.ConditionsEvaluated = conditionResults;
            decision2.AppliedPolicyIds.Add(rule.Id);
            decision2.AppliedRuleNames.Add(rule.RuleName);
            return decision2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating permission rule '{RuleName}'", rule.RuleName);
            return PermissionDecision.Deny($"Error evaluating permission: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug("Permission evaluation completed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Evaluate multiple rules and combine decisions
    /// </summary>
    public async Task<PermissionDecision> EvaluateMultipleAsync(
        IEnumerable<TransformationRule> rules,
        Dictionary<string, object?> entityData,
        PermissionEvaluationContext context,
        PermissionCombinationMode combinationMode = PermissionCombinationMode.DenyOverrides,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var decisions = new List<(TransformationRule Rule, PermissionDecision Decision)>();

        // Evaluate all rules
        foreach (var rule in rules.OrderByDescending(r => r.Priority))
        {
            if (rule.RuleType != RuleType || !rule.IsActive)
                continue;

            var decision = await EvaluateAsync(rule, entityData, context, cancellationToken);
            decisions.Add((rule, decision));
        }

        // Combine decisions based on mode
        var finalDecision = CombineDecisions(decisions, combinationMode);
        finalDecision.EvaluationTimeMs = stopwatch.ElapsedMilliseconds;

        return finalDecision;
    }

    private PermissionDecision EvaluatePolicy(
        string policyDsl,
        PermissionEvaluationContext context,
        PermissionRuleConfig config)
    {
        try
        {
            var policy = _policyParser.Parse(policyDsl);

            // Check if policy matches the context
            var matches = policy.Matches(context, (expr, ctx) =>
            {
                var conditionConfig = new ConditionConfig { Type = "Attribute", Expression = expr };
                var result = _attributeEvaluator.EvaluateAsync(conditionConfig, ctx).Result;
                return result.Passed;
            });

            if (!matches)
            {
                return PermissionDecision.NotApplicable("Policy constraints not matched");
            }

            // Policy matches - return its effect
            if (policy.Effect == PermissionEffect.Allow)
            {
                // Determine which actions are permitted
                var actions = config.Actions;
                if (policy.ActionConstraint?.AllowedValues != null)
                {
                    actions = policy.ActionConstraint.AllowedValues;
                }
                return PermissionDecision.Allow(actions, "Policy matched and permits access");
            }
            else
            {
                return PermissionDecision.Deny("Policy explicitly denies access");
            }
        }
        catch (PolicyParseException ex)
        {
            _logger.LogError(ex, "Error parsing policy DSL");
            return PermissionDecision.Deny($"Policy parse error: {ex.Message}");
        }
    }

    private PermissionDecision CombineDecisions(
        List<(TransformationRule Rule, PermissionDecision Decision)> decisions,
        PermissionCombinationMode mode)
    {
        if (decisions.Count == 0)
        {
            return PermissionDecision.NotApplicable("No applicable permission rules");
        }

        var allRuleNames = decisions.Select(d => d.Rule.RuleName).ToList();
        var allPolicyIds = decisions.Select(d => d.Rule.Id).ToList();
        var allConditions = decisions.SelectMany(d => d.Decision.ConditionsEvaluated).ToList();

        switch (mode)
        {
            case PermissionCombinationMode.DenyOverrides:
                // Any deny overrides all permits
                var denyDecision = decisions.FirstOrDefault(d => d.Decision.Effect == PermissionEffect.Deny);
                if (denyDecision.Decision != null)
                {
                    var result = PermissionDecision.Deny(denyDecision.Decision.Reason ?? "Explicitly denied");
                    result.AppliedRuleNames = allRuleNames;
                    result.AppliedPolicyIds = allPolicyIds;
                    result.ConditionsEvaluated = allConditions;
                    return result;
                }
                // Combine all permits
                var allowDecisions = decisions.Where(d => d.Decision.Effect == PermissionEffect.Allow).ToList();
                if (allowDecisions.Count > 0)
                {
                    var allPermissions = allowDecisions
                        .SelectMany(d => d.Decision.EffectivePermissions)
                        .Distinct()
                        .ToList();
                    var result = PermissionDecision.Allow(allPermissions, "Combined permissions from all matching policies");
                    result.AppliedRuleNames = allRuleNames;
                    result.AppliedPolicyIds = allPolicyIds;
                    result.ConditionsEvaluated = allConditions;
                    return result;
                }
                break;

            case PermissionCombinationMode.FirstApplicable:
                // First matching policy wins
                var first = decisions.FirstOrDefault(d => d.Decision.Effect != PermissionEffect.NotApplicable);
                if (first.Decision != null)
                {
                    first.Decision.AppliedRuleNames = new List<string> { first.Rule.RuleName };
                    first.Decision.AppliedPolicyIds = new List<int> { first.Rule.Id };
                    return first.Decision;
                }
                break;

            case PermissionCombinationMode.OnlyOneApplicable:
                // Error if multiple policies apply
                var applicable = decisions.Where(d => d.Decision.Effect != PermissionEffect.NotApplicable).ToList();
                if (applicable.Count > 1)
                {
                    return PermissionDecision.Deny(
                        $"Multiple policies apply ({applicable.Count}) but OnlyOneApplicable mode is set");
                }
                if (applicable.Count == 1)
                {
                    return applicable[0].Decision;
                }
                break;

            case PermissionCombinationMode.PermitOverrides:
                // Any permit wins (unless explicit deny on same resource)
                var permitDecision = decisions.FirstOrDefault(d => d.Decision.Effect == PermissionEffect.Allow);
                if (permitDecision.Decision != null)
                {
                    var result = permitDecision.Decision;
                    result.AppliedRuleNames = allRuleNames;
                    result.AppliedPolicyIds = allPolicyIds;
                    result.ConditionsEvaluated = allConditions;
                    return result;
                }
                // Fall through to deny
                var denyResult = decisions.FirstOrDefault(d => d.Decision.Effect == PermissionEffect.Deny);
                if (denyResult.Decision != null)
                {
                    return denyResult.Decision;
                }
                break;
        }

        return PermissionDecision.NotApplicable("No applicable policy decision");
    }

    /// <summary>
    /// Validate a permission rule configuration
    /// </summary>
    public PolicyValidationResult ValidateRule(TransformationRule rule)
    {
        var result = new PolicyValidationResult { IsValid = true };

        var config = rule.GetConfigurationAs<PermissionRuleConfig>();
        if (config == null)
        {
            result.IsValid = false;
            result.Errors.Add("Cannot parse permission rule configuration");
            return result;
        }

        // Validate policy DSL
        if (!string.IsNullOrWhiteSpace(config.PolicyDsl))
        {
            var policyResult = _policyParser.Validate(config.PolicyDsl);
            if (!policyResult.IsValid)
            {
                result.IsValid = false;
                result.Errors.AddRange(policyResult.Errors);
            }
            result.Warnings.AddRange(policyResult.Warnings);
        }

        // Validate conditions
        foreach (var condition in config.Conditions)
        {
            var evaluator = _conditionRegistry.GetEvaluator(condition.Type);
            if (evaluator == null)
            {
                result.IsValid = false;
                result.Errors.Add($"Unknown condition type: {condition.Type}");
                continue;
            }

            var conditionErrors = evaluator.Validate(condition);
            result.Errors.AddRange(conditionErrors);
            if (conditionErrors.Any())
            {
                result.IsValid = false;
            }
        }

        // Validate simulation settings
        if (config.SimulationMode && (config.SimulationSamplingRate < 0 || config.SimulationSamplingRate > 1))
        {
            result.IsValid = false;
            result.Errors.Add("SimulationSamplingRate must be between 0 and 1");
        }

        return result;
    }
}

/// <summary>
/// Interface for permission evaluator
/// </summary>
public interface IPermissionEvaluator
{
    /// <summary>
    /// Rule type this evaluator handles
    /// </summary>
    string RuleType { get; }

    /// <summary>
    /// Evaluate a single permission rule
    /// </summary>
    Task<PermissionDecision> EvaluateAsync(
        TransformationRule rule,
        Dictionary<string, object?> entityData,
        PermissionEvaluationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluate multiple rules and combine decisions
    /// </summary>
    Task<PermissionDecision> EvaluateMultipleAsync(
        IEnumerable<TransformationRule> rules,
        Dictionary<string, object?> entityData,
        PermissionEvaluationContext context,
        PermissionCombinationMode combinationMode = PermissionCombinationMode.DenyOverrides,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate a permission rule
    /// </summary>
    PolicyValidationResult ValidateRule(TransformationRule rule);
}
