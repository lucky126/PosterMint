namespace PosterMint.Application.Contracts;

public sealed record ApiEnvelope<T>(
    T Data,
    string RequestId,
    DateTimeOffset Timestamp)
{
    public static ApiEnvelope<T> Create(T data, string requestId) =>
        new(data, requestId, DateTimeOffset.UtcNow);
}

public static class ApiEnvelope
{
    public static ApiEnvelope<T> Create<T>(T data, string requestId) =>
        Contracts.ApiEnvelope<T>.Create(data, requestId);
}
