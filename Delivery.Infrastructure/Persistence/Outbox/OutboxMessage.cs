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

    public Guid? LockToken { get; private set; }

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
        LockToken = Guid.NewGuid();
        LockedUntilUtc = lockedUntilUtc;
        LastError = null;
    }

    public void Complete(
        Guid lockToken,
        DateTime processedAtUtc)
    {
        EnsureOwnership(lockToken);

        if (processedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Processing time must be UTC.",
                nameof(processedAtUtc));
        }

        Status = OutboxMessageStatus.Completed;
        ProcessedAtUtc = processedAtUtc;
        LockedUntilUtc = null;
        LockToken = null;
        LastError = null;
    }

    public void Fail(
        Guid lockToken,
        string error,
        DateTime failedAtUtc,
        DateTime nextAttemptAtUtc,
        int maximumAttempts)
    {
        EnsureOwnership(lockToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

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
        LockToken = null;

        if (AttemptCount >= maximumAttempts)
        {
            Status = OutboxMessageStatus.Failed;
            NextAttemptAtUtc = null;
            return;
        }

        Status = OutboxMessageStatus.Pending;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }

    private void EnsureOwnership(Guid lockToken)
    {
        if (Status != OutboxMessageStatus.Processing)
        {
            throw new InvalidOperationException(
                "Only processing messages can change state.");
        }

        if (lockToken == Guid.Empty ||
            LockToken != lockToken)
        {
            throw new InvalidOperationException(
                "The processing lock is no longer owned by this worker.");
        }
    }
}
