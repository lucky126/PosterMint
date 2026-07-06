namespace PosterMint.Application.Sessions;

public sealed class UpdateSessionFieldsRequest
{
    public Dictionary<string, string?> Fields { get; init; } = [];
}
