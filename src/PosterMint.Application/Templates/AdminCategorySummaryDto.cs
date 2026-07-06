namespace PosterMint.Application.Templates;

public sealed record AdminCategorySummaryDto(
    string Key,
    string Title,
    string Description,
    int TemplateCount,
    int ApprovedCount,
    int PendingCount,
    IReadOnlyList<string> HighlightTags);
