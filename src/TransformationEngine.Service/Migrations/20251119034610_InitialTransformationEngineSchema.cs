using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TransformationEngine.Migrations
{
    /// <inheritdoc />
    public partial class InitialTransformationEngineSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RawApplicationData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawApplicationData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RawUserData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SourceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawUserData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransformationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryTypeId = table.Column<int>(type: "integer", nullable: false),
                    FieldName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RuleType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourcePattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TargetPattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LookupTableJson = table.Column<string>(type: "jsonb", nullable: true),
                    CustomScript = table.Column<string>(type: "text", nullable: true),
                    ScriptLanguage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastModifiedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransformedEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RawData = table.Column<string>(type: "jsonb", nullable: false),
                    TransformedData = table.Column<string>(type: "jsonb", nullable: false),
                    TransformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformedEntities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransformationHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransformedEntityId = table.Column<int>(type: "integer", nullable: false),
                    RuleName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RuleType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FieldName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OriginalValue = table.Column<string>(type: "jsonb", nullable: true),
                    TransformedValue = table.Column<string>(type: "jsonb", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransformationHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransformationHistory_TransformedEntities_TransformedEntity~",
                        column: x => x.TransformedEntityId,
                        principalTable: "TransformedEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RawApplicationData_SourceId",
                table: "RawApplicationData",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_RawUserData_SourceId",
                table: "RawUserData",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationHistory_AppliedAt",
                table: "TransformationHistory",
                column: "AppliedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationHistory_TransformedEntityId",
                table: "TransformationHistory",
                column: "TransformedEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationRules_FieldName",
                table: "TransformationRules",
                column: "FieldName");

            migrationBuilder.CreateIndex(
                name: "IX_TransformationRules_InventoryTypeId_IsActive_Priority",
                table: "TransformationRules",
                columns: new[] { "InventoryTypeId", "IsActive", "Priority" });

            migrationBuilder.CreateIndex(
                name: "IX_TransformedEntities_EntityType_SourceId",
                table: "TransformedEntities",
                columns: new[] { "EntityType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_TransformedEntities_TransformedAt",
                table: "TransformedEntities",
                column: "TransformedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawApplicationData");

            migrationBuilder.DropTable(
                name: "RawUserData");

            migrationBuilder.DropTable(
                name: "TransformationHistory");

            migrationBuilder.DropTable(
                name: "TransformationRules");

            migrationBuilder.DropTable(
                name: "TransformedEntities");
        }
    }
}
