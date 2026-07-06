using PosterMint.Application.Sessions;

namespace PosterMint.Application.AI;

public sealed record AiEditResultDto(
    string Reply,
    string Mode,
    IReadOnlyDictionary<string, string?> UpdatedFields,
    IReadOnlyList<LayoutNodePatchDto> UpdatedLayoutNodes,
    IReadOnlyList<string> Warnings);
