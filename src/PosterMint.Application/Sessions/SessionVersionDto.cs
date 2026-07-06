namespace PosterMint.Application.Sessions;

public sealed record SessionVersionDto(
    int VersionNo,
    string Description,
    DateTimeOffset Timestamp);
