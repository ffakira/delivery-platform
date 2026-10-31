
using Delivery.Infrastructure.Persistence;
using Delivery.Infrastructure.Persistence.Outbox;
using Delivery.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.IntegrationTests.Outbox;

[Collection(DatabaseCollection.Name)]
public sealed class EfOutboxMessageFinalizerTests(
    DatabaseFixture fixture)
{
    [Fact]
    public async Task CompleteAsync_WithStaleLockToken_DoesNotCompleteMessage()
    {
        var (messageId, workerALockToken) =
            await SeedMessageClaimedByWorkerAAsync();

        var workerB = await ReclaimMessageAsync(messageId);

        Assert.NotEqual(workerALockToken, workerB.LockToken);
        Assert.Equal(2, workerB.AttemptCount);

        await using var scope = fixture.Services.CreateAsyncScope();

        var finalizer = scope.ServiceProvider
            .GetRequiredService<EfOutboxMessageFinalizer>();

        var result = await finalizer.CompleteAsync(
            messageId,
            workerALockToken,
            DateTime.UtcNow,
            CancellationToken.None);

        Assert.False(result);

        var persistedMessage = await FindMessageAsync(messageId);

        Assert.NotNull(persistedMessage);
        Assert.Equal(
            OutboxMessageStatus.Processing,
            persistedMessage.Status);

        Assert.Equal(workerB.LockToken, persistedMessage.LockToken);
        Assert.Equal(2, persistedMessage.AttemptCount);
        Assert.Null(persistedMessage.ProcessedAtUtc);
        Assert.NotNull(persistedMessage.LockedUntilUtc);
        Assert.True(persistedMessage.LockedUntilUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task FailAsync_WithStaleLockToken_DoesNotFailMessage()
    {
        var (messageId, workerALockToken) =
            await SeedMessageClaimedByWorkerAAsync();

        var workerB = await ReclaimMessageAsync(messageId);

        Assert.NotEqual(workerALockToken, workerB.LockToken);
        Assert.Equal(2, workerB.AttemptCount);

        await using var scope = fixture.Services.CreateAsyncScope();

        var finalizer = scope.ServiceProvider
            .GetRequiredService<EfOutboxMessageFinalizer>();

        var failedAtUtc = DateTime.UtcNow;

        var result = await finalizer.FailAsync(
            messageId,
            workerALockToken,
            "Worker A failed after losing ownership.",
            failedAtUtc,
            failedAtUtc.AddSeconds(30),
            maximumAttempts: 5,
            CancellationToken.None);

        Assert.False(result);

        var persistedMessage = await FindMessageAsync(messageId);

        Assert.NotNull(persistedMessage);
        Assert.Equal(
            OutboxMessageStatus.Processing,
            persistedMessage.Status);

        Assert.Equal(workerB.LockToken, persistedMessage.LockToken);
        Assert.Equal(2, persistedMessage.AttemptCount);
        Assert.Null(persistedMessage.LastError);
        Assert.NotNull(persistedMessage.LockedUntilUtc);
        Assert.True(persistedMessage.LockedUntilUtc > DateTime.UtcNow);
    }

    private async Task<(Guid MessageId, Guid LockToken)>
        SeedMessageClaimedByWorkerAAsync()
    {
        var now = DateTime.UtcNow;

        var message = OutboxMessage.Create(
            "integration-test.stale-worker",
            """{"test":true}""",
            now.AddMinutes(-20));

        // Worker A claims the message with an already-expired lease.
        message.Claim(
            now.AddMinutes(-10),
            now.AddMinutes(-5));

        var workerALockToken = message.LockToken
            ?? throw new InvalidOperationException(
                "Worker A must have a lock token.");

        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.OutboxMessages.ExecuteDeleteAsync();

        dbContext.OutboxMessages.Add(message);

        await dbContext.SaveChangesAsync();

        return (message.Id, workerALockToken);
    }

    private async Task<OutboxMessage> ReclaimMessageAsync(Guid messageId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var claimer = scope.ServiceProvider
            .GetRequiredService<EfOutboxMessageClaimer>();

        var claimedMessages = await claimer.ClaimBatchAsync(
            batchSize: 1,
            processingLease: TimeSpan.FromMinutes(5),
            CancellationToken.None);

        var message = Assert.Single(
            claimedMessages,
            candidate => candidate.Id == messageId);

        Assert.Equal(OutboxMessageStatus.Processing, message.Status);
        Assert.Equal(2, message.AttemptCount);
        Assert.NotNull(message.LockToken);
        Assert.NotNull(message.LockedUntilUtc);
        Assert.True(message.LockedUntilUtc > DateTime.UtcNow);

        return message;
    }

    private async Task<OutboxMessage?> FindMessageAsync(Guid messageId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        return await dbContext.OutboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(message => message.Id == messageId);
    }
}
