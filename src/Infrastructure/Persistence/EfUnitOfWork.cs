using Kart.Product.Application.Common.Interfaces;

namespace Kart.Product.Infrastructure.Persistence;

public sealed class EfUnitOfWork(ProductDbContext dbContext) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
