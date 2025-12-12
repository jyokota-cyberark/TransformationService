using System.Text.Json.Serialization;
using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.ImportExport;

/// <summary>
/// Export format for a single transformation rule
/// </summary>
public class RuleExportDto
{
    /// <summary>
    /// API version
    /// </summary>
    public string ApiVersion { get; set; } = "transformation/v1";

    /// <summary>
    /// Kind of resource
    /// </summary>
    public string Kind { get; set; } = "TransformationRule";

    /// <summary>
    /// Rule metadata
    /// </summary>
    public RuleMetadataDto Metadata { get; set; } = new();

    /// <summary>
    /// Rule specification
    /// </summary>
    public RuleSpecDto Spec { get; set; } = new();
}

/// <summary>
/// Export format for a set of transformation rules
/// </summary>
public class RuleSetExportDto
{
    public string ApiVersion { get; set; } = "transformation/v1";
    public string Kind { get; set; } = "TransformationRuleSet";
    public RuleSetMetadataDto Metadata { get; set; } = new();
    public List<RuleItemDto> Rules { get; set; } = new();
}

/// <summary>
/// Metadata for a single rule
/// </summary>
public class RuleMetadataDto
{
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public Dictionary<string, string> Labels { get; set; } = new();
    public Dictionary<string, string> Annotations { get; set; } = new();
}

/// <summary>
/// Metadata for a rule set
/// </summary>
public class RuleSetMetadataDto
{
    public string? EntityType { get; set; }
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public string? ExportedBy { get; set; }
    public int RuleCount { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Rule specification
/// </summary>
public class RuleSpecDto
{
    public string RuleType { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Configuration as a dynamic object (for YAML/JSON serialization)
    /// </summary>
    public object? Configuration { get; set; }
}

/// <summary>
/// Rule item in a rule set
/// </summary>
public class RuleItemDto
{
    public string Name { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
    public object? Configuration { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
}

/// <summary>
/// Import request
/// </summary>
public class RuleImportRequest
{
    /// <summary>
    /// Content to import (YAML or JSON)
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Format of the content
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ImportFormat Format { get; set; } = ImportFormat.Yaml;

    /// <summary>
    /// Import options
    /// </summary>
    public RuleImportOptions Options { get; set; } = new();
}

/// <summary>
/// Import options
/// </summary>
public class RuleImportOptions
{
    /// <summary>
    /// Import mode
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ImportMode Mode { get; set; } = ImportMode.Merge;

    /// <summary>
    /// What to do on conflict
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ConflictResolution OnConflict { get; set; } = ConflictResolution.Skip;

    /// <summary>
    /// Whether to activate rules immediately after import
    /// </summary>
    public bool ActivateImmediately { get; set; }

    /// <summary>
    /// Whether to create version history
    /// </summary>
    public bool CreateVersions { get; set; } = true;

    /// <summary>
    /// Dry run - only validate and show what would change
    /// </summary>
    public bool DryRun { get; set; }

    /// <summary>
    /// Entity type to import to (overrides metadata)
    /// </summary>
    public string? TargetEntityType { get; set; }
}

/// <summary>
/// Import format
/// </summary>
public enum ImportFormat
{
    Yaml,
    Json
}

/// <summary>
/// Import mode
/// </summary>
public enum ImportMode
{
    /// <summary>
    /// Merge with existing rules
    /// </summary>
    Merge,

    /// <summary>
    /// Replace all existing rules
    /// </summary>
    Replace,

    /// <summary>
    /// Only create new rules, don't update existing
    /// </summary>
    CreateOnly
}

/// <summary>
/// Conflict resolution strategy
/// </summary>
public enum ConflictResolution
{
    /// <summary>
    /// Skip conflicting rules
    /// </summary>
    Skip,

    /// <summary>
    /// Overwrite existing rules
    /// </summary>
    Overwrite,

    /// <summary>
    /// Fail the entire import
    /// </summary>
    Fail
}

/// <summary>
/// Import result
/// </summary>
public class RuleImportResult
{
    public bool Success { get; set; }
    public RuleImportSummary Summary { get; set; } = new();
    public List<RuleChangeDto> Changes { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Import summary
/// </summary>
public class RuleImportSummary
{
    public int ToCreate { get; set; }
    public int ToUpdate { get; set; }
    public int ToSkip { get; set; }
    public int Conflicts { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
}

/// <summary>
/// Individual rule change
/// </summary>
public class RuleChangeDto
{
    public string RuleName { get; set; } = string.Empty;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChangeAction Action { get; set; }

    public string? Diff { get; set; }
    public List<string>? Changes { get; set; }
    public bool Applied { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Change action type
/// </summary>
public enum ChangeAction
{
    Create,
    Update,
    Skip,
    Conflict,
    Delete
}

/// <summary>
/// Export options
/// </summary>
public class RuleExportOptions
{
    /// <summary>
    /// Entity type to export (null for all)
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Rule types to export (null for all)
    /// </summary>
    public List<string>? RuleTypes { get; set; }

    /// <summary>
    /// Export format
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ImportFormat Format { get; set; } = ImportFormat.Yaml;

    /// <summary>
    /// Include inactive rules
    /// </summary>
    public bool IncludeInactive { get; set; }

    /// <summary>
    /// Include version history
    /// </summary>
    public bool IncludeHistory { get; set; }
}
