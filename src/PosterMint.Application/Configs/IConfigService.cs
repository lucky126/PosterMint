using PosterMint.Application.AI;

namespace PosterMint.Application.Configs;

public interface IConfigService
{
    Task<IReadOnlyList<ConfigEntryDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<AiConfigurationDto> GetAiConfigurationAsync(CancellationToken cancellationToken = default);

    Task<AiConfigurationStatusDto> GetAiConfigurationStatusAsync(CancellationToken cancellationToken = default);

    Task SaveAiConfigurationAsync(AiConfigurationDto request, CancellationToken cancellationToken = default);

    Task<AiTestResultDto> TestTextModelAsync(CancellationToken cancellationToken = default);

    Task<AiTestResultDto> TestImageModelAsync(CancellationToken cancellationToken = default);
}
