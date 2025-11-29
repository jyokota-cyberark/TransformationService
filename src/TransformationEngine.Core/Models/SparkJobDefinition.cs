using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace TransformationEngine.Core.Models;

/// <summary>
/// Defines a Spark job that can be executed on the Spark cluster
/// </summary>
public class SparkJobDefinition
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string JobKey { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string JobName { get; set; } = string.Empty;

    public string? Description { get; set; }

    // Job Type & Language
    [Required]
    [StringLength(50)]
    public string JobType { get; set; } = string.Empty; // 'Static', 'Template', 'Generated', 'FromRules'

    [Required]
    [StringLength(50)]
    public string Language { get; set; } = string.Empty; // 'CSharp', 'Python', 'Scala'

    // Entity Type Binding
    public bool IsGeneric { get; set; } = true;

    [StringLength(50)]
    public string? EntityType { get; set; }

    // Storage
    [Required]
    [StringLength(50)]
    public string StorageType { get; set; } = string.Empty; // 'FileSystem', 'Database', 'Both'

    [StringLength(500)]
    public string? FilePath { get; set; }

    [Column(TypeName = "text")]
    public string? SourceCode { get; set; }

    [StringLength(500)]
    public string? CompiledArtifactPath { get; set; }

    // Template Configuration (for JobType = 'Template')
    [StringLength(50)]
    public string? TemplateEngine { get; set; } // 'Liquid', 'Scriban', 'Handlebars'

    [Column(TypeName = "jsonb")]
    public string? TemplateVariablesJson { get; set; }

    [NotMapped]
    public Dictionary<string, object>? TemplateVariables
    {
        get => string.IsNullOrEmpty(TemplateVariablesJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(TemplateVariablesJson);
        set => TemplateVariablesJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    // Code Generation (for JobType = 'Generated')
    [StringLength(50)]
    public string? GeneratorType { get; set; } // 'TransformationRules', 'ConfigDriven'

    [Column(TypeName = "jsonb")]
    public string? GeneratorConfigJson { get; set; }

    [NotMapped]
    public Dictionary<string, object>? GeneratorConfig
    {
        get => string.IsNullOrEmpty(GeneratorConfigJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(GeneratorConfigJson);
        set => GeneratorConfigJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    // Spark Configuration
    [StringLength(200)]
    public string? MainClass { get; set; } // For Scala/Java

    [StringLength(200)]
    public string? EntryPoint { get; set; } // For .NET

    [StringLength(200)]
    public string? PyFile { get; set; } // For Python

    public int DefaultExecutorCores { get; set; } = 2;
    public int DefaultExecutorMemoryMb { get; set; } = 2048;
    public int DefaultNumExecutors { get; set; } = 2;
    public int DefaultDriverMemoryMb { get; set; } = 1024;

    [Column(TypeName = "jsonb")]
    public string? SparkConfigJson { get; set; }

    [NotMapped]
    public Dictionary<string, object>? SparkConfig
    {
        get => string.IsNullOrEmpty(SparkConfigJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(SparkConfigJson);
        set => SparkConfigJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    // Arguments & Dependencies
    [Column(TypeName = "jsonb")]
    public string? DefaultArgumentsJson { get; set; }

    [NotMapped]
    public List<string>? DefaultArguments
    {
        get => string.IsNullOrEmpty(DefaultArgumentsJson)
            ? null
            : JsonSerializer.Deserialize<List<string>>(DefaultArgumentsJson);
        set => DefaultArgumentsJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    [Column(TypeName = "jsonb")]
    public string? DependenciesJson { get; set; }

    [NotMapped]
    public Dictionary<string, List<string>>? Dependencies
    {
        get => string.IsNullOrEmpty(DependenciesJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, List<string>>>(DependenciesJson);
        set => DependenciesJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    // Metadata
    [StringLength(100)]
    public string? Category { get; set; } // 'ETL', 'Analytics', 'Enrichment', 'Validation'

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
    public bool IsTemplate { get; set; } = false;

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    [StringLength(100)]
    public string? LastModifiedBy { get; set; }

    // Navigation Properties
    public virtual ICollection<SparkJobSchedule> Schedules { get; set; } = new List<SparkJobSchedule>();
    public virtual ICollection<SparkJobExecution> Executions { get; set; } = new List<SparkJobExecution>();
}
