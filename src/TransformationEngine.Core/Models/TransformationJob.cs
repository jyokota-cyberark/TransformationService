using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TransformationEngine.Core.Models;

/// <summary>
/// Represents a submitted transformation job
/// </summary>
[Table("TransformationJobs")]
public class TransformationJob
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(256)]
    public string JobId { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// Execution mode: InMemory, Spark, Kafka
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ExecutionMode { get; set; } = "InMemory";

    /// <summary>
    /// Job status: Submitted, Running, Completed, Failed, Cancelled
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Submitted";

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Progress { get; set; } = 0;

    /// <summary>
    /// Input data (serialized JSON)
    /// </summary>
    public string InputData { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated transformation rule IDs applied
    /// </summary>
    public string TransformationRuleIds { get; set; } = string.Empty;

    /// <summary>
    /// Context data as JSON
    /// </summary>
    public string? ContextData { get; set; }

    /// <summary>
    /// When job was submitted
    /// </summary>
    [Required]
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When job started executing
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When job completed or failed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Timeout in seconds (0 = unlimited)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 0;

    /// <summary>
    /// Error message if job failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Related job result
    /// </summary>
    public virtual TransformationJobResult? Result { get; set; }
}

/// <summary>
/// Represents the results of a completed transformation job
/// </summary>
[Table("TransformationJobResults")]
public class TransformationJobResult
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int JobId { get; set; }

    [ForeignKey("JobId")]
    public virtual TransformationJob Job { get; set; } = null!;

    /// <summary>
    /// Whether transformation succeeded
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Output data (serialized JSON)
    /// </summary>
    public string? OutputData { get; set; }

    /// <summary>
    /// Number of records processed
    /// </summary>
    public long RecordsProcessed { get; set; }

    /// <summary>
    /// Total execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Error message if transformation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Stack trace for debugging
    /// </summary>
    public string? ErrorStackTrace { get; set; }

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// When result was created/updated
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
