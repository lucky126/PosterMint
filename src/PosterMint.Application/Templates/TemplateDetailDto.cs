using PosterMint.Domain.Enums;
using System.Text.Json.Nodes;

namespace PosterMint.Application.Templates;

public sealed record TemplateDetailDto(
    int Id,
    string TemplateKey,
    string Name,
    string? Description,
    TemplateSceneType Scene,
    TemplateStatus Status,
    JsonNode? Canvas,
    JsonNode? Fields,
    JsonNode? Layout,
    IReadOnlyList<TemplateTagDto> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
