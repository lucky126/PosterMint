namespace PosterMint.Application.Shops;

/// <summary>
/// 商户 DTO（既用于列表返回，也用于详情返回）。
/// 注意：不含 PasswordHash / PasswordSalt，永不出库外；用 HasPassword 布尔位告知前端是否已配好。
/// </summary>
public sealed record ShopDto(
    int Id,
    string ShopKey,
    string Name,
    string? ContactName,
    string? ContactPhone,
    string? Address,
    string Industry,
    string Status,
    string? Remark,
    string? Username,
    bool HasPassword,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
