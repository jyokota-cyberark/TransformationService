using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TransformationEngine.Migrations
{
    /// <inheritdoc />
    public partial class AddTransformationJobTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransformationJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    JobName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExecutionMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "InMemory"),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Submitted"),
                    Progress = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    InputData = table.Column<string>(type: "jsonb", nullable: false),
                    TransformationRuleIds = table.Column<string>(type: "text", nullable: false),
                    ContextData = table.Column<string>(type: "jsonb", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformationJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransformationJobResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<int>(type: "integer", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "boolean", nullable: false),
                    OutputData = table.Column<string>(type: "jsonb", nullable: true),
                    RecordsProcessed = table.Column<long>(type: "bigint", nullable: false),
                    ExecutionTimeMs = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ErrorStackTrace = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformationJobResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransformationJobResults_TransformationJobs_JobId",
                        column: x => x.JobId,
                        principalTable: "TransformationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransformationJobResults_JobId",
                table: "TransformationJobResults",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransformationJobs_ExecutionMode",
                table: "TransformationJobs",
                column: "ExecutionMode");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationJobs_JobId",
                table: "TransformationJobs",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransformationJobs_Status",
                table: "TransformationJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationJobs_SubmittedAt",
                table: "TransformationJobs",
                column: "SubmittedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransformationJobResults");

            migrationBuilder.DropTable(
                name: "TransformationJobs");
        }
    }
}
