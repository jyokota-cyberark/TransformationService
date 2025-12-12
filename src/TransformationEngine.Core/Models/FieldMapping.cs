using TransformationEngine.Models;

namespace TransformationEngine.Core.Models;

/// <summary>
/// Represents a field mapping between source and target schemas
/// </summary>
public class FieldMapping
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Associated transformation rule ID (optional)
    /// </summary>
    public int? TransformationRuleId { get; set; }

    /// <summary>
    /// Entity type this mapping applies to
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Source field name
    /// </summary>
    public string SourceFieldName { get; set; } = string.Empty;

    /// <summary>
    /// Target field name (in target schema)
    /// </summary>
    public string TargetFieldName { get; set; } = string.Empty;

    /// <summary>
    /// Mapping type: "direct", "rule", "computed", "constant"
    /// </summary>
    public string MappingType { get; set; } = "direct";

    /// <summary>
    /// Constant value (when MappingType is "constant")
    /// </summary>
    public string? ConstantValue { get; set; }

    /// <summary>
    /// Transformation expression or script (when MappingType is "computed")
    /// Supports Cedar-like DSL expressions referencing principal, resource, action, context
    /// </summary>
    public string? TransformationExpression { get; set; }

    /// <summary>
    /// Data type of the target field (for validation and conversion)
    /// </summary>
    public string? TargetDataType { get; set; }

    /// <summary>
    /// Optional description of what this mapping does
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether to validate the mapped value against target schema
    /// </summary>
    public bool ValidateAgainstSchema { get; set; } = true;

    /// <summary>
    /// Source schema subject (from schema registry)
    /// </summary>
    public string? SourceSchemaSubject { get; set; }

    /// <summary>
    /// Priority for applying mappings (lower = first)
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Whether this mapping is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When this mapping was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this mapping was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Associated transformation rule (navigation property)
    /// </summary>
    public TransformationRule? TransformationRule { get; set; }
}

