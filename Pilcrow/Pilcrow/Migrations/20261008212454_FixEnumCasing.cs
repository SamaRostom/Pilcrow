using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pilcrow.Migrations
{
    /// <inheritdoc />
    public partial class FixEnumCasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_render_jobs_claim",
                table: "render_jobs");

            migrationBuilder.DropIndex(
                name: "ix_render_jobs_stale",
                table: "render_jobs");

            migrationBuilder.CreateIndex(
                name: "ix_render_jobs_claim",
                table: "render_jobs",
                columns: new[] { "priority", "queued_at" },
                filter: "status = 'queued'");

            migrationBuilder.CreateIndex(
                name: "ix_render_jobs_stale",
                table: "render_jobs",
                column: "lease_expires_at",
                filter: "status = 'running'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_render_jobs_claim",
                table: "render_jobs");

            migrationBuilder.DropIndex(
                name: "ix_render_jobs_stale",
                table: "render_jobs");

            migrationBuilder.CreateIndex(
                name: "ix_render_jobs_claim",
                table: "render_jobs",
                columns: new[] { "priority", "queued_at" },
                filter: "status = 'Queued'");

            migrationBuilder.CreateIndex(
                name: "ix_render_jobs_stale",
                table: "render_jobs",
                column: "lease_expires_at",
                filter: "status = 'Running'");
        }
    }
}
