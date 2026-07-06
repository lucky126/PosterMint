using PosterMint.Domain.Enums;

namespace PosterMint.Application.Templates;

public sealed record TemplateSummaryDto(
    int Id,
    string TemplateKey,
    string Name,
    string? Description,
    TemplateSceneType Scene,
    TemplateStatus Status,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<TemplateTagDto> Tags);
