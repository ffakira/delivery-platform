using Delivery.Application.Abstractions.Persistence;

namespace Delivery.UnitTests.Restaurants.Fakes;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        SaveChangesCallCount++;

        return Task.FromResult(1);
    }
}
