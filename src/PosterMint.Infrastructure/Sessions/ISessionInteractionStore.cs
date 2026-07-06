namespace PosterMint.Infrastructure.Sessions;

public interface ISessionInteractionStore
{
    SessionInteractionState GetOrCreate(string sessionKey);
}
