namespace PosterMint.Application.Shops;

/// <summary>创建/更新商户请求</summary>
public sealed class ShopUpsertRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string Industry { get; set; } = "餐饮";
    public string Status { get; set; } = "Active";
    public string? Remark { get; set; }
}
