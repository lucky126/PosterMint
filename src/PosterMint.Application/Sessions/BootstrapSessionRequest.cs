namespace PosterMint.Application.Sessions;

public sealed class BootstrapSessionRequest
{
    public string Scene { get; init; } = "single-dish";

    public string Goal { get; init; } = string.Empty;

    public string? ReferenceImageDataUrl { get; init; }
}
