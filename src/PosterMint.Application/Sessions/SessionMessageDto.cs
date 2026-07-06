namespace PosterMint.Application.Sessions;

public sealed record SessionMessageDto(
    string Role,
    string RoleLabel,
    string Text,
    DateTimeOffset Timestamp,
    IReadOnlyList<string>? Changes = null,
    string? Type = null);
