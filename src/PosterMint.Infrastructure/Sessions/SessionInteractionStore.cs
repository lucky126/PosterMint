using System.Collections.Concurrent;

namespace PosterMint.Infrastructure.Sessions;

public sealed class SessionInteractionStore : ISessionInteractionStore
{
    private readonly ConcurrentDictionary<string, SessionInteractionState> _states = new(StringComparer.OrdinalIgnoreCase);

    public SessionInteractionState GetOrCreate(string sessionKey) =>
        _states.GetOrAdd(sessionKey, _ => new SessionInteractionState());
}
