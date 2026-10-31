using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Delivery.Infrastructure.Persistence.Outbox;

public sealed class OutboxBackgroundService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OutboxBackgroundService> logger)
    : BackgroundService
{
    private const int BatchSize = 1;
    private const int MaximumAttempts = 5;

    private static readonly TimeSpan ProcessingLease =
        TimeSpan.FromMinutes(5);

    private static readonly TimeSpan PollingInterval =
        TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Outbox background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await ProcessBatchAsync(
                    stoppingToken);

                if (processedCount == 0)
                {
                    await Task.Delay(
                        PollingInterval,
                        stoppingToken);
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "An error occurred while processing the outbox.");

                await Task.Delay(
                    PollingInterval,
                    stoppingToken);
            }
        }

        logger.LogInformation(
            "Outbox background service stopped.");
    }

    private async Task<int> ProcessBatchAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ClaimedMessage> messages;

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var claimer = scope.ServiceProvider
                .GetRequiredService<EfOutboxMessageClaimer>();

            var claimedMessages = await claimer.ClaimBatchAsync(
                BatchSize,
                ProcessingLease,
                cancellationToken);

            messages = claimedMessages
                .Select(message => new ClaimedMessage(
                    message.Id,
                    message.Type,
                    message.LockToken
                        ?? throw new InvalidOperationException(
                            "A claimed message must have a lock token.")))
                .ToArray();
        }

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ProcessMessageAsync(
                message,
                cancellationToken);
        }

        return messages.Count;
    }

    private async Task ProcessMessageAsync(
        ClaimedMessage claimedMessage,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var message = await dbContext.OutboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == claimedMessage.Id,
                cancellationToken);

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        if (message is null ||
            message.Status != OutboxMessageStatus.Processing ||
            message.LockToken != claimedMessage.LockToken ||
            message.LockedUntilUtc <= nowUtc)
        {
            logger.LogWarning(
                "Outbox message {MessageId} is no longer owned by this worker. Skipping processing.",
                claimedMessage.Id);

            return;
        }

        try
        {
            var handler = scope.ServiceProvider
                .GetServices<IOutboxMessageHandler>()
                .SingleOrDefault(candidate =>
                    candidate.MessageType == message.Type);

            if (handler is null)
            {
                throw new InvalidOperationException(
                    $"No outbox handler is registered for message type '{message.Type}'.");
            }

            await handler.HandleAsync(
                message.Payload,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Leave the message in Processing.
            // Its lease will expire and another worker can reclaim it.
            throw;
        }
        catch (Exception exception)
        {
            await FailMessageAsync(
                dbContext,
                message,
                claimedMessage.LockToken,
                exception,
                cancellationToken);

            return;
        }

        await CompleteMessageAsync(
            dbContext,
            message,
            claimedMessage.LockToken,
            cancellationToken);
    }

    private async Task CompleteMessageAsync(
        ApplicationDbContext dbContext,
        OutboxMessage message,
        Guid lockToken,
        CancellationToken cancellationToken)
    {
        var processedAtUtc = timeProvider
            .GetUtcNow()
            .UtcDateTime;

        message.Complete(
            lockToken,
            processedAtUtc);

        var affectedRows = await dbContext.OutboxMessages
            .Where(item =>
                item.Id == message.Id &&
                item.Status == OutboxMessageStatus.Processing &&
                item.LockToken == lockToken &&
                item.LockedUntilUtc > processedAtUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.Status,
                        message.Status)
                    .SetProperty(
                        item => item.ProcessedAtUtc,
                        message.ProcessedAtUtc)
                    .SetProperty(
                        item => item.LockedUntilUtc,
                        message.LockedUntilUtc)
                    .SetProperty(
                        item => item.LockToken,
                        message.LockToken)
                    .SetProperty(
                        item => item.LastError,
                        message.LastError),
                cancellationToken);

        if (affectedRows == 0)
        {
            logger.LogWarning(
                "Outbox message {MessageId} could not be completed because this worker no longer owns a valid lease.",
                message.Id);

            return;
        }

        logger.LogInformation(
            "Outbox message {MessageId} of type {MessageType} completed.",
            message.Id,
            message.Type);
    }

    private async Task FailMessageAsync(
        ApplicationDbContext dbContext,
        OutboxMessage message,
        Guid lockToken,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var failedAtUtc = timeProvider
            .GetUtcNow()
            .UtcDateTime;

        message.Fail(
            lockToken,
            exception.Message,
            failedAtUtc,
            failedAtUtc.Add(
                GetRetryDelay(message.AttemptCount)),
            MaximumAttempts);

        var affectedRows = await dbContext.OutboxMessages
            .Where(item =>
                item.Id == message.Id &&
                item.Status == OutboxMessageStatus.Processing &&
                item.LockToken == lockToken &&
                item.LockedUntilUtc > failedAtUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        item => item.Status,
                        message.Status)
                    .SetProperty(
                        item => item.NextAttemptAtUtc,
                        message.NextAttemptAtUtc)
                    .SetProperty(
                        item => item.LockedUntilUtc,
                        message.LockedUntilUtc)
                    .SetProperty(
                        item => item.LockToken,
                        message.LockToken)
                    .SetProperty(
                        item => item.LastError,
                        message.LastError),
                cancellationToken);

        if (affectedRows == 0)
        {
            logger.LogWarning(
                "Outbox message {MessageId} failure could not be recorded because this worker no longer owns a valid lease.",
                message.Id);

            return;
        }

        logger.LogError(
            exception,
            "Outbox message {MessageId} of type {MessageType} failed on attempt {AttemptCount}. Status: {Status}.",
            message.Id,
            message.Type,
            message.AttemptCount,
            message.Status);
    }

    private static TimeSpan GetRetryDelay(int attemptCount)
    {
        var exponent = Math.Clamp(attemptCount - 1, 0, 10);
        var seconds = Math.Min(
            3600,
            5 * Math.Pow(2, exponent));

        return TimeSpan.FromSeconds(seconds);
    }

    private sealed record ClaimedMessage(
        Guid Id,
        string Type,
        Guid LockToken);
}
