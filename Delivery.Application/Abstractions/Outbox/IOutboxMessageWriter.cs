namespace Delivery.Application.Abstractions.Outbox;

public interface IOutboxMessageWriter
{
    Task EnqueueAsync<TPayload>(
        string type,
        TPayload payload,
        DateTime createdAtUtc,
        CancellationToken cancellationToken = default);
}
