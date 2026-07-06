namespace PosterMint.Domain.Entities;

public sealed class ConfigEntryEntity
{
    public int Id { get; set; }

    public string ConfigKey { get; set; } = string.Empty;

    public string ConfigGroup { get; set; } = string.Empty;

    public string ConfigValue { get; set; } = string.Empty;

    public bool IsSecret { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
