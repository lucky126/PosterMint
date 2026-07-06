using Microsoft.EntityFrameworkCore;
using PosterMint.Application.Shops;
using PosterMint.Domain.Entities;
using PosterMint.Domain.Enums;
using PosterMint.Infrastructure.Persistence;

namespace PosterMint.Infrastructure.Shops;

/// <summary>
/// 商铺认证 + 登录日志。
///
/// Token 说明（v2 一期）：不用 JWT，简单 "shop_{id}_{shopKey}_{unixTs}"。
/// 一期小程序端只做"登录后拿 ShopKey → 后续请求带 ShopKey 查数据"这一套。
/// 二期换成签名 JWT 是本类局部改动，不影响调用方。
/// </summary>
public sealed class ShopAuthService(
    PosterMintDbContext dbContext,
    IPasswordHasher passwordHasher) : IShopAuthService
{
    public async Task<ShopLoginResponse> LoginAsync(
        ShopLoginRequest request,
        string? ip,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var username = (request.Username ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;

        // 空输入直接拒；不写日志（避免恶意刷空请求把日志表撑爆）
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return new ShopLoginResponse(
                ShopLoginResult.UserNotFound,
                "用户名或密码不能为空。",
                null,
                null);
        }

        var shop = await dbContext.Shops.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

        ShopLoginResult result;
        ShopDto? shopDto = null;
        string? token = null;
        string message;

        if (shop is null)
        {
            result = ShopLoginResult.UserNotFound;
            message = "用户名或密码错误。";  // 对外统一模糊，不泄露"用户名不存在"
        }
        else if (!string.Equals(shop.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            result = ShopLoginResult.Disabled;
            message = "账号已停用，请联系管理员。";
        }
        else if (string.IsNullOrEmpty(shop.PasswordHash) || string.IsNullOrEmpty(shop.PasswordSalt))
        {
            result = ShopLoginResult.NoPasswordSet;
            message = "账号尚未设置密码，请联系管理员。";
        }
        else if (!passwordHasher.Verify(password, shop.PasswordHash, shop.PasswordSalt!, shop.PasswordHashVersion))
        {
            result = ShopLoginResult.BadPassword;
            message = "用户名或密码错误。";
        }
        else
        {
            result = ShopLoginResult.Success;
            message = "登录成功。";

            var now = DateTimeOffset.UtcNow;
            shop.LastLoginAt = now;
            shop.UpdatedAt = now;

            token = GenerateToken(shop, now);
            shopDto = MapShop(shop);
        }

        // 写日志（无论成败）
        dbContext.ShopLoginLogs.Add(new ShopLoginLogEntity
        {
            ShopId = shop?.Id,
            AttemptedUsername = username,
            Result = result,
            Ip = Truncate(ip, 64),
            UserAgent = Truncate(userAgent, 512),
            OccurredAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ShopLoginResponse(result, message, shopDto, token);
    }

    public async Task<IReadOnlyList<ShopLoginLogDto>> ListLogsAsync(
        int shopId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0) limit = 50;
        if (limit > 500) limit = 500;

        var rows = await dbContext.ShopLoginLogs
            .AsNoTracking()
            .Where(x => x.ShopId == shopId)
            .ToListAsync(cancellationToken);

        // SQLite DateTimeOffset 客户端排序
        return rows
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .Select(x => new ShopLoginLogDto(
                x.Id,
                x.ShopId,
                x.AttemptedUsername,
                x.Result,
                x.Ip,
                x.UserAgent,
                x.OccurredAt))
            .ToList();
    }

    private static string GenerateToken(ShopEntity shop, DateTimeOffset issuedAt) =>
        $"shop_{shop.Id}_{shop.ShopKey}_{issuedAt.ToUnixTimeSeconds()}";

    private static string? Truncate(string? s, int max) =>
        s is null || s.Length <= max ? s : s[..max];

    private static ShopDto MapShop(ShopEntity e) =>
        new(
            e.Id,
            e.ShopKey,
            e.Name,
            e.ContactName,
            e.ContactPhone,
            e.Address,
            e.Industry,
            e.Status,
            e.Remark,
            e.Username,
            HasPassword: !string.IsNullOrEmpty(e.PasswordHash),
            e.LastLoginAt,
            e.CreatedAt,
            e.UpdatedAt);
}
