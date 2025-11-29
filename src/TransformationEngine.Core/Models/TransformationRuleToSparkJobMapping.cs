using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace TransformationEngine.Core.Models;

/// <summary>
/// Maps transformation rules to generated Spark jobs
/// </summary>
public class TransformationRuleToSparkJobMapping
{
    [Key]
    public int Id { get; set; }

    // Transformation Rule Reference
    [Required]
    [StringLength(50)]
    public string EntityType { get; set; } = string.Empty;

    [Required]
    [StringLength(64)]
    public string RuleSetHash { get; set; } = string.Empty; // SHA256 hash of rule configuration

    // Generated Job Reference
    public int JobDefinitionId { get; set; }
    public virtual SparkJobDefinition? JobDefinition { get; set; }

    // Generation Metadata
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column(TypeName = "jsonb")]
    public string RulesSnapshotJson { get; set; } = "{}";

    [NotMapped]
    public Dictionary<string, object>? RulesSnapshot
    {
        get => string.IsNullOrEmpty(RulesSnapshotJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(RulesSnapshotJson);
        set => RulesSnapshotJson = value == null ? "{}" : JsonSerializer.Serialize(value);
    }

    [StringLength(50)]
    public string GenerationStrategy { get; set; } = "DirectConversion"; // 'DirectConversion', 'OptimizedBatch'

    // State
    public bool IsStale { get; set; } = false; // True if rules changed
    public DateTime? LastUsedAt { get; set; }
}
