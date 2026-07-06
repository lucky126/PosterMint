using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using PosterMint.Application.PspTemplates;
using PosterMint.Domain.Entities;
using PosterMint.Domain.Enums;
using PosterMint.Infrastructure.Persistence;

namespace PosterMint.Infrastructure.PspTemplates;

/// <summary>
/// PSP 模板落地服务。只处理 PSP 版模板（TemplateEntity.Psp 非空的一支）；
/// v1 老模板（CanvasJson/FieldsJson/LayoutJson）不由这里管，交给现有 TemplateService。
/// </summary>
public sealed class PspTemplateService(PosterMintDbContext dbContext) : IPspTemplateService
{
    public async Task<IReadOnlyList<PspTemplateSummaryDto>> ListAsync(
        PspTemplateFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Templates
            .AsNoTracking()
            .Include(x => x.Shop)
            .AsQueryable();

        if (filter.Ownership.HasValue)
        {
            query = query.Where(x => x.Ownership == filter.Ownership.Value);
        }
        if (filter.ShopId.HasValue)
        {
            query = query.Where(x => x.ShopId == filter.ShopId.Value);
        }
        if (filter.Scene.HasValue)
        {
            query = query.Where(x => x.Scene == filter.Scene.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var k = filter.Keyword.Trim();
            query = query.Where(x => x.Name.Contains(k) || x.TemplateKey.Contains(k));
        }

        var rows = await query.ToListAsync(cancellationToken);

        // SQLite 不支持 DateTimeOffset ORDER BY，客户端排
        return rows
            .OrderByDescending(x => x.UpdatedAt)
            .Select(MapSummary)
            .ToList();
    }

    public async Task<PspTemplateDetailDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Templates
            .AsNoTracking()
            .Include(x => x.Shop)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? null : MapDetail(entity);
    }

    public PspValidationResult Validate(JsonNode? psp) => PspValidator.Validate(psp);

    public async Task<PspTemplateDetailDto> ImportAsync(
        PspTemplateImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. 校验 PSP（不通过就直接抛，不入库）
        var result = PspValidator.Validate(request.Psp);
        if (!result.IsValid)
        {
            throw new PspValidationException(result);
        }

        // 2. 归属一致性校验
        if (request.Ownership == TemplateOwnership.Shop && !request.ShopId.HasValue)
        {
            throw new ArgumentException("Ownership=Shop 时必须提供 ShopId。", nameof(request));
        }
        if (request.Ownership == TemplateOwnership.Category && request.ShopId.HasValue)
        {
            throw new ArgumentException("Ownership=Category 时不应传 ShopId。", nameof(request));
        }
        if (request.ShopId.HasValue)
        {
            var shopExists = await dbContext.Shops.AnyAsync(s => s.Id == request.ShopId.Value, cancellationToken);
            if (!shopExists)
            {
                throw new ArgumentException($"ShopId={request.ShopId.Value} 不存在。", nameof(request));
            }
        }

        // 3. TemplateKey：请求 > PSP.id > 自动生成
        var key = string.IsNullOrWhiteSpace(request.TemplateKey)
            ? request.Psp["id"]?.GetValue<string>()?.Trim()
            : request.TemplateKey.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            key = "psp_" + Guid.NewGuid().ToString("N")[..12];
        }

        var duplicate = await dbContext.Templates.AnyAsync(x => x.TemplateKey == key, cancellationToken);
        if (duplicate)
        {
            throw new InvalidOperationException($"TemplateKey \"{key}\" 已存在。");
        }

        // 4. Name：请求 > PSP.name
        var name = string.IsNullOrWhiteSpace(request.Name)
            ? request.Psp["name"]?.GetValue<string>()?.Trim() ?? key!
            : request.Name.Trim();

        var now = DateTimeOffset.UtcNow;
        var entity = new TemplateEntity
        {
            TemplateKey = key!,
            Name = name,
            Description = NullIfEmpty(request.Description),
            Scene = request.Scene,
            Ownership = request.Ownership,
            ShopId = request.ShopId,
            Psp = request.Psp.ToJsonString(),
            SchemaVersion = result.SchemaVersion,
            SlotCount = result.SlotCount,
            PreviewImage = NullIfEmpty(request.PreviewImage),

            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Templates.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        // 附上 Shop 导航
        if (entity.ShopId.HasValue)
        {
            entity.Shop = await dbContext.Shops.FirstOrDefaultAsync(s => s.Id == entity.ShopId.Value, cancellationToken);
        }

        return MapDetail(entity);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Templates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null) return false;
        dbContext.Templates.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---- 映射 ----

    private static PspTemplateSummaryDto MapSummary(TemplateEntity e) =>
        new(
            e.Id,
            e.TemplateKey,
            e.Name,
            e.Description,
            e.Ownership,
            e.ShopId,
            e.Shop?.Name,
            e.Scene,
            e.SchemaVersion,
            e.SlotCount,
            e.PreviewImage,
            e.UpdatedAt);

    private static PspTemplateDetailDto MapDetail(TemplateEntity e)
    {
        var psp = JsonNode.Parse(e.Psp) ?? new JsonObject();
        return new PspTemplateDetailDto(
            e.Id,
            e.TemplateKey,
            e.Name,
            e.Description,
            e.Ownership,
            e.ShopId,
            e.Shop?.Name,
            e.Scene,
            e.SchemaVersion,
            e.SlotCount,
            e.PreviewImage,
            psp,
            e.CreatedAt,
            e.UpdatedAt);
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
