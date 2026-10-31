using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProcessEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "process_activity_logs",
                schema: "platform",
                columns: table => new
                {
                    LogId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ActivityType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InputJson = table.Column<string>(type: "jsonb", nullable: true),
                    OutputJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_activity_logs", x => x.LogId);
                });

            migrationBuilder.CreateTable(
                name: "process_definitions",
                schema: "platform",
                columns: table => new
                {
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CurrentPublishedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_definitions", x => x.DefinitionId);
                });

            migrationBuilder.CreateTable(
                name: "process_instances",
                schema: "platform",
                columns: table => new
                {
                    InstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VariablesJson = table.Column<string>(type: "jsonb", nullable: false),
                    CursorJson = table.Column<string>(type: "jsonb", nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    WaitingUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PrincipalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PrincipalRolesJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_instances", x => x.InstanceId);
                });

            migrationBuilder.CreateTable(
                name: "process_versions",
                schema: "platform",
                columns: table => new
                {
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AuthoringJson = table.Column<string>(type: "jsonb", nullable: false),
                    CompiledJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_versions", x => x.VersionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_process_activity_logs_InstanceId",
                schema: "platform",
                table: "process_activity_logs",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_process_activity_logs_StartedAt",
                schema: "platform",
                table: "process_activity_logs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_process_definitions_EnvironmentId_Slug",
                schema: "platform",
                table: "process_definitions",
                columns: new[] { "EnvironmentId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_definitions_ProjectId",
                schema: "platform",
                table: "process_definitions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_process_instances_DefinitionId",
                schema: "platform",
                table: "process_instances",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_process_instances_EnvironmentId",
                schema: "platform",
                table: "process_instances",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_process_instances_Status_WaitingUntil",
                schema: "platform",
                table: "process_instances",
                columns: new[] { "Status", "WaitingUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_process_versions_DefinitionId_VersionNumber",
                schema: "platform",
                table: "process_versions",
                columns: new[] { "DefinitionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_versions_EnvironmentId",
                schema: "platform",
                table: "process_versions",
                column: "EnvironmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "process_activity_logs",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "process_definitions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "process_instances",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "process_versions",
                schema: "platform");
        }
    }
}
