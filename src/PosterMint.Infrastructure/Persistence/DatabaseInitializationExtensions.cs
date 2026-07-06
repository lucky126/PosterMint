using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace PosterMint.Infrastructure.Persistence;

/// <summary>
/// 数据库初始化。
///
/// 迁移策略与 v2 一致：SQLite + 手写 ALTER TABLE，用 PRAGMA table_info 判断幂等。
/// 不用 EF Migrations（避免混两套体系）。
///
/// 本轮（2026-07-06）清理 v1 遗产：
///   - 删表：Sessions、TemplateTags
///   - 删 Templates 表内老列：Status / CanvasJson / FieldsJson / LayoutJson
///   - 删 ConfigEntries 里 v1 的 Approval / TagSystem 两条 seed（新库不再种）
/// </summary>
public static class DatabaseInitializationExtensions
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PosterMintDbContext>();

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        // 兼容老库：EnsureCreated 只在库不存在时建表；对已有库的增量变更需要手动处理。
        await EnsureShopsTableAsync(dbContext, cancellationToken);
        await EnsureShopAuthColumnsAsync(dbContext, cancellationToken);
        await EnsureShopLoginLogsTableAsync(dbContext, cancellationToken);
        await EnsureTemplatePspColumnsAsync(dbContext, cancellationToken);
        await DropV1LegacyAsync(dbContext, cancellationToken);   // 本轮新增

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// PSP v1 迁移：Templates 表补 6 列（Ownership/ShopId/Psp/SchemaVersion/SlotCount/PreviewImage）+ 2 索引。
    /// </summary>
    private static async Task EnsureTemplatePspColumnsAsync(PosterMintDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(\"Templates\");";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        if (existingColumns.Count == 0)
        {
            return;
        }

        var alters = new List<string>();
        if (!existingColumns.Contains("Ownership"))
        {
            alters.Add("ALTER TABLE \"Templates\" ADD COLUMN \"Ownership\" TEXT NOT NULL DEFAULT 'Category';");
        }
        if (!existingColumns.Contains("ShopId"))
        {
            alters.Add("ALTER TABLE \"Templates\" ADD COLUMN \"ShopId\" INTEGER NULL;");
        }
        if (!existingColumns.Contains("Psp"))
        {
            alters.Add("ALTER TABLE \"Templates\" ADD COLUMN \"Psp\" TEXT NULL;");
        }
        if (!existingColumns.Contains("SchemaVersion"))
        {
            alters.Add("ALTER TABLE \"Templates\" ADD COLUMN \"SchemaVersion\" TEXT NULL;");
        }
        if (!existingColumns.Contains("SlotCount"))
        {
            alters.Add("ALTER TABLE \"Templates\" ADD COLUMN \"SlotCount\" INTEGER NULL;");
        }
        if (!existingColumns.Contains("PreviewImage"))
        {
            alters.Add("ALTER TABLE \"Templates\" ADD COLUMN \"PreviewImage\" TEXT NULL;");
        }

        alters.Add("CREATE INDEX IF NOT EXISTS \"IX_Templates_Ownership_ShopId\" ON \"Templates\" (\"Ownership\", \"ShopId\");");
        alters.Add("CREATE INDEX IF NOT EXISTS \"IX_Templates_Scene\" ON \"Templates\" (\"Scene\");");

        foreach (var sql in alters)
        {
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }

    /// <summary>
    /// v2 商铺登录字段迁移：Shops 表补 5 列 + Username 唯一索引。
    /// </summary>
    private static async Task EnsureShopAuthColumnsAsync(PosterMintDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(\"Shops\");";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        if (existingColumns.Count == 0)
        {
            return;
        }

        var alters = new List<string>();
        if (!existingColumns.Contains("Username"))
        {
            alters.Add("ALTER TABLE \"Shops\" ADD COLUMN \"Username\" TEXT NULL;");
        }
        if (!existingColumns.Contains("PasswordHash"))
        {
            alters.Add("ALTER TABLE \"Shops\" ADD COLUMN \"PasswordHash\" TEXT NULL;");
        }
        if (!existingColumns.Contains("PasswordSalt"))
        {
            alters.Add("ALTER TABLE \"Shops\" ADD COLUMN \"PasswordSalt\" TEXT NULL;");
        }
        if (!existingColumns.Contains("PasswordHashVersion"))
        {
            alters.Add("ALTER TABLE \"Shops\" ADD COLUMN \"PasswordHashVersion\" INTEGER NOT NULL DEFAULT 0;");
        }
        if (!existingColumns.Contains("LastLoginAt"))
        {
            alters.Add("ALTER TABLE \"Shops\" ADD COLUMN \"LastLoginAt\" TEXT NULL;");
        }

        alters.Add("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Shops_Username\" ON \"Shops\" (\"Username\");");

        foreach (var sql in alters)
        {
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
    }

    /// <summary>
    /// v2 商铺登录日志表：不存在则整表创建 + 建索引，幂等。
    /// </summary>
    private static async Task EnsureShopLoginLogsTableAsync(PosterMintDbContext dbContext, CancellationToken cancellationToken)
    {
        const string createSql = """
            CREATE TABLE IF NOT EXISTS "ShopLoginLogs" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_ShopLoginLogs" PRIMARY KEY AUTOINCREMENT,
                "ShopId" INTEGER NULL,
                "AttemptedUsername" TEXT NOT NULL,
                "Result" TEXT NOT NULL,
                "Ip" TEXT NULL,
                "UserAgent" TEXT NULL,
                "OccurredAt" TEXT NOT NULL,
                CONSTRAINT "FK_ShopLoginLogs_Shops_ShopId" FOREIGN KEY ("ShopId") REFERENCES "Shops" ("Id") ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_ShopLoginLogs_ShopId_OccurredAt" ON "ShopLoginLogs" ("ShopId", "OccurredAt");
            CREATE INDEX IF NOT EXISTS "IX_ShopLoginLogs_AttemptedUsername" ON "ShopLoginLogs" ("AttemptedUsername");
            """;
        await dbContext.Database.ExecuteSqlRawAsync(createSql, cancellationToken);
    }

    private static async Task EnsureShopsTableAsync(PosterMintDbContext dbContext, CancellationToken cancellationToken)
    {
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

    /// <summary>
    /// 清理 v1 遗产（幂等）：
    ///   1) DROP Sessions + TemplateTags 两张老表
    ///   2) DELETE Templates 里 Psp IS NULL 的老行（v1 模板，已无 UI 可用）
    ///   3) 用 CREATE TABLE + INSERT + RENAME 三步法从 Templates 里移除 Status/CanvasJson/FieldsJson/LayoutJson 四列
    ///   4) DELETE ConfigEntries 里 v1 的 Features:Approval / Features:TagSystem 两条
    /// </summary>
    private static async Task DropV1LegacyAsync(PosterMintDbContext dbContext, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        // 1) 删老表
        await dbContext.Database.ExecuteSqlRawAsync(@"DROP TABLE IF EXISTS ""Sessions"";", cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(@"DROP TABLE IF EXISTS ""TemplateTags"";", cancellationToken);

        // 2) 删 v1 模板记录（Psp 为空 = 老 v1 模板；这些没有 PSP JSON 无法在 v2 UI 里展示）
        await dbContext.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""Templates"" WHERE ""Psp"" IS NULL OR ""Psp"" = '';",
            cancellationToken);

        // 3) 判断是否需要重建 Templates 表以去掉老列
        var templateColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(\"Templates\");";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                templateColumns.Add(reader.GetString(1));
            }
        }

        var hasLegacyColumns =
            templateColumns.Contains("CanvasJson") ||
            templateColumns.Contains("FieldsJson") ||
            templateColumns.Contains("LayoutJson") ||
            templateColumns.Contains("Status");

        if (hasLegacyColumns)
        {
            // SQLite 不支持 DROP COLUMN（3.35 之前），且我们希望顺带把 NOT NULL 约束刷新，走三步法。
            const string rebuild = @"
CREATE TABLE ""Templates_new"" (
    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_Templates"" PRIMARY KEY AUTOINCREMENT,
    ""TemplateKey"" TEXT NOT NULL,
    ""Name"" TEXT NOT NULL,
    ""Description"" TEXT NULL,
    ""Scene"" TEXT NOT NULL,
    ""Ownership"" TEXT NOT NULL DEFAULT 'Category',
    ""ShopId"" INTEGER NULL,
    ""Psp"" TEXT NOT NULL,
    ""SchemaVersion"" TEXT NOT NULL DEFAULT 'PSP-v1',
    ""SlotCount"" INTEGER NOT NULL DEFAULT 0,
    ""PreviewImage"" TEXT NULL,
    ""CreatedAt"" TEXT NOT NULL,
    ""UpdatedAt"" TEXT NOT NULL,
    CONSTRAINT ""FK_Templates_Shops_ShopId"" FOREIGN KEY (""ShopId"") REFERENCES ""Shops"" (""Id"") ON DELETE SET NULL
);
INSERT INTO ""Templates_new"" (""Id"",""TemplateKey"",""Name"",""Description"",""Scene"",""Ownership"",""ShopId"",""Psp"",""SchemaVersion"",""SlotCount"",""PreviewImage"",""CreatedAt"",""UpdatedAt"")
SELECT
    ""Id"",""TemplateKey"",""Name"",""Description"",""Scene"",
    COALESCE(""Ownership"",'Category'),
    ""ShopId"",
    ""Psp"",
    COALESCE(""SchemaVersion"",'PSP-v1'),
    COALESCE(""SlotCount"",0),
    ""PreviewImage"",""CreatedAt"",""UpdatedAt""
FROM ""Templates"";
DROP TABLE ""Templates"";
ALTER TABLE ""Templates_new"" RENAME TO ""Templates"";
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Templates_TemplateKey"" ON ""Templates"" (""TemplateKey"");
CREATE INDEX IF NOT EXISTS ""IX_Templates_Ownership_ShopId"" ON ""Templates"" (""Ownership"",""ShopId"");
CREATE INDEX IF NOT EXISTS ""IX_Templates_Scene"" ON ""Templates"" (""Scene"");
";
            await dbContext.Database.ExecuteSqlRawAsync(rebuild, cancellationToken);
        }

        // 4) 清 v1 配置项
        await dbContext.Database.ExecuteSqlRawAsync(
            @"DELETE FROM ""ConfigEntries"" WHERE ""ConfigKey"" IN ('Features:Approval', 'Features:TagSystem');",
            cancellationToken);
    }
}
