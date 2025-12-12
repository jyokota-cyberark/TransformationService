using System;
using System.Collections.Generic;

namespace TransformationEngine.Core.Models.DTOs;

public class AirflowDagDto
{
    public int Id { get; set; }
    public string DagId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Schedule { get; set; }
    public bool IsActive { get; set; }
    public int? TransformationProjectId { get; set; }
    public int? SparkJobId { get; set; }
    public DagConfigurationDto? Configuration { get; set; }
    public string? GeneratedDagPath { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class DagConfigurationDto
{
    public int TimeoutSeconds { get; set; } = 1800;
    public int PollInterval { get; set; } = 30;
    public int Retries { get; set; } = 3;
    public int RetryDelayMinutes { get; set; } = 5;
    public string Owner { get; set; } = "airflow";
    public bool EmailOnFailure { get; set; } = true;
    public string? Email { get; set; }
    public Dictionary<string, object>? InputData { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
}

public class GenerateDagRequest
{
    public string DagId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Schedule { get; set; }  // Cron expression
    public int? TransformationProjectId { get; set; }
    public int? SparkJobId { get; set; }
    public List<int>? TransformationRuleIds { get; set; }
    public DagConfigurationDto? Configuration { get; set; }
}

public class DagPreviewResponse
{
    public string DagId { get; set; } = string.Empty;
    public string GeneratedCode { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}

