namespace PosterMint.Application.Sessions;

public sealed class CreateSessionRequest
{
    public int TemplateId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Scene { get; init; } = "single-dish";

    public string Goal { get; init; } = string.Empty;

    public string? ReferenceImageDataUrl { get; init; }
}
