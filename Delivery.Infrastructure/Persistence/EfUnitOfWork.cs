using Delivery.Application.Abstractions.Persistence;

namespace Delivery.Infrastructure.Persistence;

public sealed class EfUnitOfWork(
    ApplicationDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
