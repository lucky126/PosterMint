using PosterMint.Application.Sessions;

namespace PosterMint.Infrastructure.Sessions;

public sealed class SessionInteractionState
{
    public string Scene { get; set; } = "single-dish";

    public string Goal { get; set; } = string.Empty;

    public string? ReferenceImageDataUrl { get; set; }

    public List<SessionMessageDto> Messages { get; } = [];

    public List<SessionVersionDto> Versions { get; } = [];

    public List<SuggestedActionDto> SuggestedActions { get; } = [];

    public List<SessionAssetDto> Assets { get; } = [];
}
