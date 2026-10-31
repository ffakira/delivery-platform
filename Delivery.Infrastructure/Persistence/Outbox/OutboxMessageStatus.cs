namespace Delivery.Infrastructure.Persistence.Outbox;

public enum OutboxMessageStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4
}
