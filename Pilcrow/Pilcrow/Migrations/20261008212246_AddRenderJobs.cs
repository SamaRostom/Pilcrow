using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pilcrow.Migrations
{
    /// <inheritdoc />
    public partial class AddRenderJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "render_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "text", nullable: false),
                    template_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    upload_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data_payload = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    locked_by = table.Column<string>(type: "text", nullable: true),
                    lease_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    idempotency_key = table.Column<string>(type: "text", nullable: true),
                    priority = table.Column<short>(type: "smallint", nullable: false),
                    result_blob_key = table.Column<string>(type: "text", nullable: true),
                    result_bytes = table.Column<long>(type: "bigint", nullable: true),
                    error_code = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_render_jobs", x => x.id);
                    table.CheckConstraint("ck_render_jobs_source_shape", "(source_type = 'template' AND template_version_id IS NOT NULL AND upload_id IS NULL) OR (source_type = 'upload' AND upload_id IS NOT NULL AND template_version_id IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "ix_render_jobs_claim",
                table: "render_jobs",
                columns: new[] { "priority", "queued_at" },
                filter: "status = 'Queued'");

            migrationBuilder.CreateIndex(
                name: "ix_render_jobs_expires_at",
                table: "render_jobs",
                column: "expires_at",
                filter: "result_blob_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_render_jobs_stale",
                table: "render_jobs",
                column: "lease_expires_at",
                filter: "status = 'Running'");

            migrationBuilder.CreateIndex(
                name: "ix_render_jobs_user_id_idempotency_key",
                table: "render_jobs",
                columns: new[] { "user_id", "idempotency_key" },
                unique: true,
                filter: "idempotency_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "render_jobs");
        }
    }
}
