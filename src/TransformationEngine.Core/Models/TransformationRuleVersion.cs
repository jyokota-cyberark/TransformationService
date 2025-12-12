using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TransformationEngine.Core.Models;

/// <summary>
/// Tracks version history of transformation rules
/// </summary>
[Table("TransformationRuleVersions")]
public class TransformationRuleVersion
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int RuleId { get; set; }

    [Required]
    public int Version { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public string RuleType { get; set; } = string.Empty;

    /// <summary>
    /// Rule configuration stored as JSON
    /// </summary>
    [Required]
    [Column(TypeName = "jsonb")]
    public string Configuration { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    [Required]
    [MaxLength(50)]
    public string ChangeType { get; set; } = "Updated";  // Created, Updated, Deleted

    [MaxLength(100)]
    public string? ChangedBy { get; set; }

    public string? ChangeReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(RuleId))]
    public virtual TransformationEngine.Models.TransformationRule Rule { get; set; } = null!;
}

