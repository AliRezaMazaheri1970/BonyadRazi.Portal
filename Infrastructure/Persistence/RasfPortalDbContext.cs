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

    // ✅ NEW
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

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

        modelBuilder.Entity<UserAccount>(e =>
        {
            e.ToTable("UserAccounts");
            e.HasKey(x => x.Id);

            e.Property(x => x.Username).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Username).IsUnique();

            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.PasswordSalt).IsRequired();

            e.Property(x => x.PasswordIterations).IsRequired();

            e.Property(x => x.Roles).HasMaxLength(200);
            e.Property(x => x.IsActive).IsRequired();

            e.HasIndex(x => x.CompanyCode);
            e.HasIndex(x => x.IsActive);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");

            e.HasKey(x => x.Id);

            e.Property(x => x.UserAccountId).IsRequired();

            e.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(64); // SHA-256 hex length

            e.Property(x => x.CreatedUtc).IsRequired();
            e.Property(x => x.ExpiresUtc).IsRequired();

            e.Property(x => x.RevokedUtc);
            e.Property(x => x.RevokeReason).HasMaxLength(256);

            e.Property(x => x.ReplacedByTokenId);

            e.HasIndex(x => x.UserAccountId);
            e.HasIndex(x => x.TokenHash).IsUnique();

            e.HasOne<UserAccount>()
             .WithMany()
             .HasForeignKey(x => x.UserAccountId)
             .OnDelete(DeleteBehavior.Restrict);
        });
    }
}