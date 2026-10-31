using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EntityEngineStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "definition_versions",
                schema: "platform",
                columns: table => new
                {
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AuthoringJson = table.Column<string>(type: "jsonb", nullable: false),
                    EffectiveModelJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_definition_versions", x => x.VersionId);
                });

            migrationBuilder.CreateTable(
                name: "entity_definitions",
                schema: "platform",
                columns: table => new
                {
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentPublishedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_definitions", x => x.DefinitionId);
                });

            migrationBuilder.CreateTable(
                name: "migration_runs",
                schema: "platform",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentStage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastCompletedStepOrdinal = table.Column<int>(type: "integer", nullable: false),
                    FailureMessage = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FailureStage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PlanJson = table.Column<string>(type: "jsonb", nullable: false),
                    PriorPublishedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_migration_runs", x => x.RunId);
                });

            migrationBuilder.CreateTable(
                name: "storage_mappings",
                schema: "platform",
                columns: table => new
                {
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TableName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    MappingJson = table.Column<string>(type: "jsonb", nullable: false),
                    MappedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storage_mappings", x => x.VersionId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_definition_versions_DefinitionId_VersionNumber",
                schema: "platform",
                table: "definition_versions",
                columns: new[] { "DefinitionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_definition_versions_EnvironmentId",
                schema: "platform",
                table: "definition_versions",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_entity_definitions_EnvironmentId",
                schema: "platform",
                table: "entity_definitions",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_entity_definitions_EnvironmentId_Slug",
                schema: "platform",
                table: "entity_definitions",
                columns: new[] { "EnvironmentId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_migration_runs_DefinitionId",
                schema: "platform",
                table: "migration_runs",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_migration_runs_EnvironmentId",
                schema: "platform",
                table: "migration_runs",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_migration_runs_PlanId",
                schema: "platform",
                table: "migration_runs",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_storage_mappings_DefinitionId",
                schema: "platform",
                table: "storage_mappings",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_storage_mappings_EnvironmentId",
                schema: "platform",
                table: "storage_mappings",
                column: "EnvironmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "definition_versions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "entity_definitions",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "migration_runs",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "storage_mappings",
                schema: "platform");
        }
    }
}
