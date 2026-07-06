using PosterMint.Domain.Enums;

namespace PosterMint.Application.Shops;

/// <summary>登录请求。小程序端通过 POST /api/shop/login 提交。</summary>
public sealed class ShopLoginRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// 登录结果。成功时 <see cref="Shop"/> / <see cref="Token"/> 有值；
/// 失败时只有 <see cref="Result"/> 和 <see cref="Message"/>。
/// </summary>
public sealed record ShopLoginResponse(
    ShopLoginResult Result,
    string Message,
    ShopDto? Shop,
    string? Token);

/// <summary>登录日志行，用于 PC 后台展示。</summary>
public sealed record ShopLoginLogDto(
    long Id,
    int? ShopId,
    string AttemptedUsername,
    ShopLoginResult Result,
    string? Ip,
    string? UserAgent,
    DateTimeOffset OccurredAt);

/// <summary>商铺认证服务：登录 + 日志查询 + 密码管理由 API 层调 ShopService 转发。</summary>
public interface IShopAuthService
{
    /// <summary>
    /// 认证。命中 → 返回 Success + 简单 token；否则返回具体失败分类。
    /// 无论成败都会往 ShopLoginLog 写一条。ip/ua 由 API 层从请求上下文取到后传入。
    /// </summary>
    Task<ShopLoginResponse> LoginAsync(
        ShopLoginRequest request,
        string? ip,
        string? userAgent,
        CancellationToken cancellationToken = default);

    /// <summary>查询某商铺的登录日志，最新 <paramref name="limit"/> 条。</summary>
    Task<IReadOnlyList<ShopLoginLogDto>> ListLogsAsync(
        int shopId,
        int limit,
        CancellationToken cancellationToken = default);
}
