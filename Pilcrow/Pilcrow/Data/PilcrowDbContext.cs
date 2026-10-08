using Microsoft.EntityFrameworkCore;
using Pilcrow.Models;

namespace Pilcrow.Data;

public class PilcrowDbContext(DbContextOptions<PilcrowDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();

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
    }
}