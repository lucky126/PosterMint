using PosterMint.Application.Templates;
using PosterMint.Domain.Entities;
using PosterMint.Domain.Enums;
using PosterMint.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace PosterMint.Infrastructure.Templates;

public sealed class TemplateService(PosterMintDbContext dbContext) : ITemplateService
{
    public async Task<IReadOnlyList<TemplateSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var templates = await dbContext.Templates
            .AsNoTracking()
            .Include(x => x.Tags)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return templates.Select(MapSummary).ToList();
    }

    public async Task<IReadOnlyList<AdminCategorySummaryDto>> GetAdminCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var templates = await dbContext.Templates
            .AsNoTracking()
            .Include(x => x.Tags)
            .ToListAsync(cancellationToken);

        return templates
            .GroupBy(x => x.Scene)
            .OrderBy(x => x.Key)
            .Select(group =>
            {
                var info = DescribeScene(group.Key);
                var tags = group.SelectMany(x => x.Tags)
                    .GroupBy(x => x.TagValue)
                    .OrderByDescending(x => x.Count())
                    .Take(4)
                    .Select(x => x.Key)
                    .ToList();

                return new AdminCategorySummaryDto(
                    info.Key,
                    info.Title,
                    info.Description,
                    group.Count(),
                    group.Count(x => x.Status == TemplateStatus.Approved),
                    group.Count(x => x.Status == TemplateStatus.Pending),
                    tags);
            })
            .ToList();
    }

    public async Task<TemplateDetailDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var template = await dbContext.Templates
            .AsNoTracking()
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return template is null ? null : MapDetail(template);
    }

    public async Task<TemplateDetailDto> CreateAsync(
        CreateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.TemplateKey))
        {
            throw new InvalidOperationException("TemplateKey is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Name is required.");
        }

        var existed = await dbContext.Templates.AnyAsync(
            x => x.TemplateKey == request.TemplateKey,
            cancellationToken);

        if (existed)
        {
            throw new InvalidOperationException($"TemplateKey '{request.TemplateKey}' already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new TemplateEntity
        {
            TemplateKey = request.TemplateKey.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Scene = request.Scene,
            Status = TemplateStatus.Draft,
            CanvasJson = request.Canvas.ToJsonString(),
            FieldsJson = request.Fields.ToJsonString(),
            LayoutJson = request.Layout.ToJsonString(),
            CreatedAt = now,
            UpdatedAt = now,
            Tags = request.Tags.Select(MapTagEntity).ToList()
        };

        dbContext.Templates.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapDetail(entity);
    }

    public async Task<TemplateDetailDto> UpdateAsync(int id, UpdateTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Templates
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Template '{id}' was not found.");

        if (entity.Status == TemplateStatus.Approved)
        {
            entity.Status = TemplateStatus.Draft;
        }

        entity.Name = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.Scene = request.Scene;
        entity.CanvasJson = request.Canvas.ToJsonString();
        entity.FieldsJson = request.Fields.ToJsonString();
        entity.LayoutJson = request.Layout.ToJsonString();
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        entity.Tags.Clear();
        foreach (var tag in request.Tags.Select(MapTagEntity))
        {
            entity.Tags.Add(tag);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(entity);
    }

    public async Task<TemplateChatResultDto> ApplyInstructionAsync(
        int id,
        ApplyTemplateInstructionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Templates
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Template '{id}' was not found.");

        var canvas = ParseJson(entity.CanvasJson) as JsonObject ?? new JsonObject();
        var fields = ParseJson(entity.FieldsJson) as JsonArray ?? new JsonArray();
        var layout = ParseJson(entity.LayoutJson) as JsonArray ?? new JsonArray();
        var touched = new List<string>();
        var warnings = new List<string>();
        var message = request.Message?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new InvalidOperationException("Instruction message is required.");
        }

        ApplyBackgroundInstruction(message, canvas, layout, touched);
        ApplyTitleInstruction(message, fields, layout, touched);
        ApplyPriceInstruction(message, fields, layout, touched);
        ApplyQrInstruction(message, fields, layout, touched);
        ApplyFeatureInstruction(message, fields, layout, touched);
        ApplyStoreInfoInstruction(message, fields, layout, touched);

        if (touched.Count == 0)
        {
            warnings.Add("这次指令没有命中可执行结构。可以试试“背景改成喜庆红”“加二维码区在右下角”“活动价再大一点”“店名移到左上角”。");
        }

        entity.CanvasJson = canvas.ToJsonString();
        entity.FieldsJson = fields.ToJsonString();
        entity.LayoutJson = layout.ToJsonString();
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var detail = MapDetail(entity);
        var reply = touched.Count == 0
            ? "我暂时没有改动模板结构，你可以再具体一点。"
            : $"已完成：{string.Join("、", touched.Distinct())}。预览已经同步更新。";

        return new TemplateChatResultDto(reply, warnings, detail);
    }

    public async Task<TemplateDetailDto> SubmitAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Templates
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Template '{id}' was not found.");

        entity.Status = TemplateStatus.Pending;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(entity);
    }

    public async Task<TemplateDetailDto> ReviewAsync(int id, ReviewTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Templates
            .Include(x => x.Tags)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"Template '{id}' was not found.");

        entity.Status = request.Approved ? TemplateStatus.Approved : TemplateStatus.Rejected;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Comment))
        {
            entity.Description = string.IsNullOrWhiteSpace(entity.Description)
                ? $"审核备注：{request.Comment.Trim()}"
                : $"{entity.Description}\n审核备注：{request.Comment.Trim()}";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(entity);
    }

    private static TemplateSummaryDto MapSummary(TemplateEntity entity) =>
        new(
            entity.Id,
            entity.TemplateKey,
            entity.Name,
            entity.Description,
            entity.Scene,
            entity.Status,
            entity.UpdatedAt,
            entity.Tags.Select(MapTag).ToList());

    private static TemplateDetailDto MapDetail(TemplateEntity entity) =>
        new(
            entity.Id,
            entity.TemplateKey,
            entity.Name,
            entity.Description,
            entity.Scene,
            entity.Status,
            ParseJson(entity.CanvasJson),
            ParseJson(entity.FieldsJson),
            ParseJson(entity.LayoutJson),
            entity.Tags.Select(MapTag).ToList(),
            entity.CreatedAt,
            entity.UpdatedAt);

    private static TemplateTagDto MapTag(TemplateTagEntity entity) =>
        new(entity.Dimension, entity.TagValue, entity.Weight);

    private static TemplateTagEntity MapTagEntity(TemplateTagDto dto) =>
        new()
        {
            Dimension = dto.Dimension,
            TagValue = dto.TagValue,
            Weight = dto.Weight
        };

    private static JsonNode? ParseJson(string json) => JsonNode.Parse(json);

    private static void ApplyBackgroundInstruction(string message, JsonObject canvas, JsonArray layout, List<string> touched)
    {
        string? background = null;

        if (message.Contains("喜庆", StringComparison.OrdinalIgnoreCase) || message.Contains("红", StringComparison.OrdinalIgnoreCase))
        {
            background = "#8d1f1c";
        }
        else if (message.Contains("绿色", StringComparison.OrdinalIgnoreCase) || message.Contains("清新", StringComparison.OrdinalIgnoreCase))
        {
            background = "#214f3d";
        }
        else if (message.Contains("米白", StringComparison.OrdinalIgnoreCase) || message.Contains("奶油", StringComparison.OrdinalIgnoreCase))
        {
            background = "#f7efe0";
        }
        else if (message.Contains("深色", StringComparison.OrdinalIgnoreCase) || message.Contains("高级", StringComparison.OrdinalIgnoreCase))
        {
            background = "#18231f";
        }

        if (background is null)
        {
            return;
        }

        canvas["background"] = background;

        if (layout.Count > 0 && layout[0] is JsonObject first && string.Equals(first["type"]?.GetValue<string>(), "rect", StringComparison.OrdinalIgnoreCase))
        {
            first["fill"] = background;
        }

        touched.Add("背景色");
    }

    private static void ApplyTitleInstruction(string message, JsonArray fields, JsonArray layout, List<string> touched)
    {
        if (!message.Contains("店名", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("标题", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        EnsureField(fields, "storeName", "店铺名称", "text", "新模板标题");
        var node = EnsureTextNode(layout, "storeName", 120, 140, 840, 72, 42, 700, "#fff6df", "center");

        if (message.Contains("左上角", StringComparison.OrdinalIgnoreCase))
        {
            node["x"] = 88;
            node["y"] = 92;
            node["align"] = "left";
            touched.Add("店名移到左上角");
        }

        if (message.Contains("居中", StringComparison.OrdinalIgnoreCase))
        {
            node["x"] = 120;
            node["y"] = 140;
            node["align"] = "center";
            touched.Add("店名居中");
        }

        if (message.Contains("加大", StringComparison.OrdinalIgnoreCase) || message.Contains("大一点", StringComparison.OrdinalIgnoreCase))
        {
            node["fontSize"] = (node["fontSize"]?.GetValue<int?>() ?? 42) + 8;
            touched.Add("店名字号增大");
        }

        if (message.Contains("加粗", StringComparison.OrdinalIgnoreCase))
        {
            node["fontWeight"] = Math.Min((node["fontWeight"]?.GetValue<int?>() ?? 700) + 100, 900);
            touched.Add("店名字重增强");
        }
    }

    private static void ApplyPriceInstruction(string message, JsonArray fields, JsonArray layout, List<string> touched)
    {
        if (!message.Contains("价格", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("活动价", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        EnsureField(fields, "promoPrice", "促销价", "price", "59.9");
        var node = EnsurePriceNode(layout, "promoPrice", 300, 1120, 420, 110, 96, 900, "#ffe071", "center");

        if (message.Contains("右下角", StringComparison.OrdinalIgnoreCase))
        {
            node["x"] = 660;
            node["y"] = 1540;
            node["align"] = "right";
            touched.Add("价格区移到右下角");
        }

        if (message.Contains("竖版", StringComparison.OrdinalIgnoreCase))
        {
            node["w"] = 240;
            node["h"] = 200;
            node["align"] = "center";
            touched.Add("价格区改为更竖向的展示");
        }

        if (message.Contains("加大", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("大一点", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("醒目", StringComparison.OrdinalIgnoreCase))
        {
            node["fontSize"] = (node["fontSize"]?.GetValue<int?>() ?? 96) + 10;
            touched.Add("价格字号增大");
        }
    }

    private static void ApplyQrInstruction(string message, JsonArray fields, JsonArray layout, List<string> touched)
    {
        if (!message.Contains("二维码", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        EnsureField(fields, "qrcode", "二维码", "qrcode");
        var node = EnsureQrNode(layout, "qrcode", 760, 1460, 200, 200);

        if (message.Contains("右下角", StringComparison.OrdinalIgnoreCase))
        {
            node["x"] = 820;
            node["y"] = 1620;
            touched.Add("二维码移到右下角");
        }
        else if (message.Contains("底部", StringComparison.OrdinalIgnoreCase))
        {
            node["y"] = 1600;
            touched.Add("二维码下移");
        }
        else
        {
            touched.Add("二维码区已确保存在");
        }

        if (message.Contains("白色圆角底框", StringComparison.OrdinalIgnoreCase) || message.Contains("底框", StringComparison.OrdinalIgnoreCase))
        {
            EnsureRectBefore(layout, node, 0, "#ffffff", 14, 16);
            touched.Add("二维码底框");
        }
    }

    private static void ApplyFeatureInstruction(string message, JsonArray fields, JsonArray layout, List<string> touched)
    {
        if (!message.Contains("卖点", StringComparison.OrdinalIgnoreCase) &&
            !message.Contains("标签", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        EnsureField(fields, "features", "特色卖点", "features");
        EnsureFeatureRepeat(layout);
        touched.Add("卖点标签区");
    }

    private static void ApplyStoreInfoInstruction(string message, JsonArray fields, JsonArray layout, List<string> touched)
    {
        if (message.Contains("地址", StringComparison.OrdinalIgnoreCase))
        {
            EnsureField(fields, "address", "地址", "text", "XX路88号");
            EnsureTextNode(layout, "address", 112, 1492, 620, 50, 30, 700, "#fff6df", "left");
            touched.Add("地址区");
        }

        if (message.Contains("电话", StringComparison.OrdinalIgnoreCase))
        {
            EnsureField(fields, "phone", "电话", "text", "13800000000");
            EnsureTextNode(layout, "phone", 112, 1562, 480, 46, 30, 700, "#cfe2a3", "left");
            touched.Add("电话区");
        }
    }

    private static JsonObject EnsureTextNode(
        JsonArray layout,
        string field,
        int x,
        int y,
        int w,
        int h,
        int fontSize,
        int fontWeight,
        string color,
        string align)
    {
        var existing = layout.OfType<JsonObject>()
            .FirstOrDefault(node => string.Equals(node["field"]?.GetValue<string>(), field, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(node["type"]?.GetValue<string>(), "text", StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var node = new JsonObject
        {
            ["type"] = "text",
            ["field"] = field,
            ["x"] = x,
            ["y"] = y,
            ["w"] = w,
            ["h"] = h,
            ["fontSize"] = fontSize,
            ["fontWeight"] = fontWeight,
            ["color"] = color,
            ["align"] = align,
            ["maxLines"] = 1
        };

        layout.Add(node);
        return node;
    }

    private static JsonObject EnsurePriceNode(
        JsonArray layout,
        string field,
        int x,
        int y,
        int w,
        int h,
        int fontSize,
        int fontWeight,
        string color,
        string align)
    {
        var existing = layout.OfType<JsonObject>()
            .FirstOrDefault(node => string.Equals(node["field"]?.GetValue<string>(), field, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(node["type"]?.GetValue<string>(), "price", StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var node = new JsonObject
        {
            ["type"] = "price",
            ["field"] = field,
            ["x"] = x,
            ["y"] = y,
            ["w"] = w,
            ["h"] = h,
            ["fontSize"] = fontSize,
            ["fontWeight"] = fontWeight,
            ["color"] = color,
            ["align"] = align,
            ["prefix"] = "￥"
        };

        layout.Add(node);
        return node;
    }

    private static JsonObject EnsureQrNode(JsonArray layout, string field, int x, int y, int w, int h)
    {
        var existing = layout.OfType<JsonObject>()
            .FirstOrDefault(node => string.Equals(node["field"]?.GetValue<string>(), field, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(node["type"]?.GetValue<string>(), "qrcode", StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var node = new JsonObject
        {
            ["type"] = "qrcode",
            ["field"] = field,
            ["x"] = x,
            ["y"] = y,
            ["w"] = w,
            ["h"] = h,
            ["radius"] = 8,
            ["fit"] = "contain",
            ["placeholder"] = "二维码"
        };

        layout.Add(node);
        return node;
    }

    private static void EnsureFeatureRepeat(JsonArray layout)
    {
        var existing = layout.OfType<JsonObject>()
            .FirstOrDefault(node => string.Equals(node["field"]?.GetValue<string>(), "features", StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(node["type"]?.GetValue<string>(), "repeat", StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return;
        }

        layout.Add(new JsonObject
        {
            ["type"] = "repeat",
            ["field"] = "features",
            ["x"] = 106,
            ["y"] = 1244,
            ["w"] = 760,
            ["h"] = 80,
            ["columns"] = 3,
            ["gapX"] = 16,
            ["itemHeight"] = 54,
            ["maxItems"] = 3,
            ["children"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "rect",
                    ["x"] = 0,
                    ["y"] = 0,
                    ["w"] = 232,
                    ["h"] = 54,
                    ["radius"] = 27,
                    ["fill"] = "#1f6b4c"
                },
                new JsonObject
                {
                    ["type"] = "text",
                    ["field"] = "$value",
                    ["x"] = 12,
                    ["y"] = 10,
                    ["w"] = 208,
                    ["h"] = 36,
                    ["fontSize"] = 24,
                    ["fontWeight"] = 800,
                    ["color"] = "#fff8e9",
                    ["align"] = "center",
                    ["maxLines"] = 1
                }
            }
        });
    }

    private static void EnsureRectBefore(JsonArray layout, JsonObject targetNode, int padding, string fill, int radius, int strokeWidth)
    {
        var targetX = targetNode["x"]?.GetValue<int?>() ?? 0;
        var targetY = targetNode["y"]?.GetValue<int?>() ?? 0;
        var targetW = targetNode["w"]?.GetValue<int?>() ?? 0;
        var targetH = targetNode["h"]?.GetValue<int?>() ?? 0;

        var rect = new JsonObject
        {
            ["type"] = "rect",
            ["x"] = targetX - padding,
            ["y"] = targetY - padding,
            ["w"] = targetW + padding * 2,
            ["h"] = targetH + padding * 2,
            ["radius"] = radius,
            ["fill"] = fill,
            ["stroke"] = "#d9d3c7",
            ["strokeWidth"] = strokeWidth > 0 ? 1 : 0
        };

        var index = layout.IndexOf(targetNode);
        if (index < 0)
        {
            layout.Add(rect);
            return;
        }

        layout.Insert(index, rect);
    }

    private static void EnsureField(JsonArray fields, string key, string label, string type, string? defaultValue = null)
    {
        var existing = fields.OfType<JsonObject>()
            .FirstOrDefault(field => string.Equals(field["key"]?.GetValue<string>(), key, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return;
        }

        var fieldNode = new JsonObject
        {
            ["key"] = key,
            ["label"] = label,
            ["type"] = type
        };

        if (defaultValue is not null)
        {
            fieldNode["default"] = defaultValue;
        }

        fields.Add(fieldNode);
    }

    private static (string Key, string Title, string Description) DescribeScene(TemplateSceneType scene) =>
        scene switch
        {
            TemplateSceneType.Menu => ("menu", "菜单模板", "适合多菜品展示和套餐菜单"),
            TemplateSceneType.StorePromo => ("store-promo", "门店宣传", "适合门头、简介、地址和二维码"),
            TemplateSceneType.Festival => ("festival", "节庆活动", "适合节日营销和阶段活动"),
            TemplateSceneType.Custom => ("custom", "自定义场景", "用于扩展行业和特殊场景"),
            _ => ("single-dish", "单菜品促销", "适合主推单品和活动价格")
        };
}
