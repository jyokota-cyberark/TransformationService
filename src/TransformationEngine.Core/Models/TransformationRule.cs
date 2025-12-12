namespace TransformationEngine.Models;

/// <summary>
/// Defines a transformation rule for converting field values
/// </summary>
public class TransformationRule
{
    public int Id { get; set; }
    public int InventoryTypeId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty; // e.g., "Replace", "RegexReplace", "Format", "Lookup", "Custom"
    public string? SourcePattern { get; set; }
    public string? TargetPattern { get; set; }
    public string? LookupTableJson { get; set; } // For lookup-based transformations
    public string? CustomScript { get; set; } // For custom transformations
    public string? ScriptLanguage { get; set; } = "JavaScript"; // e.g., "JavaScript", "Python"
    public int Priority { get; set; } // Order of execution
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastModifiedDate { get; set; }
    
    // Versioning fields
    public int CurrentVersion { get; set; } = 1;
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    
    // Note: InventoryType navigation property removed to avoid circular dependency
    // The InventoryTypeId foreign key is sufficient for database relationships
}

/// <summary>
/// Result of applying a transformation rule
/// </summary>
public class TransformationResult
{
    public string FieldName { get; set; } = string.Empty;
    public object? OriginalValue { get; set; }
    public object? TransformedValue { get; set; }
    public List<string> RulesApplied { get; set; } = new();
    public bool WasTransformed { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request to perform a dry run or actual transformation
/// </summary>
public class TransformationRequest
{
    public int InventoryTypeId { get; set; }
    public List<int>? InventoryItemIds { get; set; } // Null for bulk (all items)
    public bool IsDryRun { get; set; } = true;
    public List<int>? RuleIds { get; set; } // Null to apply all active rules
}

/// <summary>
/// Response from a transformation operation
/// </summary>
public class TransformationResponse
{
    public bool Success { get; set; }
    public bool IsDryRun { get; set; }
    public int ItemsProcessed { get; set; }
    public int ItemsTransformed { get; set; }
    public List<ItemTransformationDetail> Details { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Details of transformation for a single item
/// </summary>
public class ItemTransformationDetail
{
    public int InventoryItemId { get; set; }
    public string SourceItemId { get; set; } = string.Empty;
    public Dictionary<string, object?> RawData { get; set; } = new();
    public Dictionary<string, object?> TransformedData { get; set; } = new();
    public List<TransformationResult> FieldTransformations { get; set; } = new();
}
