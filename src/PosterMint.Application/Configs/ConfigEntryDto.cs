namespace PosterMint.Application.Configs;

public sealed record ConfigEntryDto(
    string ConfigKey,
    string ConfigGroup,
    string ConfigValue,
    bool IsSecret,
    string? Description,
    DateTimeOffset UpdatedAt);
