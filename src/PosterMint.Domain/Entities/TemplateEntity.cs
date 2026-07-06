using PosterMint.Domain.Enums;

namespace PosterMint.Domain.Entities;

public sealed class TemplateEntity
{
    public int Id { get; set; }

    public string TemplateKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>行业分类；仅当 Ownership=Category 时用于分组。</summary>
    public TemplateSceneType Scene { get; set; } = TemplateSceneType.Custom;

    /// <summary>模板归属：Shop 商铺专属 / Category 行业通用。</summary>
    public TemplateOwnership Ownership { get; set; } = TemplateOwnership.Category;

    /// <summary>Ownership=Shop 时指向 ShopEntity；Category 时为 null。</summary>
    public int? ShopId { get; set; }

    /// <summary>PSP 完整 JSON（CC 工具产出，粘进 PC 后台入库）。</summary>
    public string Psp { get; set; } = "{}";

    /// <summary>PSP schema 版本号，如 "PSP-v1"。</summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>从 PSP 解析出的 slot 总数，冗余存，便于列表页展示。</summary>
    public int SlotCount { get; set; }

    /// <summary>预览图 URL / 相对路径；CC 制作时产出的示意图。</summary>
    public string? PreviewImage { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ShopEntity? Shop { get; set; }
}
