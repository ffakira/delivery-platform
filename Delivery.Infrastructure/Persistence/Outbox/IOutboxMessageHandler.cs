namespace Delivery.Infrastructure.Persistence.Outbox;

public interface IOutboxMessageHandler
{
    string MessageType { get; }

    Task HandleAsync(
        string payload,
        CancellationToken cancellationToken);
}
