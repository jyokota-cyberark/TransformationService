using Microsoft.EntityFrameworkCore;
using TransformationEngine.Models;

namespace TransformationEngine.Data;

public class TransformationEngineDbContext : DbContext
{
    public TransformationEngineDbContext(DbContextOptions<TransformationEngineDbContext> options)
        : base(options) { }

    // Generic raw data table for all inventory types
    public DbSet<RawData> RawData { get; set; } = null!;

    public DbSet<TransformationRule> TransformationRules { get; set; } = null!;
    public DbSet<TransformedEntity> TransformedEntities { get; set; } = null!;
    public DbSet<TransformationHistory> TransformationHistories { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure generic RawData
        modelBuilder.Entity<RawData>(entity =>
        {
            entity.ToTable("RawData");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InventoryTypeId).IsRequired();
            entity.Property(e => e.SourceItemId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.DataJson).HasColumnType("jsonb").IsRequired().HasColumnName("Data");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Ignore(e => e.Data); // Ignore helper property
            entity.HasIndex(e => e.SourceItemId);
            entity.HasIndex(e => e.InventoryTypeId);
        });

            // RawUserData and RawApplicationData removed; now using generic RawData table
        // Configure TransformationRule
        modelBuilder.Entity<TransformationRule>(entity =>
        {
            entity.ToTable("TransformationRules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FieldName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RuleName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RuleType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SourcePattern).HasMaxLength(500);
            entity.Property(e => e.TargetPattern).HasMaxLength(500);
            entity.Property(e => e.CustomScript).HasColumnType("text");
            entity.Property(e => e.ScriptLanguage).HasMaxLength(50);
            entity.Property(e => e.LookupTableJson).HasColumnType("jsonb");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Priority).HasDefaultValue(0);
            entity.HasIndex(e => new { e.InventoryTypeId, e.IsActive, e.Priority });
            entity.HasIndex(e => e.FieldName);
        });

        // Configure TransformedEntity
        modelBuilder.Entity<TransformedEntity>(entity =>
        {
            entity.ToTable("TransformedEntities");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SourceId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RawDataJson).HasColumnType("jsonb").IsRequired().HasColumnName("RawData");
            entity.Property(e => e.TransformedDataJson).HasColumnType("jsonb").IsRequired().HasColumnName("TransformedData");
            entity.Property(e => e.TransformedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Ignore(e => e.RawData); // Ignore helper property
            entity.Ignore(e => e.TransformedData); // Ignore helper property
            entity.HasMany(e => e.TransformationHistory)
                .WithOne(h => h.TransformedEntity)
                .HasForeignKey(h => h.TransformedEntityId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.EntityType, e.SourceId });
            entity.HasIndex(e => e.TransformedAt);
        });

        // Configure TransformationHistory
        modelBuilder.Entity<TransformationHistory>(entity =>
        {
            entity.ToTable("TransformationHistory");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RuleName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.RuleType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FieldName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.OriginalValueJson).HasColumnType("jsonb").HasColumnName("OriginalValue");
            entity.Property(e => e.TransformedValueJson).HasColumnType("jsonb").HasColumnName("TransformedValue");
            entity.Property(e => e.AppliedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Ignore(e => e.OriginalValue); // Ignore helper property
            entity.Ignore(e => e.TransformedValue); // Ignore helper property
            entity.HasIndex(e => e.TransformedEntityId);
            entity.HasIndex(e => e.AppliedAt);
        });
    }
}
