using System.Collections.Concurrent;
using Delivery.Application.Abstractions.Email;

namespace Delivery.IntegrationTests.Infrastructure;

public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyCollection<EmailMessage> Messages => _messages.ToArray();

    public bool ShouldFail { get; set; }

    public Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        cancellationToken.ThrowIfCancellationRequested();

        if (ShouldFail)
        {
            throw new InvalidOperationException(
                "Simulated email delivery failure.");
        }

        _messages.Enqueue(message);

        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }
}
