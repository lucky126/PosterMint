using PosterMint.Application.Shops;
using PosterMint.Domain.Entities;
using PosterMint.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PosterMint.Infrastructure.Shops;

public sealed class ShopService(PosterMintDbContext dbContext) : IShopService
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
                (x.ContactPhone != null && x.ContactPhone.Contains(k)));
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
        new(e.Id, e.ShopKey, e.Name, e.ContactName, e.ContactPhone, e.Address, e.Industry, e.Status, e.Remark, e.CreatedAt, e.UpdatedAt);

    private static void Validate(ShopUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("店铺名称不能为空", nameof(request));
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
