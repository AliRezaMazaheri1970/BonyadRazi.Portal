using BonyadRazi.Portal.Infrastructure.Audit.Entities;
using Microsoft.EntityFrameworkCore;

namespace BonyadRazi.Portal.Infrastructure.Persistence;

public sealed class RasfPortalDbContext : DbContext
{
    public RasfPortalDbContext(DbContextOptions<RasfPortalDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserActionLog> UserActionLogs => Set<UserActionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserActionLog>(e =>
        {
            e.ToTable("UserActionLogs");
            e.HasKey(x => x.Id);

            e.Property(x => x.Utc).IsRequired();

            e.Property(x => x.ActionType)
                .HasMaxLength(64)
                .IsRequired();

            e.Property(x => x.MetadataJson)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            e.Property(x => x.TraceId).HasMaxLength(128);
            e.Property(x => x.Path).HasMaxLength(2048);
            e.Property(x => x.Method).HasMaxLength(16);
            e.Property(x => x.RemoteIp).HasMaxLength(64);
            e.Property(x => x.UserAgent).HasMaxLength(512);
            e.Property(x => x.Reason).HasMaxLength(128);

            e.HasIndex(x => x.Utc);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.CompanyCode);
            e.HasIndex(x => x.ActionType);
            e.HasIndex(x => x.StatusCode);
        });
    }
}
