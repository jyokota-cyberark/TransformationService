using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TransformationEngine.Core.Models;

/// <summary>
/// Defines an Airflow DAG that can be auto-generated
/// </summary>
[Table("AirflowDagDefinitions")]
public class AirflowDagDefinition
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string DagId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string EntityType { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Cron expression for scheduling (e.g., "0 2 * * *")
    /// </summary>
    [MaxLength(100)]
    public string? Schedule { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional link to transformation project
    /// </summary>
    public int? TransformationProjectId { get; set; }

    /// <summary>
    /// Optional link to Spark job definition
    /// </summary>
    public int? SparkJobId { get; set; }

    /// <summary>
    /// DAG-specific configuration stored as JSON
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? Configuration { get; set; }

    /// <summary>
    /// Path to the generated DAG file
    /// </summary>
    public string? GeneratedDagPath { get; set; }

    public DateTime? LastGeneratedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(TransformationProjectId))]
    public virtual TransformationProject? TransformationProject { get; set; }

    [ForeignKey(nameof(SparkJobId))]
    public virtual SparkJobDefinition? SparkJob { get; set; }
}

