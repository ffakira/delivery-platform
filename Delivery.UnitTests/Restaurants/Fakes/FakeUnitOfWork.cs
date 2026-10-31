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

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var result = await operation(cancellationToken);
        
        await SaveChangesAsync(cancellationToken);

        return result;
    }
}
