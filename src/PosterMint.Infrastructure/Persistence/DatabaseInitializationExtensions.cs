using PosterMint.Domain.Entities;
using PosterMint.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;

namespace PosterMint.Infrastructure.Persistence;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PosterMintDbContext>();

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        // 兼容老库：EnsureCreated 只在库不存在时建表；对已有库中的新增表需要手动补建
        await EnsureShopsTableAsync(dbContext, cancellationToken);

        foreach (var template in LoadSeedTemplates())
        {
            var exists = await dbContext.Templates.AnyAsync(x => x.TemplateKey == template.TemplateKey, cancellationToken);
            if (!exists)
            {
                dbContext.Templates.Add(template);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureShopsTableAsync(PosterMintDbContext dbContext, CancellationToken cancellationToken)
    {
        // SQLite：Shops 表不存在则创建（保持与 OnModelCreating 中的映射一致）
        const string createSql = """
            CREATE TABLE IF NOT EXISTS "Shops" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Shops" PRIMARY KEY AUTOINCREMENT,
                "ShopKey" TEXT NOT NULL,
                "Name" TEXT NOT NULL,
                "ContactName" TEXT NULL,
                "ContactPhone" TEXT NULL,
                "Address" TEXT NULL,
                "Industry" TEXT NOT NULL,
                "Status" TEXT NOT NULL,
                "Remark" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Shops_ShopKey" ON "Shops" ("ShopKey");
            """;
        await dbContext.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

    private static IEnumerable<TemplateEntity> LoadSeedTemplates()
    {
        var root = FindProjectRoot(AppContext.BaseDirectory);
        var templateDirectory = Path.Combine(root, "data", "templates");
        if (!Directory.Exists(templateDirectory))
        {
            return [CreateFallbackTemplate()];
        }

        var templates = new List<TemplateEntity>();
        foreach (var file in Directory.GetFiles(templateDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            var json = File.ReadAllText(file);
            var node = JsonNode.Parse(json) as JsonObject;
            if (node is null)
            {
                continue;
            }

            templates.Add(MapTemplate(node));
        }

        return templates.Count == 0 ? [CreateFallbackTemplate()] : templates;
    }

    private static TemplateEntity MapTemplate(JsonObject node)
    {
        var now = DateTimeOffset.UtcNow;
        var key = node["id"]?.GetValue<string>() ?? $"template-{Guid.NewGuid():N}";
        var scene = MapScene(node["scene"]?.GetValue<string>());
        var canvas = node["canvas"]?.DeepClone() as JsonObject ?? new JsonObject();
        var fields = node["fields"]?.DeepClone() as JsonArray ?? [];
        var layout = node["layout"]?.DeepClone() as JsonArray ?? [];

        AddSmartDefaults(fields);

        return new TemplateEntity
        {
            TemplateKey = key,
            Name = node["name"]?.GetValue<string>() ?? key,
            Description = node["description"]?.GetValue<string>(),
            Scene = scene,
            Status = TemplateStatus.Draft,
            CanvasJson = canvas.ToJsonString(),
            FieldsJson = fields.ToJsonString(),
            LayoutJson = layout.ToJsonString(),
            CreatedAt = now,
            UpdatedAt = now,
            Tags = BuildDefaultTags(scene)
        };
    }

    private static void AddSmartDefaults(JsonArray fields)
    {
        foreach (var item in fields)
        {
            if (item is not JsonObject fieldObject)
            {
                continue;
            }

            var type = fieldObject["type"]?.GetValue<string>();
            if (type == "items" && fieldObject["default"] is null)
            {
                fieldObject["default"] = new JsonArray
                {
                    new JsonObject { ["name"] = "招牌小炒肉", ["price"] = "28" },
                    new JsonObject { ["name"] = "酸菜鱼", ["price"] = "58" },
                    new JsonObject { ["name"] = "宫保鸡丁", ["price"] = "26" },
                    new JsonObject { ["name"] = "青椒牛柳", ["price"] = "36" }
                };
            }
        }
    }

    private static List<TemplateTagEntity> BuildDefaultTags(TemplateSceneType scene)
    {
        return scene switch
        {
            TemplateSceneType.SingleDish =>
            [
                new TemplateTagEntity { Dimension = "product", TagValue = "招牌必点", Weight = 1.0d },
                new TemplateTagEntity { Dimension = "marketing", TagValue = "限时秒杀", Weight = 1.0d }
            ],
            TemplateSceneType.Menu =>
            [
                new TemplateTagEntity { Dimension = "product", TagValue = "店铺爆款", Weight = 1.0d },
                new TemplateTagEntity { Dimension = "crowd", TagValue = "多人聚餐", Weight = 1.0d }
            ],
            TemplateSceneType.StorePromo =>
            [
                new TemplateTagEntity { Dimension = "marketing", TagValue = "新客福利", Weight = 1.0d },
                new TemplateTagEntity { Dimension = "shopType", TagValue = "中式菜系", Weight = 1.0d }
            ],
            _ => []
        };
    }

    private static TemplateSceneType MapScene(string? scene) =>
        scene?.Trim().ToLowerInvariant() switch
        {
            "single_dish" => TemplateSceneType.SingleDish,
            "menu" => TemplateSceneType.Menu,
            "store_promo" => TemplateSceneType.StorePromo,
            "festival" => TemplateSceneType.Festival,
            _ => TemplateSceneType.Custom
        };

    private static string FindProjectRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            if (current.GetFiles("PosterMint.sln").Any() || current.GetFiles("PosterMint.slnx").Any())
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static TemplateEntity CreateFallbackTemplate()
    {
        var node = new JsonObject
        {
            ["id"] = "single-dish-red",
            ["name"] = "单菜品红色促销模板",
            ["scene"] = "single_dish",
            ["description"] = "回退种子模板",
            ["canvas"] = new JsonObject
            {
                ["width"] = 1080,
                ["height"] = 1920,
                ["background"] = "#841d18"
            },
            ["fields"] = new JsonArray
            {
                new JsonObject { ["key"] = "storeName", ["label"] = "店铺名称", ["type"] = "text", ["default"] = "今日招牌" },
                new JsonObject { ["key"] = "dishName", ["label"] = "菜品名称", ["type"] = "text", ["default"] = "香辣牛肉煲" },
                new JsonObject { ["key"] = "promoPrice", ["label"] = "促销价", ["type"] = "price", ["default"] = "59.9" }
            },
            ["layout"] = new JsonArray
            {
                new JsonObject { ["type"] = "rect", ["x"] = 0, ["y"] = 0, ["w"] = 1080, ["h"] = 1920, ["fill"] = "#841d18" },
                new JsonObject { ["type"] = "text", ["field"] = "storeName", ["x"] = 120, ["y"] = 200, ["w"] = 840, ["h"] = 60, ["fontSize"] = 42, ["fontWeight"] = 700, ["color"] = "#fff5e5", ["align"] = "center" },
                new JsonObject { ["type"] = "text", ["field"] = "dishName", ["x"] = 120, ["y"] = 820, ["w"] = 840, ["h"] = 100, ["fontSize"] = 74, ["fontWeight"] = 900, ["color"] = "#fff5e5", ["align"] = "center" },
                new JsonObject { ["type"] = "price", ["field"] = "promoPrice", ["x"] = 300, ["y"] = 1040, ["w"] = 420, ["h"] = 100, ["fontSize"] = 96, ["fontWeight"] = 900, ["color"] = "#ffe071", ["align"] = "center" }
            }
        };

        return MapTemplate(node);
    }
}
