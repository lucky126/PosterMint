namespace PosterMint.Application.Sessions;

public sealed record SessionAssetDto(
    string Kind,
    string Name,
    string DataUrl);
