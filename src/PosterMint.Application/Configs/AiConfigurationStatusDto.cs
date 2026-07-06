namespace PosterMint.Application.Configs;

public sealed record AiConfigurationStatusDto(
    bool IsTextConfigured,
    bool IsImageConfigured,
    bool IsReady);
