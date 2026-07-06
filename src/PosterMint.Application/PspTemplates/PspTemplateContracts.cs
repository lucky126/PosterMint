using System.Text.Json.Nodes;
using PosterMint.Domain.Enums;

namespace PosterMint.Application.PspTemplates;

/// <summary>PSP 模板列表项。</summary>
public sealed record PspTemplateSummaryDto(
    int Id,
    string TemplateKey,
    string Name,
    string? Description,
    TemplateOwnership Ownership,
    int? ShopId,
    string? ShopName,
    TemplateSceneType Scene,
    string SchemaVersion,
    int SlotCount,
    string? PreviewImage,
    DateTimeOffset UpdatedAt);

/// <summary>PSP 模板详情，含完整 JSON。</summary>
public sealed record PspTemplateDetailDto(
    int Id,
    string TemplateKey,
    string Name,
    string? Description,
    TemplateOwnership Ownership,
    int? ShopId,
    string? ShopName,
    TemplateSceneType Scene,
    string SchemaVersion,
    int SlotCount,
    string? PreviewImage,
    JsonNode Psp,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>入库请求。TemplateKey 可选：留空则自动从 PSP.id 取；再没有就服务端生成。</summary>
public sealed class PspTemplateImportRequest
{
    public string? TemplateKey { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public TemplateOwnership Ownership { get; init; } = TemplateOwnership.Category;

    public int? ShopId { get; init; }

    public TemplateSceneType Scene { get; init; } = TemplateSceneType.Custom;

    public string? PreviewImage { get; init; }

    /// <summary>PSP 完整 JSON（对象）。</summary>
    public JsonObject Psp { get; init; } = new();
}

/// <summary>校验结果。IsValid=false 时不入库；SlotCount 只在成功时填。</summary>
public sealed record PspValidationResult(
    bool IsValid,
    string SchemaVersion,
    int SlotCount,
    IReadOnlyList<string> Errors);
