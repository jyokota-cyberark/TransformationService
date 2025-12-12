using System;
using System.Collections.Generic;

namespace TransformationEngine.Core.Models.DTOs;

public class RuleVersionDto
{
    public int Id { get; set; }
    public int RuleId { get; set; }
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RuleType { get; set; } = string.Empty;
    public Dictionary<string, object> Configuration { get; set; } = new();
    public bool IsActive { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string? ChangedBy { get; set; }
    public string? ChangeReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RuleVersionDiffDto
{
    public int RuleId { get; set; }
    public int Version1 { get; set; }
    public int Version2 { get; set; }
    public List<FieldChangeDto> Changes { get; set; } = new();
}

public class FieldChangeDto
{
    public string FieldName { get; set; } = string.Empty;
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
    public string ChangeType { get; set; } = string.Empty;  // Added, Modified, Removed
}

public class RollbackRuleRequest
{
    public int TargetVersion { get; set; }
    public string? Reason { get; set; }
    public string? ChangedBy { get; set; }
}

