namespace PosterMint.Application.AI;

public sealed record AiTestResultDto(
    bool Success,
    string ModelName,
    int LatencyMs,
    string? Message = null,
    string? Error = null);
