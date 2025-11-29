using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TransformationEngine.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddSparkJobManagementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SparkJobDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    JobName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    JobType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsGeneric = table.Column<bool>(type: "boolean", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StorageType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceCode = table.Column<string>(type: "text", nullable: true),
                    CompiledArtifactPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TemplateEngine = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    TemplateVariablesJson = table.Column<string>(type: "jsonb", nullable: true),
                    GeneratorType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    GeneratorConfigJson = table.Column<string>(type: "jsonb", nullable: true),
                    MainClass = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EntryPoint = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PyFile = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DefaultExecutorCores = table.Column<int>(type: "integer", nullable: false),
                    DefaultExecutorMemoryMb = table.Column<int>(type: "integer", nullable: false),
                    DefaultNumExecutors = table.Column<int>(type: "integer", nullable: false),
                    DefaultDriverMemoryMb = table.Column<int>(type: "integer", nullable: false),
                    SparkConfigJson = table.Column<string>(type: "jsonb", nullable: true),
                    DefaultArgumentsJson = table.Column<string>(type: "jsonb", nullable: true),
                    DependenciesJson = table.Column<string>(type: "jsonb", nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LastModifiedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SparkJobDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SparkJobTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TemplateKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TemplateName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TemplateCode = table.Column<string>(type: "text", nullable: false),
                    TemplateEngine = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Scriban"),
                    VariablesJson = table.Column<string>(type: "jsonb", nullable: false),
                    SampleVariablesJson = table.Column<string>(type: "jsonb", nullable: true),
                    SampleOutput = table.Column<string>(type: "text", nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SparkJobTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SparkJobSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ScheduleKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ScheduleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    JobDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    ScheduleType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OneTimeExecutionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RuntimeArgumentsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ExecutorCores = table.Column<int>(type: "integer", nullable: true),
                    ExecutorMemoryMb = table.Column<int>(type: "integer", nullable: true),
                    NumExecutors = table.Column<int>(type: "integer", nullable: true),
                    EntityTypeFilter = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EntityIdFilter = table.Column<int>(type: "integer", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastExecutionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextExecutionTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExecutionCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    MaxRetries = table.Column<int>(type: "integer", nullable: false),
                    RetryDelaySeconds = table.Column<int>(type: "integer", nullable: false),
                    NotifyOnSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyOnFailure = table.Column<bool>(type: "boolean", nullable: false),
                    NotificationEmailsJson = table.Column<string>(type: "jsonb", nullable: true),
                    WebhookUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HangfireJobId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SparkJobSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SparkJobSchedules_SparkJobDefinitions_JobDefinitionId",
                        column: x => x.JobDefinitionId,
                        principalTable: "SparkJobDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransformationRuleToSparkJobMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RuleSetHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    JobDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    RulesSnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    GenerationStrategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "DirectConversion"),
                    IsStale = table.Column<bool>(type: "boolean", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformationRuleToSparkJobMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransformationRuleToSparkJobMappings_SparkJobDefinitions_Jo~",
                        column: x => x.JobDefinitionId,
                        principalTable: "SparkJobDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SparkJobExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExecutionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    JobDefinitionId = table.Column<int>(type: "integer", nullable: false),
                    ScheduleId = table.Column<int>(type: "integer", nullable: true),
                    TriggerType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TriggeredBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SparkJobId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SparkSubmissionCommand = table.Column<string>(type: "text", nullable: true),
                    ExecutorCores = table.Column<int>(type: "integer", nullable: false),
                    ExecutorMemoryMb = table.Column<int>(type: "integer", nullable: false),
                    NumExecutors = table.Column<int>(type: "integer", nullable: false),
                    ArgumentsJson = table.Column<string>(type: "jsonb", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Queued"),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    CurrentStage = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    QueuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    OutputPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RowsProcessed = table.Column<long>(type: "bigint", nullable: true),
                    RecordsWritten = table.Column<long>(type: "bigint", nullable: true),
                    BytesRead = table.Column<long>(type: "bigint", nullable: true),
                    BytesWritten = table.Column<long>(type: "bigint", nullable: true),
                    ResultSummaryJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ErrorStackTrace = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    SparkDriverLog = table.Column<string>(type: "text", nullable: true),
                    ApplicationLog = table.Column<string>(type: "text", nullable: true),
                    LogPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EntityId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SparkJobExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SparkJobExecutions_SparkJobDefinitions_JobDefinitionId",
                        column: x => x.JobDefinitionId,
                        principalTable: "SparkJobDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SparkJobExecutions_SparkJobSchedules_ScheduleId",
                        column: x => x.ScheduleId,
                        principalTable: "SparkJobSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobDefinitions_Category",
                table: "SparkJobDefinitions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobDefinitions_EntityType",
                table: "SparkJobDefinitions",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobDefinitions_JobKey",
                table: "SparkJobDefinitions",
                column: "JobKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobDefinitions_JobType",
                table: "SparkJobDefinitions",
                column: "JobType");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobExecutions_EntityType",
                table: "SparkJobExecutions",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobExecutions_ExecutionId",
                table: "SparkJobExecutions",
                column: "ExecutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobExecutions_JobDefinitionId",
                table: "SparkJobExecutions",
                column: "JobDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobExecutions_QueuedAt",
                table: "SparkJobExecutions",
                column: "QueuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobExecutions_ScheduleId",
                table: "SparkJobExecutions",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobExecutions_Status",
                table: "SparkJobExecutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobSchedules_IsEnabled",
                table: "SparkJobSchedules",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobSchedules_JobDefinitionId",
                table: "SparkJobSchedules",
                column: "JobDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobSchedules_NextExecutionTime",
                table: "SparkJobSchedules",
                column: "NextExecutionTime");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobSchedules_ScheduleKey",
                table: "SparkJobSchedules",
                column: "ScheduleKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobTemplates_Category",
                table: "SparkJobTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobTemplates_Language",
                table: "SparkJobTemplates",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobTemplates_TemplateKey",
                table: "SparkJobTemplates",
                column: "TemplateKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransformationRuleToSparkJobMappings_EntityType",
                table: "TransformationRuleToSparkJobMappings",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationRuleToSparkJobMappings_JobDefinitionId",
                table: "TransformationRuleToSparkJobMappings",
                column: "JobDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationRuleToSparkJobMappings_RuleSetHash",
                table: "TransformationRuleToSparkJobMappings",
                column: "RuleSetHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SparkJobExecutions");

            migrationBuilder.DropTable(
                name: "SparkJobTemplates");

            migrationBuilder.DropTable(
                name: "TransformationRuleToSparkJobMappings");

            migrationBuilder.DropTable(
                name: "SparkJobSchedules");

            migrationBuilder.DropTable(
                name: "SparkJobDefinitions");
        }
    }
}
