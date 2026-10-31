using Microsoft.EntityFrameworkCore;

namespace Delivery.Infrastructure.Persistence.Outbox;

public class EfOutboxMessageClaimer(
    ApplicationDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
        int batchSize,
        TimeSpan processingLease,
        CancellationToken cancellationToken)
    {
        if (batchSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                "Batch size must be greater than zero.");
        }

        if (processingLease <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(processingLease),
                "Processing lease must be greater than zero.");
        }

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var lockedUntilUtc = nowUtc.Add(processingLease);

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        var messages = await dbContext.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                FROM "OutboxMessages"
                WHERE
                    (
                        "Status" = {(int)OutboxMessageStatus.Pending}
                        AND "NextAttemptAtUtc" <= {nowUtc}
                    )
                    OR
                    (
                        "Status" = {(int)OutboxMessageStatus.Processing}
                        AND "LockedUntilUtc" <= {nowUtc}
                    )
                ORDER BY "CreatedAtUtc", "Id"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
            """)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.Claim(nowUtc, lockedUntilUtc);
        }

        if (messages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return messages;
    }
}