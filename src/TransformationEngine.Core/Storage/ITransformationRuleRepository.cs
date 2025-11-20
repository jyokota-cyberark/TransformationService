namespace TransformationEngine.Storage;

/// <summary>
/// Repository interface for managing transformation rules
/// </summary>
public interface ITransformationRuleRepository
{
    /// <summary>
    /// Gets all transformation rules for a specific event type
    /// </summary>
    Task<List<TransformationRule>> GetRulesAsync(string eventType);

    /// <summary>
    /// Gets a specific transformation rule by ID
    /// </summary>
    Task<TransformationRule?> GetRuleAsync(string eventType, string ruleId);

    /// <summary>
    /// Creates a new transformation rule
    /// </summary>
    Task<TransformationRule> CreateRuleAsync(string eventType, TransformationRule rule);

    /// <summary>
    /// Updates an existing transformation rule
    /// </summary>
    Task<TransformationRule> UpdateRuleAsync(string eventType, string ruleId, TransformationRule rule);

    /// <summary>
    /// Deletes a transformation rule
    /// </summary>
    Task<bool> DeleteRuleAsync(string eventType, string ruleId);

    /// <summary>
    /// Checks if a rule exists
    /// </summary>
    Task<bool> RuleExistsAsync(string eventType, string ruleId);

    /// <summary>
    /// Gets all event types that have rules
    /// </summary>
    Task<List<string>> GetEventTypesAsync();

    /// <summary>
    /// Event fired when rules are changed (for hot-reload)
    /// </summary>
    event EventHandler<TransformationRuleChangedEventArgs>? RuleChanged;
}

/// <summary>
/// Event arguments for rule change events
/// </summary>
public class TransformationRuleChangedEventArgs : EventArgs
{
    public string EventType { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public RuleChangeType ChangeType { get; set; }
}

/// <summary>
/// Type of rule change
/// </summary>
public enum RuleChangeType
{
    Created,
    Updated,
    Deleted
}

/// <summary>
/// Represents a transformation rule
/// </summary>
public class TransformationRule
{
    public string Id { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Order { get; set; }
    public string TransformerType { get; set; } = string.Empty;
    public Dictionary<string, object> Config { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

