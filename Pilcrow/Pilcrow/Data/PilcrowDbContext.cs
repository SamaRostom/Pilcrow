using Microsoft.EntityFrameworkCore;
using Pilcrow.Models;

namespace Pilcrow.Data;

public class PilcrowDbContext(DbContextOptions<PilcrowDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();
    public DbSet<RenderJob> RenderJobs => Set<RenderJob>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("citext");

        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.Property(x => x.Email).HasColumnType("citext").IsRequired();
            e.HasIndex(x => x.Email)
             .IsUnique()
             .HasFilter("deleted_at IS NULL");
        });

        b.Entity<Template>(e =>
        {
            e.ToTable("templates");
            e.Property(x => x.Name).IsRequired();
            e.HasOne(x => x.User)
             .WithMany(u => u.Templates)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.UpdatedAt })
             .HasFilter("deleted_at IS NULL");
        });

        b.Entity<TemplateVersion>(e =>
        {
            e.ToTable("template_versions");
            e.Property(x => x.Doc).HasColumnType("jsonb").IsRequired();
            e.HasOne(x => x.Template)
             .WithMany(t => t.Versions)
             .HasForeignKey(x => x.TemplateId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.TemplateId, x.VersionNo }).IsUnique();
        });

        b.Entity<RenderJob>(e =>
        {
            e.ToTable("render_jobs", t => t.HasCheckConstraint(
                "ck_render_jobs_source_shape",
                "(source_type = 'template' AND template_version_id IS NOT NULL AND upload_id IS NULL) " +
                "OR (source_type = 'upload' AND upload_id IS NOT NULL AND template_version_id IS NULL)"));

            e.Property(x => x.SourceType)
             .HasConversion(v => v.ToString().ToLowerInvariant(),
                            v => Enum.Parse<JobSource>(v, true));

            e.Property(x => x.Status)
             .HasConversion(v => v.ToString().ToLowerInvariant(),
                            v => Enum.Parse<JobStatus>(v, true));
            e.Property(x => x.DataPayload).HasColumnType("jsonb");

            e.HasIndex(x => new { x.Priority, x.QueuedAt })
             .HasFilter("status = 'queued'")
             .HasDatabaseName("ix_render_jobs_claim");

            e.HasIndex(x => x.LeaseExpiresAt)
             .HasFilter("status = 'running'")
             .HasDatabaseName("ix_render_jobs_stale");

            e.HasIndex(x => new { x.UserId, x.IdempotencyKey })
             .IsUnique()
             .HasFilter("idempotency_key IS NOT NULL");

            e.HasIndex(x => x.ExpiresAt)
             .HasFilter("result_blob_key IS NOT NULL");
        });
        
    }
}