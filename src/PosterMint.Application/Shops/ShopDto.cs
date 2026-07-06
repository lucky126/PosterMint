namespace PosterMint.Application.Shops;

/// <summary>商户 DTO（既用于列表返回，也用于详情返回）</summary>
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
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
