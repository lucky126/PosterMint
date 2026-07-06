using PosterMint.Domain.Enums;

namespace PosterMint.Domain.Entities;

/// <summary>
/// 商铺登录日志。每次 /api/shop/login 尝试写一条，无论成败。
/// 只增，不改；导出/统计走查询即可，不做删除接口。
/// </summary>
public sealed class ShopLoginLogEntity
{
    public long Id { get; set; }

    /// <summary>命中用户名对应的 ShopId；用户名不存在时为 null。</summary>
    public int? ShopId { get; set; }

    /// <summary>本次登录传入的用户名（原始值，用于排查恶意扫号）。</summary>
    public string AttemptedUsername { get; set; } = string.Empty;

    /// <summary>登录结果分类，见 <see cref="ShopLoginResult"/>。</summary>
    public ShopLoginResult Result { get; set; }

    /// <summary>客户端 IP。反向代理场景下由 API 层从 X-Forwarded-For / RemoteIpAddress 里取。</summary>
    public string? Ip { get; set; }

    /// <summary>User-Agent 全串；小程序端会带 wx 版本，PC 会带浏览器 UA。</summary>
    public string? UserAgent { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public ShopEntity? Shop { get; set; }
}
