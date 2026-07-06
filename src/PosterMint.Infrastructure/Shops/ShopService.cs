using PosterMint.Application.Shops;
using PosterMint.Domain.Entities;
using PosterMint.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PosterMint.Infrastructure.Shops;

public sealed class ShopService(
    PosterMintDbContext dbContext,
    IPasswordHasher passwordHasher) : IShopService
{
    public async Task<IReadOnlyList<ShopDto>> ListAsync(string? keyword = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Shops.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(x =>
                x.Name.Contains(k) ||
                (x.ContactName != null && x.ContactName.Contains(k)) ||
                (x.ContactPhone != null && x.ContactPhone.Contains(k)) ||
                (x.Username != null && x.Username.Contains(k)));
        }

        var shops = await query.ToListAsync(cancellationToken);
        // SQLite 不支持 DateTimeOffset 的 ORDER BY，改为客户端排序
        return shops.OrderByDescending(x => x.UpdatedAt).Select(Map).ToList();
    }

    public async Task<ShopDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var shop = await dbContext.Shops.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return shop is null ? null : Map(shop);
    }

    public async Task<ShopDto> CreateAsync(ShopUpsertRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var now = DateTimeOffset.UtcNow;
        var entity = new ShopEntity
        {
            ShopKey = GenerateShopKey(),
            Name = request.Name.Trim(),
            ContactName = NullIfEmpty(request.ContactName),
            ContactPhone = NullIfEmpty(request.ContactPhone),
            Address = NullIfEmpty(request.Address),
            Industry = string.IsNullOrWhiteSpace(request.Industry) ? "餐饮" : request.Industry.Trim(),
            Status = NormalizeStatus(request.Status),
            Remark = NullIfEmpty(request.Remark),
            CreatedAt = now,
            UpdatedAt = now
        };

        // 账号（可选）
        var username = NullIfEmpty(request.Username);
        if (username is not null)
        {
            await EnsureUsernameUniqueAsync(username, ignoreId: null, cancellationToken);
            entity.Username = username;
        }

        // 密码（可选，允许"先建号后设密码"）
        if (!string.IsNullOrEmpty(request.Password))
        {
            var (hash, salt) = passwordHasher.Hash(request.Password);
            entity.PasswordHash = hash;
            entity.PasswordSalt = salt;
            entity.PasswordHashVersion = passwordHasher.CurrentVersion;
        }

        dbContext.Shops.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ShopDto> UpdateAsync(int id, ShopUpsertRequest request, CancellationToken cancellationToken = default)
    {
        Validate(request);
        var entity = await dbContext.Shops.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException($"商户不存在：{id}");

        entity.Name = request.Name.Trim();
        entity.ContactName = NullIfEmpty(request.ContactName);
        entity.ContactPhone = NullIfEmpty(request.ContactPhone);
        entity.Address = NullIfEmpty(request.Address);
        entity.Industry = string.IsNullOrWhiteSpace(request.Industry) ? "餐饮" : request.Industry.Trim();
        entity.Status = NormalizeStatus(request.Status);
        entity.Remark = NullIfEmpty(request.Remark);
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        // Username 语义：null = 保持不变；"" = 清空；其他 = 更新
        if (request.Username is not null)
        {
            var normalized = NullIfEmpty(request.Username);
            if (normalized is null)
            {
                // 显式清账号：同时清密码，避免"有密码没账号"孤悬
                entity.Username = null;
                entity.PasswordHash = null;
                entity.PasswordSalt = null;
                entity.PasswordHashVersion = 0;
            }
            else if (!string.Equals(normalized, entity.Username, StringComparison.Ordinal))
            {
                await EnsureUsernameUniqueAsync(normalized, ignoreId: id, cancellationToken);
                entity.Username = normalized;
            }
        }

        // Password 语义：null 或 "" = 保持不变；非空 = 更新
        if (!string.IsNullOrEmpty(request.Password))
        {
            var (hash, salt) = passwordHasher.Hash(request.Password);
            entity.PasswordHash = hash;
            entity.PasswordSalt = salt;
            entity.PasswordHashVersion = passwordHasher.CurrentVersion;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Shops.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return false;
        dbContext.Shops.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ShopDto Map(ShopEntity e) =>
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

    private static void Validate(ShopUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("店铺名称不能为空", nameof(request));
        }

        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            var u = request.Username.Trim();
            if (u.Length < 3 || u.Length > 32)
            {
                throw new ArgumentException("用户名长度必须在 3~32 之间", nameof(request));
            }
            foreach (var c in u)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                {
                    throw new ArgumentException("用户名只能包含字母、数字、下划线、连字符", nameof(request));
                }
            }
        }

        if (!string.IsNullOrEmpty(request.Password) && request.Password.Length < 6)
        {
            throw new ArgumentException("密码至少 6 位", nameof(request));
        }
    }

    private async Task EnsureUsernameUniqueAsync(string username, int? ignoreId, CancellationToken cancellationToken)
    {
        var q = dbContext.Shops.AsNoTracking().Where(x => x.Username == username);
        if (ignoreId is int id)
        {
            q = q.Where(x => x.Id != id);
        }
        if (await q.AnyAsync(cancellationToken))
        {
            throw new ArgumentException($"用户名 \"{username}\" 已被占用。", nameof(username));
        }
    }

    private static string NormalizeStatus(string status) =>
        status?.Trim() switch
        {
            "Disabled" or "disabled" or "停用" => "Disabled",
            _ => "Active"
        };

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string GenerateShopKey() =>
        "shop_" + Guid.NewGuid().ToString("N")[..12];
}
