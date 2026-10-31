using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morph.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FilesJobsMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "file_buckets",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PhysicalBucket = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MaxUploadBytes = table.Column<long>(type: "bigint", nullable: false),
                    AllowedContentTypesJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_file_buckets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stored_files",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BucketId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    ChecksumSha256 = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stored_files", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_file_buckets_ProjectId",
                schema: "platform",
                table: "file_buckets",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_file_buckets_ProjectId_EnvironmentId_Slug",
                schema: "platform",
                table: "file_buckets",
                columns: new[] { "ProjectId", "EnvironmentId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_Status_available_at",
                schema: "platform",
                table: "outbox_messages",
                columns: new[] { "Status", "available_at" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_Type",
                schema: "platform",
                table: "outbox_messages",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_stored_files_BucketId",
                schema: "platform",
                table: "stored_files",
                column: "BucketId");

            migrationBuilder.CreateIndex(
                name: "IX_stored_files_DeletedAt",
                schema: "platform",
                table: "stored_files",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_stored_files_EnvironmentId",
                schema: "platform",
                table: "stored_files",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_stored_files_ObjectKey",
                schema: "platform",
                table: "stored_files",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stored_files_ProjectId",
                schema: "platform",
                table: "stored_files",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "file_buckets",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "stored_files",
                schema: "platform");
        }
    }
}
