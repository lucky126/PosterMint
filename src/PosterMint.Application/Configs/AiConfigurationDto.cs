namespace PosterMint.Application.Configs;

public sealed class AiConfigurationDto
{
    public string TextProvider { get; set; } = "openai-compatible";

    public string TextBaseUrl { get; set; } = string.Empty;

    public string TextChatUrl { get; set; } = string.Empty;

    public string TextApiKey { get; set; } = string.Empty;

    public string TextModel { get; set; } = string.Empty;

    public string ImageProvider { get; set; } = "openai-compatible";

    public string ImageBaseUrl { get; set; } = string.Empty;

    public string ImageApiKey { get; set; } = string.Empty;

    public string ImageModel { get; set; } = string.Empty;
}
