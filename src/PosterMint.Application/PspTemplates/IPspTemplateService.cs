using System.Text.Json.Nodes;

namespace PosterMint.Application.PspTemplates;

public interface IPspTemplateService
{
    Task<IReadOnlyList<PspTemplateSummaryDto>> ListAsync(
        PspTemplateFilter filter,
        CancellationToken cancellationToken = default);

    Task<PspTemplateDetailDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>纯校验，不入库。用于 PC 后台"实时校验"。</summary>
    PspValidationResult Validate(JsonNode? psp);

    /// <summary>校验并入库。校验失败抛 <see cref="PspValidationException"/>。</summary>
    Task<PspTemplateDetailDto> ImportAsync(
        PspTemplateImportRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>列表筛选。所有字段可空 = 不过滤。</summary>
public sealed record PspTemplateFilter(
    Domain.Enums.TemplateOwnership? Ownership = null,
    int? ShopId = null,
    Domain.Enums.TemplateSceneType? Scene = null,
    string? Keyword = null);

/// <summary>PSP JSON 校验失败：把 errors 结构化抛出，让 API 层能返回 400 + 错误列表。</summary>
public sealed class PspValidationException(PspValidationResult result)
    : InvalidOperationException("PSP validation failed: " + string.Join(" | ", result.Errors))
{
    public PspValidationResult Result { get; } = result;
}
