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

    // ---- 小程序端登录凭证（v2 新增，2026-07-06）----

    /// <summary>登录用户名，全库唯一；商户在小程序端用它登录。可空 = 尚未设置账号。</summary>
    public string? Username { get; set; }

    /// <summary>PBKDF2 派生密钥的 Base64。空 = 尚未设置密码。</summary>
    public string? PasswordHash { get; set; }

    /// <summary>盐的 Base64 编码。与 PasswordHash 配对；单独存便于将来升级哈希算法。</summary>
    public string? PasswordSalt { get; set; }

    /// <summary>密码算法版本号，用于将来升级（如从 PBKDF2 换 Argon2）。当前恒为 1。</summary>
    public int PasswordHashVersion { get; set; }

    /// <summary>最近一次成功登录时间。仅记录成功，用于列表展示；详细流水见 ShopLoginLog。</summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    // ---- 时间戳 ----

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
