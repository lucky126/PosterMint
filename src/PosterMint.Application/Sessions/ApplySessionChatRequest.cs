namespace PosterMint.Application.Sessions;

public sealed class ApplySessionChatRequest
{
    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<SessionAssetDto> Assets { get; init; } = Array.Empty<SessionAssetDto>();
}
