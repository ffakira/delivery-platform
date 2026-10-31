using System.Text.Json;
using Delivery.Application.Abstractions.Outbox;

namespace Delivery.Infrastructure.Persistence.Outbox;

public sealed class EfOutboxMessageWriter(
    ApplicationDbContext dbContext) : IOutboxMessageWriter
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public Task EnqueueAsync<TPayload>(
        string type,
        TPayload payload,
        DateTime createdAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var serializedPayload = JsonSerializer.Serialize(
            payload,
            JsonOptions);

        var message = OutboxMessage.Create(
            type,
            serializedPayload,
            createdAtUtc);

        dbContext.OutboxMessages.Add(message);

        return Task.CompletedTask;
    }
}
