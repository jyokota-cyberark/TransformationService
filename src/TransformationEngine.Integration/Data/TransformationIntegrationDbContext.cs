using Microsoft.EntityFrameworkCore;
using TransformationEngine.Integration.Configuration;
using TransformationEngine.Integration.Models;
using TransformationEngine.Core.Models;

namespace TransformationEngine.Integration.Data;

/// <summary>
/// Database context for transformation integration
/// </summary>
public class TransformationIntegrationDbContext : DbContext
{
    public TransformationIntegrationDbContext(DbContextOptions<TransformationIntegrationDbContext> options)
        : base(options)
    {
    }

    public DbSet<TransformationConfigEntity> TransformationConfigs { get; set; } = null!;
    public DbSet<TransformationRule> TransformationRules { get; set; } = null!;
    public DbSet<TransformationJobQueue> TransformationJobQueue { get; set; } = null!;
    public DbSet<TransformationHistory> TransformationHistory { get; set; } = null!;
    public DbSet<FieldMapping> FieldMappings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // TransformationConfig
        modelBuilder.Entity<TransformationConfigEntity>(entity =>
        {
            entity.ToTable("TransformationConfig");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EntityType).IsUnique();
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Mode).IsRequired().HasMaxLength(20);
            entity.Property(e => e.RuleStorage).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ExternalApiUrl).HasMaxLength(500);
            entity.Property(e => e.KafkaEnrichmentConfig).HasColumnType("jsonb");
            entity.Property(e => e.CustomSettings).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        // TransformationRules
        modelBuilder.Entity<TransformationRule>(entity =>
        {
            entity.ToTable("TransformationRules");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EntityType, e.RuleName });
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RuleName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RuleType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Configuration).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        // TransformationJobQueue
        modelBuilder.Entity<TransformationJobQueue>(entity =>
        {
            entity.ToTable("TransformationJobQueue");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.JobId).IsUnique(); // Add unique index on JobId
            entity.HasIndex(e => new { e.EntityType, e.Status });
            entity.Property(e => e.JobId).IsRequired().HasMaxLength(50); // Add JobId column
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RawData).HasColumnType("jsonb");
            entity.Property(e => e.GeneratedFields).HasColumnType("jsonb");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        });

        // TransformationHistory
        modelBuilder.Entity<TransformationHistory>(entity =>
        {
            entity.ToTable("TransformationHistory");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EntityType, e.EntityId, e.CreatedAt });
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Mode).IsRequired().HasMaxLength(20);
            entity.Property(e => e.RulesApplied).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
        });

        // FieldMappings
        modelBuilder.Entity<FieldMapping>(entity =>
        {
            entity.ToTable("FieldMappings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EntityType, e.SourceFieldName, e.TargetFieldName });
            entity.HasIndex(e => e.TransformationRuleId);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SourceFieldName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TargetFieldName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.MappingType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.ConstantValue).HasMaxLength(500);
            entity.Property(e => e.TransformationExpression).HasMaxLength(2000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("NOW()");
            
            // Navigation property to TransformationRule
            entity.HasOne(e => e.TransformationRule)
                .WithMany()
                .HasForeignKey(e => e.TransformationRuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

/// <summary>
/// Entity for storing transformation configuration in database
/// </summary>
public class TransformationConfigEntity
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Mode { get; set; } = TransformationMode.Sidecar.ToString();
    public string RuleStorage { get; set; } = RuleStorageMode.Central.ToString();
    public string? ExternalApiUrl { get; set; }
    public bool KafkaEnrichmentEnabled { get; set; }
    public string? KafkaEnrichmentConfig { get; set; } // JSON
    public int Priority { get; set; }
    public bool AllowGeneratedFieldRetransformation { get; set; } = true;
    public string? CustomSettings { get; set; } // JSON
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Convert to EntityTypeConfig
    /// </summary>
    public EntityTypeConfig ToEntityTypeConfig()
    {
        var config = new EntityTypeConfig
        {
            EntityType = EntityType,
            Enabled = Enabled,
            Mode = Enum.Parse<TransformationMode>(Mode),
            RuleStorage = Enum.Parse<RuleStorageMode>(RuleStorage),
            Priority = Priority,
            AllowGeneratedFieldRetransformation = AllowGeneratedFieldRetransformation
        };

        if (!string.IsNullOrEmpty(KafkaEnrichmentConfig))
        {
            config.KafkaEnrichment = System.Text.Json.JsonSerializer.Deserialize<KafkaEnrichmentConfig>(KafkaEnrichmentConfig) ?? new();
        }

        if (!string.IsNullOrEmpty(CustomSettings))
        {
            config.CustomSettings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(CustomSettings) ?? new();
        }

        return config;
    }

    /// <summary>
    /// Create from EntityTypeConfig
    /// </summary>
    public static TransformationConfigEntity FromEntityTypeConfig(EntityTypeConfig config)
    {
        return new TransformationConfigEntity
        {
            EntityType = config.EntityType,
            Enabled = config.Enabled,
            Mode = config.Mode.ToString(),
            RuleStorage = config.RuleStorage.ToString(),
            Priority = config.Priority,
            AllowGeneratedFieldRetransformation = config.AllowGeneratedFieldRetransformation,
            KafkaEnrichmentEnabled = config.KafkaEnrichment.Enabled,
            KafkaEnrichmentConfig = System.Text.Json.JsonSerializer.Serialize(config.KafkaEnrichment),
            CustomSettings = System.Text.Json.JsonSerializer.Serialize(config.CustomSettings),
            UpdatedAt = DateTime.UtcNow
        };
    }
}
