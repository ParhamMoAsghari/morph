using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RealtimeHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_credentials",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    KeyPrefix = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByPrincipalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_credentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorPrincipalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResourceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DetailJson = table.Column<string>(type: "jsonb", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "usage_counters",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metric = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usage_counters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_subscriptions",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SigningSecret = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EntitySlug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EventTypesJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_credentials_KeyPrefix",
                schema: "platform",
                table: "api_credentials",
                column: "KeyPrefix");

            migrationBuilder.CreateIndex(
                name: "IX_api_credentials_ProjectId",
                schema: "platform",
                table: "api_credentials",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_api_credentials_SecretHash",
                schema: "platform",
                table: "api_credentials",
                column: "SecretHash");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_Action",
                schema: "platform",
                table: "audit_events",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_OccurredAt",
                schema: "platform",
                table: "audit_events",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_audit_events_ProjectId_OccurredAt",
                schema: "platform",
                table: "audit_events",
                columns: new[] { "ProjectId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_usage_counters_ProjectId_Metric_Day",
                schema: "platform",
                table: "usage_counters",
                columns: new[] { "ProjectId", "Metric", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_ProjectId",
                schema: "platform",
                table: "webhook_subscriptions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_subscriptions_ProjectId_EnvironmentId_EntitySlug",
                schema: "platform",
                table: "webhook_subscriptions",
                columns: new[] { "ProjectId", "EnvironmentId", "EntitySlug" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_credentials",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "audit_events",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "usage_counters",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "webhook_subscriptions",
                schema: "platform");
        }
    }
}
