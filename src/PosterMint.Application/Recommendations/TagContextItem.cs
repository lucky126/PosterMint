namespace PosterMint.Application.Recommendations;

public sealed record TagContextItem(
    string Dimension,
    string TagValue,
    double Weight = 1.0d);
