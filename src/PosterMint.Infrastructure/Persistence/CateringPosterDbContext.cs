using PosterMint.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace PosterMint.Infrastructure.Persistence;

public sealed class PosterMintDbContext(DbContextOptions<PosterMintDbContext> options)
    : DbContext(options)
{
    public DbSet<TemplateEntity> Templates => Set<TemplateEntity>();

    public DbSet<ConfigEntryEntity> ConfigEntries => Set<ConfigEntryEntity>();

    public DbSet<ShopEntity> Shops => Set<ShopEntity>();

    public DbSet<ShopLoginLogEntity> ShopLoginLogs => Set<ShopLoginLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TemplateEntity>(entity =>
        {
            entity.ToTable("Templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TemplateKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Scene).HasConversion<string>().HasMaxLength(64);
            entity.Property(x => x.Ownership).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ShopId);
            entity.Property(x => x.Psp).IsRequired();
            entity.Property(x => x.SchemaVersion).HasMaxLength(32).IsRequired();
            entity.Property(x => x.SlotCount);
            entity.Property(x => x.PreviewImage).HasMaxLength(512);

            entity.HasIndex(x => x.TemplateKey).IsUnique();
            entity.HasIndex(x => new { x.Ownership, x.ShopId });
            entity.HasIndex(x => x.Scene);

            entity.HasOne(x => x.Shop)
                .WithMany()
                .HasForeignKey(x => x.ShopId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ConfigEntryEntity>(entity =>
        {
            entity.ToTable("ConfigEntries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ConfigKey).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ConfigGroup).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ConfigValue).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.HasIndex(x => x.ConfigKey).IsUnique();
        });

        modelBuilder.Entity<ShopEntity>(entity =>
        {
            entity.ToTable("Shops");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ShopKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.ContactName).HasMaxLength(64);
            entity.Property(x => x.ContactPhone).HasMaxLength(32);
            entity.Property(x => x.Address).HasMaxLength(500);
            entity.Property(x => x.Industry).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Remark).HasMaxLength(1000);

            entity.Property(x => x.Username).HasMaxLength(64);
            entity.Property(x => x.PasswordHash).HasMaxLength(256);
            entity.Property(x => x.PasswordSalt).HasMaxLength(128);
            entity.Property(x => x.PasswordHashVersion);
            entity.Property(x => x.LastLoginAt);

            entity.HasIndex(x => x.ShopKey).IsUnique();
            entity.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<ShopLoginLogEntity>(entity =>
        {
            entity.ToTable("ShopLoginLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AttemptedUsername).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Result).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Ip).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);

            entity.HasOne(x => x.Shop)
                .WithMany()
                .HasForeignKey(x => x.ShopId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.ShopId, x.OccurredAt });
            entity.HasIndex(x => x.AttemptedUsername);
        });

        // 内置配置项（AI 相关开关；老的 Approval/TagSystem 已随 v1 一起废弃）
        var now = new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero);
        modelBuilder.Entity<ConfigEntryEntity>().HasData(
            new ConfigEntryEntity
            {
                Id = 3,
                ConfigKey = "LlmText:Provider",
                ConfigGroup = "AI",
                ConfigValue = "DashScope",
                Description = "文本模型提供方",
                IsSecret = false,
                UpdatedAt = now
            });
    }
}
