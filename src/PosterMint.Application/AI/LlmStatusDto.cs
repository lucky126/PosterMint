namespace PosterMint.Application.AI;

public sealed record LlmStatusDto(
    bool ExternalAvailable,
    string Provider,
    string Model,
    string Mode);
