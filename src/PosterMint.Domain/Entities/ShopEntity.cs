namespace PosterMint.Domain.Entities;

public sealed class ShopEntity
{
    public int Id { get; set; }

    /// <summary>商户 Key（对外用，如小程序端登录后拿到的商户标识）</summary>
    public string ShopKey { get; set; } = string.Empty;

    /// <summary>店铺名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>联系人</summary>
    public string? ContactName { get; set; }

    /// <summary>联系电话</summary>
    public string? ContactPhone { get; set; }

    /// <summary>店铺地址</summary>
    public string? Address { get; set; }

    /// <summary>行业（一期固定"餐饮"）</summary>
    public string Industry { get; set; } = "餐饮";

    /// <summary>状态（Active / Disabled）</summary>
    public string Status { get; set; } = "Active";

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
