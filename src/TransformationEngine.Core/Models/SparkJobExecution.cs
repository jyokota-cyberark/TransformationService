using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace TransformationEngine.Core.Models;

/// <summary>
/// Tracks individual Spark job execution instances
/// </summary>
public class SparkJobExecution
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string ExecutionId { get; set; } = string.Empty;

    // Job Reference
    public int JobDefinitionId { get; set; }
    public virtual SparkJobDefinition? JobDefinition { get; set; }

    public int? ScheduleId { get; set; }
    public virtual SparkJobSchedule? Schedule { get; set; }

    // Execution Context
    [Required]
    [StringLength(50)]
    public string TriggerType { get; set; } = string.Empty; // 'Manual', 'Scheduled', 'Webhook', 'API'

    [StringLength(100)]
    public string? TriggeredBy { get; set; }

    // Spark Job Info
    [StringLength(200)]
    public string? SparkJobId { get; set; }

    [Column(TypeName = "text")]
    public string? SparkSubmissionCommand { get; set; }

    // Runtime Configuration
    public int ExecutorCores { get; set; }
    public int ExecutorMemoryMb { get; set; }
    public int NumExecutors { get; set; }

    [Column(TypeName = "jsonb")]
    public string? ArgumentsJson { get; set; }

    [NotMapped]
    public List<string>? Arguments
    {
        get => string.IsNullOrEmpty(ArgumentsJson)
            ? null
            : JsonSerializer.Deserialize<List<string>>(ArgumentsJson);
        set => ArgumentsJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    // Status Tracking
    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "Queued"; // 'Queued', 'Submitting', 'Running', 'Succeeded', 'Failed', 'Cancelled'

    public int Progress { get; set; } = 0; // 0-100

    [StringLength(200)]
    public string? CurrentStage { get; set; }

    // Timing
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? DurationSeconds { get; set; }

    // Results
    [StringLength(500)]
    public string? OutputPath { get; set; }

    public long? RowsProcessed { get; set; }
    public long? RecordsWritten { get; set; }
    public long? BytesRead { get; set; }
    public long? BytesWritten { get; set; }

    [Column(TypeName = "jsonb")]
    public string? ResultSummaryJson { get; set; }

    [NotMapped]
    public Dictionary<string, object>? ResultSummary
    {
        get => string.IsNullOrEmpty(ResultSummaryJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(ResultSummaryJson);
        set => ResultSummaryJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    // Error Handling
    [Column(TypeName = "text")]
    public string? ErrorMessage { get; set; }

    [Column(TypeName = "text")]
    public string? ErrorStackTrace { get; set; }

    public int RetryCount { get; set; } = 0;

    // Logs
    [Column(TypeName = "text")]
    public string? SparkDriverLog { get; set; }

    [Column(TypeName = "text")]
    public string? ApplicationLog { get; set; }

    [StringLength(500)]
    public string? LogPath { get; set; }

    // Metadata
    [StringLength(50)]
    public string? EntityType { get; set; }

    public int? EntityId { get; set; }
}
