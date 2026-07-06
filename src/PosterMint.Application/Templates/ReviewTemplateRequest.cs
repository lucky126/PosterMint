namespace PosterMint.Application.Templates;

public sealed class ReviewTemplateRequest
{
    public bool Approved { get; init; }

    public string? Comment { get; init; }
}
