using Microsoft.EntityFrameworkCore;

namespace Delivery.Infrastructure.Persistence.Outbox;

public sealed class EfOutboxMessageFinalizer(
    ApplicationDbContext dbContext)
{
    public async Task<bool> CompleteAsync(
        Guid messageId,
        Guid lockToken,
        DateTime processedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            messageId,
            Guid.Empty);

        ArgumentOutOfRangeException.ThrowIfEqual(
            lockToken,
            Guid.Empty);

        if (processedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Processing time must be UTC.",
                nameof(processedAtUtc));
        }

        var affectedRows = await dbContext.OutboxMessages
            .Where(message =>
                message.Id == messageId &&
                message.Status == OutboxMessageStatus.Processing &&
                message.LockToken == lockToken &&
                message.LockedUntilUtc > processedAtUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        message => message.Status,
                        OutboxMessageStatus.Completed)
                    .SetProperty(
                        message => message.ProcessedAtUtc,
                        processedAtUtc)
                    .SetProperty(
                        message => message.LockedUntilUtc,
                        (DateTime?)null)
                    .SetProperty(
                        message => message.LockToken,
                        (Guid?)null)
                    .SetProperty(
                        message => message.LastError,
                        (string?)null),
                cancellationToken);

        return affectedRows == 1;
    }

    public async Task<bool> FailAsync(
        Guid messageId,
        Guid lockToken,
        string error,
        DateTime failedAtUtc,
        DateTime nextAttemptAtUtc,
        int maximumAttempts,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            messageId,
            Guid.Empty);

        ArgumentOutOfRangeException.ThrowIfEqual(
            lockToken,
            Guid.Empty);

        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        ArgumentOutOfRangeException.ThrowIfLessThan(
            maximumAttempts,
            1);

        if (failedAtUtc.Kind != DateTimeKind.Utc ||
            nextAttemptAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Failure and retry times must be UTC.");
        }

        var lastError = error.Length > 2000
            ? error[..2000]
            : error;

        var affectedRows = await dbContext.OutboxMessages
            .Where(message =>
                message.Id == messageId &&
                message.Status == OutboxMessageStatus.Processing &&
                message.LockToken == lockToken &&
                message.LockedUntilUtc > failedAtUtc)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        message => message.Status,
                        message => message.AttemptCount >= maximumAttempts
                            ? OutboxMessageStatus.Failed
                            : OutboxMessageStatus.Pending)
                    .SetProperty(
                        message => message.NextAttemptAtUtc,
                        message => message.AttemptCount >= maximumAttempts
                            ? null
                            : nextAttemptAtUtc)
                    .SetProperty(
                        message => message.LockedUntilUtc,
                        (DateTime?)null)
                    .SetProperty(
                        message => message.LockToken,
                        (Guid?)null)
                    .SetProperty(
                        message => message.LastError,
                        lastError),
                cancellationToken);

        return affectedRows == 1;
    }
}
