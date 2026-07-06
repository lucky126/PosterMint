using PosterMint.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace PosterMint.Infrastructure.Persistence;

public sealed class PosterMintDbContext(DbContextOptions<PosterMintDbContext> options)
    : DbContext(options)
{
    public DbSet<TemplateEntity> Templates => Set<TemplateEntity>();

    public DbSet<TemplateTagEntity> TemplateTags => Set<TemplateTagEntity>();

    public DbSet<PosterSessionEntity> Sessions => Set<PosterSessionEntity>();

    public DbSet<ConfigEntryEntity> ConfigEntries => Set<ConfigEntryEntity>();

    public DbSet<ShopEntity> Shops => Set<ShopEntity>();

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
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
            entity.Property(x => x.CanvasJson).IsRequired();
            entity.Property(x => x.FieldsJson).IsRequired();
            entity.Property(x => x.LayoutJson).IsRequired();
            entity.HasIndex(x => x.TemplateKey).IsUnique();
            entity.HasMany(x => x.Tags)
                .WithOne(x => x.Template)
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Sessions)
                .WithOne(x => x.Template)
                .HasForeignKey(x => x.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TemplateTagEntity>(entity =>
        {
            entity.ToTable("TemplateTags");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Dimension).HasMaxLength(64).IsRequired();
            entity.Property(x => x.TagValue).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.TemplateId, x.Dimension, x.TagValue }).IsUnique();
        });

        modelBuilder.Entity<PosterSessionEntity>(entity =>
        {
            entity.ToTable("Sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SessionKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(64);
            entity.Property(x => x.TemplateSnapshotJson).IsRequired();
            entity.Property(x => x.CurrentFieldsJson).IsRequired();
            entity.Property(x => x.CurrentLayoutJson).IsRequired();
            entity.HasIndex(x => x.SessionKey).IsUnique();
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
            entity.HasIndex(x => x.ShopKey).IsUnique();
        });

        SeedDefaults(modelBuilder);
    }

    private static void SeedDefaults(ModelBuilder modelBuilder)
    {
        var now = new DateTimeOffset(2026, 6, 29, 0, 0, 0, TimeSpan.Zero);

        modelBuilder.Entity<ConfigEntryEntity>().HasData(
            new ConfigEntryEntity
            {
                Id = 1,
                ConfigKey = "Features:Approval",
                ConfigGroup = "Features",
                ConfigValue = "true",
                Description = "审核流程开关",
                IsSecret = false,
                UpdatedAt = now
            },
            new ConfigEntryEntity
            {
                Id = 2,
                ConfigKey = "Features:TagSystem",
                ConfigGroup = "Features",
                ConfigValue = "true",
                Description = "标签推荐开关",
                IsSecret = false,
                UpdatedAt = now
            },
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
