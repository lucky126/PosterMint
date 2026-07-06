namespace PosterMint.Application.Templates;

public sealed record TemplateChatResultDto(
    string Reply,
    IReadOnlyList<string> Warnings,
    TemplateDetailDto Template);
