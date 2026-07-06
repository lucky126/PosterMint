using PosterMint.Application.Sessions;
using System.Text.Json.Nodes;

namespace PosterMint.Application.AI;

public interface IChatEditingAiService
{
    Task<AiEditResultDto> ApplyAsync(
        string message,
        JsonObject templateSnapshot,
        JsonObject currentFields,
        JsonArray currentLayout,
        IReadOnlyList<SessionMessageDto> conversationHistory,
        IReadOnlyList<SessionAssetDto> assets,
        CancellationToken cancellationToken = default);

    Task<AiTestResultDto> TestConnectionAsync(CancellationToken cancellationToken = default);

    LlmStatusDto GetStatus();
}
