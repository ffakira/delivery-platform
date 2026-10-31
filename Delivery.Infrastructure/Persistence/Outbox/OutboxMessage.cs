namespace Delivery.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public OutboxMessageStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? NextAttemptAtUtc { get; private set; }

    public DateTime? LockedUntilUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Create(
        string type,
        string payload,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Creation time must be UTC.",
                nameof(createdAtUtc));
        }

        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = payload,
            Status = OutboxMessageStatus.Pending,
            AttemptCount = 0,
            CreatedAtUtc = createdAtUtc,
            NextAttemptAtUtc = createdAtUtc
        };
    }

    public void Claim(
        DateTime claimedAtUtc,
        DateTime lockedUntilUtc)
    {
        if (Status is not OutboxMessageStatus.Pending
            and not OutboxMessageStatus.Processing)
        {
            throw new InvalidOperationException(
                "Only pending or expired processing messages can be claimed.");
        }

        if (claimedAtUtc.Kind != DateTimeKind.Utc ||
            lockedUntilUtc.Kind != DateTimeKind.Utc ||
            lockedUntilUtc <= claimedAtUtc)
        {
            throw new ArgumentException(
                "The processing lease must be a valid UTC time range.");
        }

        Status = OutboxMessageStatus.Processing;
        AttemptCount++;
        LockedUntilUtc = lockedUntilUtc;
        LastError = null;
    }

    public void Complete(DateTime processedAtUtc)
    {
        if (Status != OutboxMessageStatus.Processing)
        {
            throw new InvalidOperationException(
                "Only processing messages can be completed.");
        }

        if (processedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Processing time must be UTC.",
                nameof(processedAtUtc));
        }

        Status = OutboxMessageStatus.Completed;
        ProcessedAtUtc = processedAtUtc;
        LockedUntilUtc = null;
        LastError = null;
    }

    public void Fail(
        string error,
        DateTime failedAtUtc,
        DateTime nextAttemptAtUtc,
        int maximumAttempts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        if (Status != OutboxMessageStatus.Processing)
        {
            throw new InvalidOperationException(
                "Only processing messages can fail.");
        }

        if (maximumAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAttempts));
        }

        if (failedAtUtc.Kind != DateTimeKind.Utc ||
            nextAttemptAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Failure and retry times must be UTC.");
        }

        LastError = error.Length > 2000
            ? error[..2000]
            : error;

        LockedUntilUtc = null;

        if (AttemptCount >= maximumAttempts)
        {
            Status = OutboxMessageStatus.Failed;
            NextAttemptAtUtc = null;
            return;
        }

        Status = OutboxMessageStatus.Pending;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }
}
