using Microsoft.EntityFrameworkCore;
using TransformationEngine.Core.Models;
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

    // Transformation job management
    public DbSet<TransformationJob> TransformationJobs { get; set; } = null!;
    public DbSet<TransformationJobResult> TransformationJobResults { get; set; } = null!;

    // Spark job management
    public DbSet<SparkJobDefinition> SparkJobDefinitions { get; set; } = null!;
    public DbSet<SparkJobSchedule> SparkJobSchedules { get; set; } = null!;
    public DbSet<SparkJobExecution> SparkJobExecutions { get; set; } = null!;
    public DbSet<SparkJobTemplate> SparkJobTemplates { get; set; } = null!;
    public DbSet<TransformationRuleToSparkJobMapping> TransformationRuleToSparkJobMappings { get; set; } = null!;

    // Transformation projects
    public DbSet<TransformationProject> TransformationProjects { get; set; } = null!;
    public DbSet<TransformationProjectRule> TransformationProjectRules { get; set; } = null!;
    public DbSet<TransformationProjectExecution> TransformationProjectExecutions { get; set; } = null!;

    // Rule versioning
    public DbSet<TransformationRuleVersion> TransformationRuleVersions { get; set; } = null!;

    // Airflow DAG definitions
    public DbSet<AirflowDagDefinition> AirflowDagDefinitions { get; set; } = null!;

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

        // Configure TransformationJob
        modelBuilder.Entity<TransformationJob>(entity =>
        {
            entity.ToTable("TransformationJobs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JobId).IsRequired().HasMaxLength(256);
            entity.Property(e => e.JobName).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ExecutionMode).IsRequired().HasMaxLength(50).HasDefaultValue("InMemory");
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Submitted");
            entity.Property(e => e.Progress).HasDefaultValue(0);
            entity.Property(e => e.InputData).HasColumnType("jsonb");
            entity.Property(e => e.ContextData).HasColumnType("jsonb");
            entity.Property(e => e.SubmittedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.HasOne(e => e.Result)
                .WithOne(r => r.Job)
                .HasForeignKey<TransformationJobResult>(r => r.JobId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.JobId).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ExecutionMode);
            entity.HasIndex(e => e.SubmittedAt);
        });

        // Configure TransformationJobResult
        modelBuilder.Entity<TransformationJobResult>(entity =>
        {
            entity.ToTable("TransformationJobResults");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OutputData).HasColumnType("jsonb");
            entity.Property(e => e.ExecutionTimeMs).HasDefaultValue(0);
            entity.Property(e => e.ErrorStackTrace).HasColumnType("text");
            entity.Property(e => e.Metadata).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.JobId);
        });

        // Configure SparkJobDefinition
        modelBuilder.Entity<SparkJobDefinition>(entity =>
        {
            entity.ToTable("SparkJobDefinitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JobKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.JobName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.JobType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Language).IsRequired().HasMaxLength(50);
            entity.Property(e => e.StorageType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Ignore(e => e.TemplateVariables);
            entity.Ignore(e => e.GeneratorConfig);
            entity.Ignore(e => e.SparkConfig);
            entity.Ignore(e => e.DefaultArguments);
            entity.Ignore(e => e.Dependencies);
            entity.Ignore(e => e.Tags);
            entity.HasIndex(e => e.JobKey).IsUnique();
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.JobType);
            entity.HasIndex(e => e.Category);
        });

        // Configure SparkJobSchedule
        modelBuilder.Entity<SparkJobSchedule>(entity =>
        {
            entity.ToTable("SparkJobSchedules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ScheduleKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ScheduleName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ScheduleType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TimeZone).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Ignore(e => e.JobParameters);
            entity.Ignore(e => e.SparkConfig);
            entity.HasOne(e => e.JobDefinition)
                .WithMany()
                .HasForeignKey(e => e.JobDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.ScheduleKey).IsUnique();
            entity.HasIndex(e => e.JobDefinitionId);
            entity.HasIndex(e => e.NextExecutionAt);
            entity.HasIndex(e => e.IsActive);
        });

        // Configure SparkJobExecution
        modelBuilder.Entity<SparkJobExecution>(entity =>
        {
            entity.ToTable("SparkJobExecutions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExecutionId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TriggerType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Queued");
            entity.Property(e => e.QueuedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Ignore(e => e.Arguments);
            entity.Ignore(e => e.ResultSummary);
            entity.HasOne(e => e.JobDefinition)
                .WithMany(j => j.Executions)
                .HasForeignKey(e => e.JobDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Schedule)
                .WithMany()
                .HasForeignKey(e => e.ScheduleId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.ExecutionId).IsUnique();
            entity.HasIndex(e => e.JobDefinitionId);
            entity.HasIndex(e => e.ScheduleId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.QueuedAt);
            entity.HasIndex(e => e.EntityType);
        });

        // Configure SparkJobTemplate
        modelBuilder.Entity<SparkJobTemplate>(entity =>
        {
            entity.ToTable("SparkJobTemplates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TemplateKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TemplateName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Language).IsRequired().HasMaxLength(50);
            entity.Property(e => e.TemplateCode).IsRequired().HasColumnType("text");
            entity.Property(e => e.TemplateEngine).IsRequired().HasMaxLength(50).HasDefaultValue("Scriban");
            entity.Property(e => e.VariablesJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Ignore(e => e.Variables);
            entity.Ignore(e => e.SampleVariables);
            entity.Ignore(e => e.Tags);
            entity.HasIndex(e => e.TemplateKey).IsUnique();
            entity.HasIndex(e => e.Language);
            entity.HasIndex(e => e.Category);
        });

        // Configure TransformationRuleToSparkJobMapping
        modelBuilder.Entity<TransformationRuleToSparkJobMapping>(entity =>
        {
            entity.ToTable("TransformationRuleToSparkJobMappings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RuleSetHash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.RulesSnapshotJson).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.GenerationStrategy).IsRequired().HasMaxLength(50).HasDefaultValue("DirectConversion");
            entity.Property(e => e.GeneratedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Ignore(e => e.RulesSnapshot);
            entity.HasOne(e => e.JobDefinition)
                .WithMany()
                .HasForeignKey(e => e.JobDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.JobDefinitionId);
            entity.HasIndex(e => e.RuleSetHash);
        });

        // Configure TransformationProject
        modelBuilder.Entity<TransformationProject>(entity =>
        {
            entity.ToTable("TransformationProjects");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Configuration).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.IsActive);
        });

        // Configure TransformationProjectRule
        modelBuilder.Entity<TransformationProjectRule>(entity =>
        {
            entity.ToTable("TransformationProjectRules");
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Project)
                .WithMany(p => p.ProjectRules)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Rule)
                .WithMany()
                .HasForeignKey(e => e.RuleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.RuleId);
            entity.HasIndex(e => new { e.ProjectId, e.RuleId }).IsUnique();
        });

        // Configure TransformationProjectExecution
        modelBuilder.Entity<TransformationProjectExecution>(entity =>
        {
            entity.ToTable("TransformationProjectExecutions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ExecutionMetadata).HasColumnType("jsonb");
            entity.HasOne(e => e.Project)
                .WithMany(p => p.Executions)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.ProjectId);
            entity.HasIndex(e => e.ExecutionId).IsUnique();
            entity.HasIndex(e => e.StartedAt);
        });

        // Configure TransformationRuleVersion
        modelBuilder.Entity<TransformationRuleVersion>(entity =>
        {
            entity.ToTable("TransformationRuleVersions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.RuleType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Configuration).IsRequired().HasColumnType("jsonb");
            entity.Property(e => e.ChangeType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(e => e.Rule)
                .WithMany()
                .HasForeignKey(e => e.RuleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.RuleId);
            entity.HasIndex(e => new { e.RuleId, e.Version }).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure AirflowDagDefinition
        modelBuilder.Entity<AirflowDagDefinition>(entity =>
        {
            entity.ToTable("AirflowDagDefinitions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DagId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.EntityType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Configuration).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(e => e.TransformationProject)
                .WithMany()
                .HasForeignKey(e => e.TransformationProjectId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.SparkJob)
                .WithMany()
                .HasForeignKey(e => e.SparkJobId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.DagId).IsUnique();
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.IsActive);
        });
    }
}
