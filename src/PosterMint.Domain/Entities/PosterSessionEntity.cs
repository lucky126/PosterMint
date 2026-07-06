using PosterMint.Domain.Enums;

namespace PosterMint.Domain.Entities;

public sealed class PosterSessionEntity
{
    public int Id { get; set; }

    public string SessionKey { get; set; } = string.Empty;

    public int TemplateId { get; set; }

    public string Name { get; set; } = string.Empty;

    public SessionStatus Status { get; set; } = SessionStatus.Created;

    public string TemplateSnapshotJson { get; set; } = "{}";

    public string CurrentFieldsJson { get; set; } = "{}";

    public string CurrentLayoutJson { get; set; } = "[]";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public TemplateEntity? Template { get; set; }
}
