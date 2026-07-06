namespace PosterMint.Application.Contracts;

public sealed record ApiErrorResponse(
    string Error,
    string Message,
    object? Details,
    string RequestId,
    DateTimeOffset Timestamp)
{
    public static ApiErrorResponse Create(
        string error,
        string message,
        string requestId,
        object? details = null) =>
        new(error, message, details, requestId, DateTimeOffset.UtcNow);
}
