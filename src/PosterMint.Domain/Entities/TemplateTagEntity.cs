namespace PosterMint.Domain.Entities;

public sealed class TemplateTagEntity
{
    public int Id { get; set; }

    public int TemplateId { get; set; }

    public string Dimension { get; set; } = string.Empty;

    public string TagValue { get; set; } = string.Empty;

    public double Weight { get; set; } = 1.0d;

    public TemplateEntity? Template { get; set; }
}
