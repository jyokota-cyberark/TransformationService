using System;
using System.Collections.Generic;

namespace TransformationEngine.Core.Models.DTOs;

public class TransformationProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ExecutionOrder { get; set; }
    public Dictionary<string, object>? Configuration { get; set; }
    public List<ProjectRuleDto> Rules { get; set; } = new();
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ProjectRuleDto
{
    public int RuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public int ExecutionOrder { get; set; }
    public bool IsEnabled { get; set; }
}

public class CreateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Dictionary<string, object>? Configuration { get; set; }
    public List<int>? RuleIds { get; set; }
}

public class UpdateProjectRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
    public Dictionary<string, object>? Configuration { get; set; }
}

public class ExecuteProjectRequest
{
    public Dictionary<string, object>? InputData { get; set; }
    public string ExecutionMode { get; set; } = "Spark";
    public int TimeoutSeconds { get; set; } = 1800;
}

public class ProjectExecutionDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Guid ExecutionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsFailed { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object>? ExecutionMetadata { get; set; }
}

