namespace PosterMint.Application.Sessions;

public sealed record SessionChatResultDto(
    string Reply,
    string Mode,
    IReadOnlyDictionary<string, string?> UpdatedFields,
    IReadOnlyList<LayoutNodePatchDto> UpdatedLayoutNodes,
    IReadOnlyList<string> Warnings,
    PosterSessionDto Session);
