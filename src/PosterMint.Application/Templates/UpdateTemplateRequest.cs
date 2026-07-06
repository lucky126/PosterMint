using PosterMint.Domain.Enums;
using System.Text.Json.Nodes;

namespace PosterMint.Application.Templates;

public sealed class UpdateTemplateRequest
{
    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public TemplateSceneType Scene { get; init; } = TemplateSceneType.SingleDish;

    public JsonObject Canvas { get; init; } = new();

    public JsonArray Fields { get; init; } = new();

    public JsonArray Layout { get; init; } = new();

    public IReadOnlyList<TemplateTagDto> Tags { get; init; } = Array.Empty<TemplateTagDto>();
}
