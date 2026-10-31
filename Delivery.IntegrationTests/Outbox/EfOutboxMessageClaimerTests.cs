using Delivery.Infrastructure.Persistence;
using Delivery.Infrastructure.Persistence.Outbox;
using Delivery.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.IntegrationTests.Outbox;

[Collection(DatabaseCollection.Name)]
public sealed class EfOutboxMessageClaimerTests(
    DatabaseFixture fixture)
{
    [Fact]
    public async Task ClaimBatchAsync_WithPendingMessages_ClaimsAndIncrementsAttemptCount()
    {
        var messageId = await SeedPendingMessageAsync();

        var claimedMessages = await ClaimBatchAsync(10);

        var message = Assert.Single(
            claimedMessages,
            message => message.Id == messageId);

        Assert.Equal(OutboxMessageStatus.Processing, message.Status);
        Assert.Equal(1, message.AttemptCount);
        Assert.NotNull(message.LockedUntilUtc);
        Assert.Null(message.LastError);

        var persistedMessage = await FindMessageAsync(messageId);

        Assert.NotNull(persistedMessage);
        Assert.Equal(OutboxMessageStatus.Processing, persistedMessage.Status);
        Assert.Equal(1, persistedMessage.AttemptCount);
    }

    [Fact]
    public async Task ClaimBatchAsync_WithExpiredProcessingLease_ReclaimsMessage()
    {
        var messageId = await SeedExpiredProcessingMessageAsync();

        var claimedMessages = await ClaimBatchAsync(10);

        var message = Assert.Single(
            claimedMessages,
            message => message.Id == messageId);

        Assert.Equal(OutboxMessageStatus.Processing, message.Status);
        Assert.Equal(2, message.AttemptCount);
        Assert.NotNull(message.LockedUntilUtc);
        Assert.True(message.LockedUntilUtc > DateTime.UtcNow);

        var persistedMessage = await FindMessageAsync(messageId);

        Assert.NotNull(persistedMessage);
        Assert.Equal(OutboxMessageStatus.Processing, persistedMessage.Status);
        Assert.Equal(2, persistedMessage.AttemptCount);
    }

    [Fact]
    public async Task ClaimBatchAsync_WithFutureRetryMessage_DoesNotClaimMessage()
    {
        var messageId = await SeedFutureRetryMessageAsync();

        var claimedMessages = await ClaimBatchAsync(10);

        Assert.DoesNotContain(
            claimedMessages,
            message => message.Id == messageId);

        var persistedMessage = await FindMessageAsync(messageId);

        Assert.NotNull(persistedMessage);
        Assert.Equal(OutboxMessageStatus.Pending, persistedMessage.Status);
        Assert.Equal(1, persistedMessage.AttemptCount);
        Assert.NotNull(persistedMessage.NextAttemptAtUtc);
        Assert.True(persistedMessage.NextAttemptAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task ClaimBatchAsync_WithConcurrentWorkers_DoesNotClaimSameMessageTwice()
    {
        await SeedPendingMessagesAsync(10);

        var firstWorkerTask = ClaimBatchAsync(5);
        var secondWorkerTask = ClaimBatchAsync(5);

        var results = await Task.WhenAll(
            firstWorkerTask,
            secondWorkerTask);

        var firstBatch = results[0];
        var secondBatch = results[1];

        Assert.Equal(5, firstBatch.Count);
        Assert.Equal(5, secondBatch.Count);

        var firstIds = firstBatch
            .Select(message => message.Id)
            .ToHashSet();

        var secondIds = secondBatch
            .Select(message => message.Id)
            .ToHashSet();

        Assert.Empty(firstIds.Intersect(secondIds));

        Assert.Equal(
            10,
            firstIds.Union(secondIds).Count());

        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var persistedMessages = await dbContext.OutboxMessages
            .AsNoTracking()
            .ToListAsync();

        Assert.Equal(10, persistedMessages.Count);

        Assert.All(
            persistedMessages,
            message =>
            {
                Assert.Equal(
                    OutboxMessageStatus.Processing,
                    message.Status);

                Assert.Equal(1, message.AttemptCount);
            });
    }

    private async Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
        int batchSize)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var claimer = scope.ServiceProvider
            .GetRequiredService<EfOutboxMessageClaimer>();

        return await claimer.ClaimBatchAsync(
            batchSize,
            TimeSpan.FromMinutes(5),
            CancellationToken.None);
    }

    private async Task<Guid> SeedPendingMessageAsync()
    {
        var message = CreateMessage();

        await SeedMessagesAsync(message);

        return message.Id;
    }

    private async Task SeedPendingMessagesAsync(int count)
    {
        var messages = Enumerable
            .Range(0, count)
            .Select(_ => CreateMessage())
            .ToArray();

        await SeedMessagesAsync(messages);
    }

    private async Task<Guid> SeedExpiredProcessingMessageAsync()
    {
        var now = DateTime.UtcNow;

        var message = CreateMessage(now.AddMinutes(-10));

        message.Claim(
            now.AddMinutes(-5),
            now.AddMinutes(-1));

        await SeedMessagesAsync(message);

        return message.Id;
    }

    private async Task<Guid> SeedFutureRetryMessageAsync()
    {
        var now = DateTime.UtcNow;

        var message = CreateMessage(now.AddMinutes(-10));

        message.Claim(
            now.AddMinutes(-5),
            now.AddMinutes(5));

        var lockToken = message.LockToken
            ?? throw new InvalidOperationException(
                "Claimed message must have a lock token.");

        message.Fail(
            lockToken,
            "Temporary SMTP failure.",
            now,
            now.AddHours(1),
            maximumAttempts: 5);

        await SeedMessagesAsync(message);

        return message.Id;
    }

    private async Task SeedMessagesAsync(
        params OutboxMessage[] messages)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.OutboxMessages.ExecuteDeleteAsync();

        await dbContext.OutboxMessages.AddRangeAsync(messages);

        await dbContext.SaveChangesAsync();
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

    private static OutboxMessage CreateMessage(DateTime? createdAtUtc = null)
    {
        return OutboxMessage.Create(
            "integration-test.message",
            """{"test":true}""",
            createdAtUtc ?? DateTime.UtcNow.AddMinutes(-1));
    }
}
