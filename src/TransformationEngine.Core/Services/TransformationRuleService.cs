using Microsoft.Extensions.Logging;
using TransformationEngine.Core;
using TransformationEngine.Storage;

namespace TransformationEngine.Services;

/// <summary>
/// Service for managing transformation rules with CRUD operations
/// </summary>
public interface ITransformationRuleService
{
    Task<List<TransformationRule>> GetRulesAsync(string eventType);
    Task<TransformationRule?> GetRuleAsync(string eventType, string ruleId);
    Task<TransformationRule> CreateRuleAsync(string eventType, TransformationRule rule);
    Task<TransformationRule> UpdateRuleAsync(string eventType, string ruleId, TransformationRule rule);
    Task<bool> DeleteRuleAsync(string eventType, string ruleId);
    Task<List<string>> GetEventTypesAsync();
    Task<ITransformationEngine<T>> GetOrLoadEngineAsync<T>();
}

/// <summary>
/// Implementation of transformation rule service
/// </summary>
public class TransformationRuleService : ITransformationRuleService
{
    private readonly ITransformationRuleRepository _repository;
    private readonly DynamicTransformationRuleLoader _loader;
    private readonly ILogger<TransformationRuleService>? _logger;

    public TransformationRuleService(
        ITransformationRuleRepository repository,
        DynamicTransformationRuleLoader loader,
        ILogger<TransformationRuleService>? logger = null)
    {
        _repository = repository;
        _loader = loader;
        _logger = logger;
    }

    public Task<List<TransformationRule>> GetRulesAsync(string eventType)
    {
        return _repository.GetRulesAsync(eventType);
    }

    public Task<TransformationRule?> GetRuleAsync(string eventType, string ruleId)
    {
        return _repository.GetRuleAsync(eventType, ruleId);
    }

    public async Task<TransformationRule> CreateRuleAsync(string eventType, TransformationRule rule)
    {
        var created = await _repository.CreateRuleAsync(eventType, rule);
        
        // Clear cache to force reload
        _loader.ClearCache(eventType);
        
        _logger?.LogInformation("Created transformation rule: {EventType}/{RuleId}", eventType, created.Id);
        return created;
    }

    public async Task<TransformationRule> UpdateRuleAsync(string eventType, string ruleId, TransformationRule rule)
    {
        var updated = await _repository.UpdateRuleAsync(eventType, ruleId, rule);
        
        // Clear cache to force reload
        _loader.ClearCache(eventType);
        
        _logger?.LogInformation("Updated transformation rule: {EventType}/{RuleId}", eventType, ruleId);
        return updated;
    }

    public async Task<bool> DeleteRuleAsync(string eventType, string ruleId)
    {
        var deleted = await _repository.DeleteRuleAsync(eventType, ruleId);
        
        if (deleted)
        {
            // Clear cache to force reload
            _loader.ClearCache(eventType);
            _logger?.LogInformation("Deleted transformation rule: {EventType}/{RuleId}", eventType, ruleId);
        }
        
        return deleted;
    }

    public Task<List<string>> GetEventTypesAsync()
    {
        return _repository.GetEventTypesAsync();
    }

    public async Task<ITransformationEngine<T>> GetOrLoadEngineAsync<T>()
    {
        return await _loader.LoadEngineAsync<T>();
    }
}

