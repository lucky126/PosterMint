using PosterMint.Domain.Enums;

namespace PosterMint.Domain.Entities;

public sealed class TemplateEntity
{
    public int Id { get; set; }

    public string TemplateKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public TemplateSceneType Scene { get; set; } = TemplateSceneType.SingleDish;

    public TemplateStatus Status { get; set; } = TemplateStatus.Draft;

    public string CanvasJson { get; set; } = "{}";

    public string FieldsJson { get; set; } = "[]";

    public string LayoutJson { get; set; } = "[]";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<TemplateTagEntity> Tags { get; set; } = new List<TemplateTagEntity>();

    public ICollection<PosterSessionEntity> Sessions { get; set; } = new List<PosterSessionEntity>();
}
