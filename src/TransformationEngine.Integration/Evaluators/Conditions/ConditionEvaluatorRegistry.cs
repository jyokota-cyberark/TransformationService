using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.Evaluators.Conditions;

/// <summary>
/// Registry for condition evaluators
/// </summary>
public interface IConditionEvaluatorRegistry
{
    /// <summary>
    /// Get evaluator for condition type
    /// </summary>
    IConditionEvaluator? GetEvaluator(string conditionType);

    /// <summary>
    /// Check if evaluator is registered
    /// </summary>
    bool IsRegistered(string conditionType);

    /// <summary>
    /// Get all registered condition types
    /// </summary>
    IEnumerable<string> GetRegisteredTypes();

    /// <summary>
    /// Evaluate all conditions
    /// </summary>
    Task<List<ConditionEvaluationResult>> EvaluateAllAsync(
        IEnumerable<ConditionConfig> conditions,
        PermissionEvaluationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of condition evaluator registry
/// </summary>
public class ConditionEvaluatorRegistry : IConditionEvaluatorRegistry
{
    private readonly Dictionary<string, IConditionEvaluator> _evaluators = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ConditionEvaluatorRegistry> _logger;

    public ConditionEvaluatorRegistry(
        ILogger<ConditionEvaluatorRegistry> logger,
        IEnumerable<IConditionEvaluator> evaluators)
    {
        _logger = logger;

        foreach (var evaluator in evaluators)
        {
            RegisterEvaluator(evaluator);
        }

        _logger.LogInformation("Registered {Count} condition evaluators: {Types}",
            _evaluators.Count, string.Join(", ", _evaluators.Keys));
    }

    /// <summary>
    /// Register an evaluator
    /// </summary>
    public void RegisterEvaluator(IConditionEvaluator evaluator)
    {
        _evaluators[evaluator.ConditionType] = evaluator;
        _logger.LogDebug("Registered condition evaluator: {Type}", evaluator.ConditionType);
    }

    public IConditionEvaluator? GetEvaluator(string conditionType)
    {
        return _evaluators.TryGetValue(conditionType, out var evaluator) ? evaluator : null;
    }

    public bool IsRegistered(string conditionType)
    {
        return _evaluators.ContainsKey(conditionType);
    }

    public IEnumerable<string> GetRegisteredTypes()
    {
        return _evaluators.Keys;
    }

    public async Task<List<ConditionEvaluationResult>> EvaluateAllAsync(
        IEnumerable<ConditionConfig> conditions,
        PermissionEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ConditionEvaluationResult>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        foreach (var condition in conditions)
        {
            var evaluator = GetEvaluator(condition.Type);
            if (evaluator == null)
            {
                _logger.LogWarning("No evaluator registered for condition type: {Type}", condition.Type);
                results.Add(new ConditionEvaluationResult
                {
                    ConditionType = condition.Type,
                    Passed = false,
                    Reason = $"No evaluator registered for condition type: {condition.Type}"
                });
                continue;
            }

            try
            {
                var conditionStopwatch = System.Diagnostics.Stopwatch.StartNew();
                var result = await evaluator.EvaluateAsync(condition, context, cancellationToken);
                conditionStopwatch.Stop();
                result.EvaluationTimeMs = conditionStopwatch.ElapsedMilliseconds;
                results.Add(result);

                _logger.LogDebug("Condition {Type} evaluated: {Passed} ({TimeMs}ms)",
                    condition.Type, result.Passed, result.EvaluationTimeMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating condition {Type}", condition.Type);
                results.Add(new ConditionEvaluationResult
                {
                    ConditionType = condition.Type,
                    Passed = false,
                    Reason = $"Error evaluating condition: {ex.Message}"
                });
            }
        }

        stopwatch.Stop();
        _logger.LogDebug("Evaluated {Count} conditions in {TimeMs}ms",
            results.Count, stopwatch.ElapsedMilliseconds);

        return results;
    }
}
