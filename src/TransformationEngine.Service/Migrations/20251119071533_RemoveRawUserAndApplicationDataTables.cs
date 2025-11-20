using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TransformationEngine.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRawUserAndApplicationDataTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawApplicationData");

            migrationBuilder.DropTable(
                name: "RawUserData");

            migrationBuilder.CreateTable(
                name: "RawData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InventoryTypeId = table.Column<int>(type: "integer", nullable: false),
                    SourceItemId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawData", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RawData_InventoryTypeId",
                table: "RawData",
                column: "InventoryTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RawData_SourceItemId",
                table: "RawData",
                column: "SourceItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RawData");

            migrationBuilder.CreateTable(
                name: "RawApplicationData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawUserData", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RawApplicationData_SourceId",
                table: "RawApplicationData",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_RawUserData_SourceId",
                table: "RawUserData",
                column: "SourceId");
        }
    }
}
