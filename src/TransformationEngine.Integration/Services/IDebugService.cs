namespace TransformationEngine.Integration.Services;

using TransformationEngine.Integration.Models;

/// <summary>
/// Service for debugging and testing transformations with dry-run capability
/// </summary>
public interface IDebugService
{
    /// <summary>
    /// Perform dry-run transformation (no persistence)
    /// </summary>
    Task<DryRunResult> DryRunTransformAsync(DryRunRequest request);

    /// <summary>
    /// Get raw vs transformed data comparison for debugging
    /// </summary>
    Task<DataComparisonResult> CompareRawVsTransformedAsync(
        string entityType,
        int entityId);

    /// <summary>
    /// Test transformation rules against sample data
    /// </summary>
    Task<RuleTestResult> TestRulesAsync(RuleTestRequest request);

    /// <summary>
    /// Validate transformation configuration
    /// </summary>
    Task<ConfigValidationResult> ValidateConfigAsync(string entityType);
}

/// <summary>
/// Dry run request
/// </summary>
public class DryRunRequest
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string RawDataJson { get; set; } = string.Empty;
    public List<string>? SpecificRules { get; set; }
}

/// <summary>
/// Dry run result
/// </summary>
public class DryRunResult
{
    public bool Success { get; set; }
    public string? RawData { get; set; }
    public string? TransformedData { get; set; }
    public List<RuleApplication> RulesApplied { get; set; } = new();
    public long DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Rule application details
/// </summary>
public class RuleApplication
{
    public string RuleName { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? BeforeValue { get; set; }
    public string? AfterValue { get; set; }
    public bool Applied { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Data comparison result
/// </summary>
public class DataComparisonResult
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string? RawData { get; set; }
    public string? TransformedData { get; set; }
    public List<FieldComparison> FieldDifferences { get; set; } = new();
}

/// <summary>
/// Field comparison
/// </summary>
public class FieldComparison
{
    public string FieldName { get; set; } = string.Empty;
    public string? RawValue { get; set; }
    public string? TransformedValue { get; set; }
    public bool Changed { get; set; }
}

/// <summary>
/// Rule test request
/// </summary>
public class RuleTestRequest
{
    public string EntityType { get; set; } = string.Empty;
    public string SampleDataJson { get; set; } = string.Empty;
    public List<int>? RuleIds { get; set; }
}

/// <summary>
/// Rule test result
/// </summary>
public class RuleTestResult
{
    public bool Success { get; set; }
    public string? InputData { get; set; }
    public string? OutputData { get; set; }
    public List<RuleApplication> RuleResults { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Configuration validation result
/// </summary>
public class ConfigValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? ConfigMode { get; set; }
    public int RuleCount { get; set; }
    public bool ServiceHealthy { get; set; }
}
