using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Domain.Variants;
using Microsoft.EntityFrameworkCore;

namespace Kart.Product.Infrastructure.Persistence;

public sealed class VariantRepository(ProductDbContext dbContext) : IVariantRepository
{
    public Task<Variant?> GetBySkuAsync(string sku, CancellationToken cancellationToken)
    {
        var skuValue = Sku.From(sku);
        return dbContext.Variants.FirstOrDefaultAsync(v => v.Sku == skuValue, cancellationToken);
    }

    public Task<bool> ExistsAsync(string sku, CancellationToken cancellationToken)
    {
        var skuValue = Sku.From(sku);
        return dbContext.Variants.AnyAsync(v => v.Sku == skuValue, cancellationToken);
    }

    public async Task<IReadOnlyList<Variant>> GetByProductGroupIdAsync(Guid productGroupId, CancellationToken cancellationToken) =>
        await dbContext.Variants.Where(v => v.ProductGroupId == productGroupId).ToListAsync(cancellationToken);

    public void Add(Variant variant) => dbContext.Variants.Add(variant);
}
