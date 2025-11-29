using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransformationEngine.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddHangfireScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SparkJobSchedules_IsEnabled",
                table: "SparkJobSchedules");

            migrationBuilder.DropColumn(
                name: "EntityIdFilter",
                table: "SparkJobSchedules");

            migrationBuilder.DropColumn(
                name: "EntityTypeFilter",
                table: "SparkJobSchedules");

            migrationBuilder.DropColumn(
                name: "ExecutorCores",
                table: "SparkJobSchedules");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "SparkJobSchedules");

            migrationBuilder.DropColumn(
                name: "MaxRetries",
                table: "SparkJobSchedules");

            migrationBuilder.DropColumn(
                name: "WebhookUrl",
                table: "SparkJobSchedules");

            migrationBuilder.RenameColumn(
                name: "Timezone",
                table: "SparkJobSchedules",
                newName: "TimeZone");

            migrationBuilder.RenameColumn(
                name: "RuntimeArgumentsJson",
                table: "SparkJobSchedules",
                newName: "SparkConfigJson");

            migrationBuilder.RenameColumn(
                name: "RetryDelaySeconds",
                table: "SparkJobSchedules",
                newName: "SuccessCount");

            migrationBuilder.RenameColumn(
                name: "OneTimeExecutionTime",
                table: "SparkJobSchedules",
                newName: "ScheduledAt");

            migrationBuilder.RenameColumn(
                name: "NumExecutors",
                table: "SparkJobSchedules",
                newName: "SparkJobDefinitionId");

            migrationBuilder.RenameColumn(
                name: "NotifyOnSuccess",
                table: "SparkJobSchedules",
                newName: "IsPaused");

            migrationBuilder.RenameColumn(
                name: "NotifyOnFailure",
                table: "SparkJobSchedules",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "NotificationEmailsJson",
                table: "SparkJobSchedules",
                newName: "JobParametersJson");

            migrationBuilder.RenameColumn(
                name: "NextExecutionTime",
                table: "SparkJobSchedules",
                newName: "NextExecutionAt");

            migrationBuilder.RenameColumn(
                name: "LastExecutionTime",
                table: "SparkJobSchedules",
                newName: "LastExecutionAt");

            migrationBuilder.RenameColumn(
                name: "ExecutorMemoryMb",
                table: "SparkJobSchedules",
                newName: "DelayMinutes");

            migrationBuilder.RenameIndex(
                name: "IX_SparkJobSchedules_NextExecutionTime",
                table: "SparkJobSchedules",
                newName: "IX_SparkJobSchedules_NextExecutionAt");

            migrationBuilder.AlterColumn<string>(
                name: "TimeZone",
                table: "SparkJobSchedules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "HangfireJobId",
                table: "SparkJobSchedules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "SparkJobSchedules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SparkJobSchedules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecurringJobId",
                table: "SparkJobSchedules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobSchedules_IsActive",
                table: "SparkJobSchedules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobSchedules_SparkJobDefinitionId",
                table: "SparkJobSchedules",
                column: "SparkJobDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_SparkJobSchedules_SparkJobDefinitions_SparkJobDefinitionId",
                table: "SparkJobSchedules",
                column: "SparkJobDefinitionId",
                principalTable: "SparkJobDefinitions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SparkJobSchedules_SparkJobDefinitions_SparkJobDefinitionId",
                table: "SparkJobSchedules");

            migrationBuilder.DropIndex(
                name: "IX_SparkJobSchedules_IsActive",
                table: "SparkJobSchedules");

            migrationBuilder.DropIndex(
                name: "IX_SparkJobSchedules_SparkJobDefinitionId",
                table: "SparkJobSchedules");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "SparkJobSchedules");

            migrationBuilder.DropColumn(
                name: "RecurringJobId",
                table: "SparkJobSchedules");

            migrationBuilder.RenameColumn(
                name: "TimeZone",
                table: "SparkJobSchedules",
                newName: "Timezone");

            migrationBuilder.RenameColumn(
                name: "SuccessCount",
                table: "SparkJobSchedules",
                newName: "RetryDelaySeconds");

            migrationBuilder.RenameColumn(
                name: "SparkJobDefinitionId",
                table: "SparkJobSchedules",
                newName: "NumExecutors");

            migrationBuilder.RenameColumn(
                name: "SparkConfigJson",
                table: "SparkJobSchedules",
                newName: "RuntimeArgumentsJson");

            migrationBuilder.RenameColumn(
                name: "ScheduledAt",
                table: "SparkJobSchedules",
                newName: "OneTimeExecutionTime");

            migrationBuilder.RenameColumn(
                name: "NextExecutionAt",
                table: "SparkJobSchedules",
                newName: "NextExecutionTime");

            migrationBuilder.RenameColumn(
                name: "LastExecutionAt",
                table: "SparkJobSchedules",
                newName: "LastExecutionTime");

            migrationBuilder.RenameColumn(
                name: "JobParametersJson",
                table: "SparkJobSchedules",
                newName: "NotificationEmailsJson");

            migrationBuilder.RenameColumn(
                name: "IsPaused",
                table: "SparkJobSchedules",
                newName: "NotifyOnSuccess");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "SparkJobSchedules",
                newName: "NotifyOnFailure");

            migrationBuilder.RenameColumn(
                name: "DelayMinutes",
                table: "SparkJobSchedules",
                newName: "ExecutorMemoryMb");

            migrationBuilder.RenameIndex(
                name: "IX_SparkJobSchedules_NextExecutionAt",
                table: "SparkJobSchedules",
                newName: "IX_SparkJobSchedules_NextExecutionTime");

            migrationBuilder.AlterColumn<string>(
                name: "Timezone",
                table: "SparkJobSchedules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "HangfireJobId",
                table: "SparkJobSchedules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CreatedBy",
                table: "SparkJobSchedules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "EntityIdFilter",
                table: "SparkJobSchedules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityTypeFilter",
                table: "SparkJobSchedules",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExecutorCores",
                table: "SparkJobSchedules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "SparkJobSchedules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetries",
                table: "SparkJobSchedules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WebhookUrl",
                table: "SparkJobSchedules",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SparkJobSchedules_IsEnabled",
                table: "SparkJobSchedules",
                column: "IsEnabled");
        }
    }
}
