namespace PosterMint.Application.AI;

public sealed class LlmOptions
{
    public bool Enabled { get; set; } = true;

    public string Provider { get; set; } = "openai-compatible";

    public string BaseUrl { get; set; } = string.Empty;

    public string ChatUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;

    public double Temperature { get; set; }

    public bool ResponseFormat { get; set; } = true;
}
