using Kart.Product.Application.Common.Interfaces;
using Kart.Product.Domain.ProductGroups;
using Microsoft.EntityFrameworkCore;

namespace Kart.Product.Infrastructure.Persistence;

public sealed class ProductGroupRepository(ProductDbContext dbContext) : IProductGroupRepository
{
    public Task<ProductGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ProductGroups.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Add(ProductGroup productGroup) => dbContext.ProductGroups.Add(productGroup);
}
