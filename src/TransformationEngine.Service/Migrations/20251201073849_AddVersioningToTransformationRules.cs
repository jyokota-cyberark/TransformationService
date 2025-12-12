using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TransformationEngine.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddVersioningToTransformationRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentVersion",
                table: "TransformationRules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "TransformationRules",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "TransformationRules",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransformationProjects",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExecutionOrder = table.Column<int>(type: "integer", nullable: false),
                    Configuration = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformationProjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransformationRuleVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RuleId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    RuleType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Configuration = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ChangedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ChangeReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformationRuleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransformationRuleVersions_TransformationRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "TransformationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AirflowDagDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DagId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Schedule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    TransformationProjectId = table.Column<int>(type: "integer", nullable: true),
                    SparkJobId = table.Column<int>(type: "integer", nullable: true),
                    Configuration = table.Column<string>(type: "jsonb", nullable: true),
                    GeneratedDagPath = table.Column<string>(type: "text", nullable: true),
                    LastGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirflowDagDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AirflowDagDefinitions_SparkJobDefinitions_SparkJobId",
                        column: x => x.SparkJobId,
                        principalTable: "SparkJobDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AirflowDagDefinitions_TransformationProjects_Transformation~",
                        column: x => x.TransformationProjectId,
                        principalTable: "TransformationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TransformationProjectExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecordsProcessed = table.Column<int>(type: "integer", nullable: false),
                    RecordsFailed = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ExecutionMetadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformationProjectExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransformationProjectExecutions_TransformationProjects_Proj~",
                        column: x => x.ProjectId,
                        principalTable: "TransformationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransformationProjectRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    RuleId = table.Column<int>(type: "integer", nullable: false),
                    ExecutionOrder = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformationProjectRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransformationProjectRules_TransformationProjects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "TransformationProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransformationProjectRules_TransformationRules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "TransformationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AirflowDagDefinitions_DagId",
                table: "AirflowDagDefinitions",
                column: "DagId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AirflowDagDefinitions_EntityType",
                table: "AirflowDagDefinitions",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_AirflowDagDefinitions_IsActive",
                table: "AirflowDagDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AirflowDagDefinitions_SparkJobId",
                table: "AirflowDagDefinitions",
                column: "SparkJobId");

            migrationBuilder.CreateIndex(
                name: "IX_AirflowDagDefinitions_TransformationProjectId",
                table: "AirflowDagDefinitions",
                column: "TransformationProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationProjectExecutions_ExecutionId",
                table: "TransformationProjectExecutions",
                column: "ExecutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransformationProjectExecutions_ProjectId",
                table: "TransformationProjectExecutions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationProjectExecutions_StartedAt",
                table: "TransformationProjectExecutions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationProjectRules_ProjectId",
                table: "TransformationProjectRules",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationProjectRules_ProjectId_RuleId",
                table: "TransformationProjectRules",
                columns: new[] { "ProjectId", "RuleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransformationProjectRules_RuleId",
                table: "TransformationProjectRules",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationProjects_EntityType",
                table: "TransformationProjects",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationProjects_IsActive",
                table: "TransformationProjects",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationRuleVersions_CreatedAt",
                table: "TransformationRuleVersions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationRuleVersions_RuleId",
                table: "TransformationRuleVersions",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationRuleVersions_RuleId_Version",
                table: "TransformationRuleVersions",
                columns: new[] { "RuleId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AirflowDagDefinitions");

            migrationBuilder.DropTable(
                name: "TransformationProjectExecutions");

            migrationBuilder.DropTable(
                name: "TransformationProjectRules");

            migrationBuilder.DropTable(
                name: "TransformationRuleVersions");

            migrationBuilder.DropTable(
                name: "TransformationProjects");

            migrationBuilder.DropColumn(
                name: "CurrentVersion",
                table: "TransformationRules");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "TransformationRules");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "TransformationRules");
        }
    }
}
