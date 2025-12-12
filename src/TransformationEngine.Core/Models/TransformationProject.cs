using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TransformationEngine.Core.Models;

/// <summary>
/// Represents a collection of transformation rules grouped into a project/pipeline
/// </summary>
[Table("TransformationProjects")]
public class TransformationProject
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int ExecutionOrder { get; set; } = 0;

    /// <summary>
    /// Project-specific configuration stored as JSON
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? Configuration { get; set; }

    [MaxLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ICollection<TransformationProjectRule> ProjectRules { get; set; } = new List<TransformationProjectRule>();
    public virtual ICollection<TransformationProjectExecution> Executions { get; set; } = new List<TransformationProjectExecution>();
}

/// <summary>
/// Many-to-many relationship between projects and rules
/// </summary>
[Table("TransformationProjectRules")]
public class TransformationProjectRule
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProjectId { get; set; }

    [Required]
    public int RuleId { get; set; }

    public int ExecutionOrder { get; set; } = 0;

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(ProjectId))]
    public virtual TransformationProject Project { get; set; } = null!;

    [ForeignKey(nameof(RuleId))]
    public virtual TransformationEngine.Models.TransformationRule Rule { get; set; } = null!;
}

/// <summary>
/// Tracks execution history of transformation projects
/// </summary>
[Table("TransformationProjectExecutions")]
public class TransformationProjectExecution
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ProjectId { get; set; }

    [Required]
    public Guid ExecutionId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public int RecordsProcessed { get; set; } = 0;

    public int RecordsFailed { get; set; } = 0;

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Execution metadata stored as JSON
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? ExecutionMetadata { get; set; }

    // Navigation properties
    [ForeignKey(nameof(ProjectId))]
    public virtual TransformationProject Project { get; set; } = null!;
}

