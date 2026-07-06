namespace PosterMint.Application.Shops;

public interface IShopService
{
    Task<IReadOnlyList<ShopDto>> ListAsync(string? keyword = null, CancellationToken cancellationToken = default);

    Task<ShopDto?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<ShopDto> CreateAsync(ShopUpsertRequest request, CancellationToken cancellationToken = default);

    Task<ShopDto> UpdateAsync(int id, ShopUpsertRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
