using System.Text.Json.Nodes;

namespace PosterMint.Application.Sessions;

public sealed record LayoutNodePatchDto(
    int NodeIndex,
    JsonObject Changes);
