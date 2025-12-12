namespace TransformationEngine.Core.Models.DTOs;

/// <summary>
/// Data Transfer Object for TransformationRule, used to avoid serialization issues
/// and provide a clean API contract
/// </summary>
public class TransformationRuleDto
{
    public int Id { get; set; }
    public int InventoryTypeId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string? SourcePattern { get; set; }
    public string? TargetPattern { get; set; }
    public string? LookupTableJson { get; set; }
    public string? CustomScript { get; set; }
    public string? ScriptLanguage { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public int CurrentVersion { get; set; }
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
}

