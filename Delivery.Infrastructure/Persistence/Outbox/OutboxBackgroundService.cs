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
    private const int BatchSize = 20;
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

        // Claim messages and release the database scope before processing.
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
                    message.Type))
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
            .SingleOrDefaultAsync(
                item => item.Id == claimedMessage.Id,
                cancellationToken);

        if (message is null ||
            message.Status != OutboxMessageStatus.Processing)
        {
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
            var failedAtUtc = timeProvider
                .GetUtcNow()
                .UtcDateTime;

            message.Fail(
                exception.Message,
                failedAtUtc,
                failedAtUtc.Add(
                    GetRetryDelay(message.AttemptCount)),
                MaximumAttempts);

            await dbContext.SaveChangesAsync(
                cancellationToken);

            logger.LogError(
                exception,
                "Outbox message {MessageId} of type {MessageType} failed on attempt {AttemptCount}. Status: {Status}.",
                message.Id,
                message.Type,
                message.AttemptCount,
                message.Status);

            return;
        }

        message.Complete(
            timeProvider.GetUtcNow().UtcDateTime);

        await dbContext.SaveChangesAsync(
            cancellationToken);

        logger.LogInformation(
            "Outbox message {MessageId} of type {MessageType} completed.",
            message.Id,
            message.Type);
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
        string Type);
}
