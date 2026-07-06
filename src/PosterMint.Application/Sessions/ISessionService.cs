namespace PosterMint.Application.Sessions;

public interface ISessionService
{
    Task<PosterSessionDto> CreateAsync(CreateSessionRequest request, CancellationToken cancellationToken = default);

    Task<PosterSessionDto> BootstrapAsync(BootstrapSessionRequest request, CancellationToken cancellationToken = default);

    Task<PosterSessionDto?> GetAsync(string sessionKey, CancellationToken cancellationToken = default);

    Task<PosterSessionDto> UpdateFieldsAsync(
        string sessionKey,
        UpdateSessionFieldsRequest request,
        CancellationToken cancellationToken = default);

    Task<SessionChatResultDto> ApplyChatAsync(
        string sessionKey,
        ApplySessionChatRequest request,
        CancellationToken cancellationToken = default);
}
