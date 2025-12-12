using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.Evaluators.Conditions;

/// <summary>
/// Interface for condition evaluators
/// </summary>
public interface IConditionEvaluator
{
    /// <summary>
    /// Condition type this evaluator handles
    /// </summary>
    string ConditionType { get; }

    /// <summary>
    /// Evaluate the condition
    /// </summary>
    /// <param name="condition">Condition configuration</param>
    /// <param name="context">Evaluation context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Evaluation result</returns>
    Task<ConditionEvaluationResult> EvaluateAsync(
        ConditionConfig condition,
        PermissionEvaluationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate condition configuration
    /// </summary>
    /// <param name="condition">Condition to validate</param>
    /// <returns>Validation errors if any</returns>
    IEnumerable<string> Validate(ConditionConfig condition);
}

/// <summary>
/// Base class for condition evaluators
/// </summary>
public abstract class ConditionEvaluatorBase : IConditionEvaluator
{
    public abstract string ConditionType { get; }

    public abstract Task<ConditionEvaluationResult> EvaluateAsync(
        ConditionConfig condition,
        PermissionEvaluationContext context,
        CancellationToken cancellationToken = default);

    public virtual IEnumerable<string> Validate(ConditionConfig condition)
    {
        return Enumerable.Empty<string>();
    }

    /// <summary>
    /// Create a passed result
    /// </summary>
    protected ConditionEvaluationResult Passed(string? reason = null, Dictionary<string, object>? details = null)
    {
        return new ConditionEvaluationResult
        {
            ConditionType = ConditionType,
            Passed = true,
            Reason = reason ?? "Condition passed",
            Details = details
        };
    }

    /// <summary>
    /// Create a failed result
    /// </summary>
    protected ConditionEvaluationResult Failed(string reason, Dictionary<string, object>? details = null)
    {
        return new ConditionEvaluationResult
        {
            ConditionType = ConditionType,
            Passed = false,
            Reason = reason,
            Details = details
        };
    }
}
