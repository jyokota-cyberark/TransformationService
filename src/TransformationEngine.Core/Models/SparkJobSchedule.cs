using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace TransformationEngine.Core.Models;

/// <summary>
/// Defines a schedule for recurring or one-time Spark job execution
/// </summary>
public class SparkJobSchedule
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string ScheduleKey { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string ScheduleName { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Job Reference
    [Required]
    public int JobDefinitionId { get; set; }

    [ForeignKey(nameof(JobDefinitionId))]
    public SparkJobDefinition? JobDefinition { get; set; }

    // Schedule Type
    [Required]
    [StringLength(50)]
    public string ScheduleType { get; set; } = string.Empty; // 'Recurring', 'OneTime', 'Delayed'

    // Recurring Schedule (Cron)
    [StringLength(100)]
    public string? CronExpression { get; set; }

    [StringLength(100)]
    public string? TimeZone { get; set; } = "UTC";

    // One-Time Schedule
    public DateTime? ScheduledAt { get; set; }

    // Delayed Schedule (minutes from now)
    public int? DelayMinutes { get; set; }

    // Job Parameters
    [Column(TypeName = "jsonb")]
    public string? JobParametersJson { get; set; }

    [NotMapped]
    public Dictionary<string, object>? JobParameters
    {
        get => string.IsNullOrEmpty(JobParametersJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(JobParametersJson);
        set => JobParametersJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    // Spark Configuration Overrides
    [Column(TypeName = "jsonb")]
    public string? SparkConfigJson { get; set; }

    [NotMapped]
    public Dictionary<string, string>? SparkConfig
    {
        get => string.IsNullOrEmpty(SparkConfigJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(SparkConfigJson);
        set => SparkConfigJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    // Hangfire Integration
    [StringLength(100)]
    public string? HangfireJobId { get; set; }

    [StringLength(100)]
    public string? RecurringJobId { get; set; }

    // State
    public bool IsActive { get; set; } = true;
    public bool IsPaused { get; set; } = false;

    // Statistics
    public int ExecutionCount { get; set; } = 0;
    public DateTime? LastExecutionAt { get; set; }
    public DateTime? NextExecutionAt { get; set; }
    public int SuccessCount { get; set; } = 0;
    public int FailureCount { get; set; } = 0;

    // Audit
    [Required]
    [StringLength(100)]
    public string CreatedBy { get; set; } = "System";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
