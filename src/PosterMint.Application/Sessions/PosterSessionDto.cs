using PosterMint.Domain.Enums;
using System.Text.Json.Nodes;

namespace PosterMint.Application.Sessions;

public sealed record PosterSessionDto(
    string SessionKey,
    int TemplateId,
    string Name,
    SessionStatus Status,
    string Scene,
    string Goal,
    string? ReferenceImageDataUrl,
    JsonNode? TemplateSnapshot,
    JsonNode? Fields,
    JsonNode? Layout,
    IReadOnlyList<SessionMessageDto> Messages,
    IReadOnlyList<SessionVersionDto> Versions,
    IReadOnlyList<SuggestedActionDto> SuggestedActions,
    IReadOnlyList<SessionAssetDto> Assets,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
