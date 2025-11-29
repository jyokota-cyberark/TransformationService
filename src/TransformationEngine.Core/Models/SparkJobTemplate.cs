using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace TransformationEngine.Core.Models;

/// <summary>
/// Defines reusable templates for creating Spark jobs
/// </summary>
public class SparkJobTemplate
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string TemplateKey { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string TemplateName { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Template Details
    [Required]
    [StringLength(50)]
    public string Language { get; set; } = string.Empty; // 'CSharp', 'Python', 'Scala'

    [StringLength(100)]
    public string? Category { get; set; }

    // Template Code
    [Required]
    [Column(TypeName = "text")]
    public string TemplateCode { get; set; } = string.Empty;

    [StringLength(50)]
    public string TemplateEngine { get; set; } = "Scriban";

    // Variable Schema
    [Required]
    [Column(TypeName = "jsonb")]
    public string VariablesJson { get; set; } = "[]";

    [NotMapped]
    public List<TemplateVariable>? Variables
    {
        get => string.IsNullOrEmpty(VariablesJson)
            ? null
            : JsonSerializer.Deserialize<List<TemplateVariable>>(VariablesJson);
        set => VariablesJson = value == null ? "[]" : JsonSerializer.Serialize(value);
    }

    // Sample Usage
    [Column(TypeName = "jsonb")]
    public string? SampleVariablesJson { get; set; }

    [NotMapped]
    public Dictionary<string, object>? SampleVariables
    {
        get => string.IsNullOrEmpty(SampleVariablesJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(SampleVariablesJson);
        set => SampleVariablesJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    [Column(TypeName = "text")]
    public string? SampleOutput { get; set; }

    // Metadata
    public bool IsBuiltIn { get; set; } = false;

    [Column(TypeName = "jsonb")]
    public string? TagsJson { get; set; }

    [NotMapped]
    public List<string>? Tags
    {
        get => string.IsNullOrEmpty(TagsJson)
            ? null
            : JsonSerializer.Deserialize<List<string>>(TagsJson);
        set => TagsJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    [StringLength(50)]
    public string? Version { get; set; }

    [StringLength(100)]
    public string? Author { get; set; }

    // State
    public bool IsActive { get; set; } = true;
    public int UsageCount { get; set; } = 0;

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Defines a variable in a template
/// </summary>
public class TemplateVariable
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string"; // string, number, boolean, array, object
    public bool Required { get; set; } = false;
    public object? Default { get; set; }
    public string? Description { get; set; }
}
