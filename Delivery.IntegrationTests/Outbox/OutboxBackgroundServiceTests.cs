using System.Text.Json;
using Delivery.Infrastructure.Identity;
using Delivery.Infrastructure.Persistence;
using Delivery.Infrastructure.Persistence.Outbox;
using Delivery.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Delivery.IntegrationTests.Outbox;

[Collection(DatabaseCollection.Name)]
public sealed class OutboxBackgroundServiceTests(
    DatabaseFixture fixture)
{
    [Fact]
    public async Task ExecuteAsync_WithValidEmailConfirmation_CompletesMessageAndSendsEmail()
    {
        var user = await CreateUserAsync();

        var messageId = await SeedMessageAsync(
            "identity.email-confirmation",
            JsonSerializer.Serialize(new { userId = user.Id }));

        await RunUntilAsync(
            messageId,
            message => message.Status == OutboxMessageStatus.Completed);

        var persistedMessage = await FindMessageAsync(messageId);

        Assert.NotNull(persistedMessage);
        Assert.Equal(OutboxMessageStatus.Completed, persistedMessage.Status);
        Assert.Equal(1, persistedMessage.AttemptCount);
        Assert.NotNull(persistedMessage.ProcessedAtUtc);
        Assert.Null(persistedMessage.LastError);
        Assert.Null(persistedMessage.LockToken);
        Assert.Null(persistedMessage.LockedUntilUtc);

        var email = Assert.Single(fixture.EmailSender.Messages);

        Assert.Equal(user.Email, email.To);
        Assert.Contains("Confirm email address", email.HtmlBody);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownMessageType_SchedulesRetry()
    {
        var messageId = await SeedMessageAsync(
            "integration-test.unknown",
            """{"test":true}""");

        await RunUntilAsync(
            messageId,
            message =>
                message.Status == OutboxMessageStatus.Pending &&
                message.AttemptCount == 1);

        var persistedMessage = await FindMessageAsync(messageId);

        Assert.NotNull(persistedMessage);
        Assert.Equal(OutboxMessageStatus.Pending, persistedMessage.Status);
        Assert.Equal(1, persistedMessage.AttemptCount);
        Assert.NotNull(persistedMessage.NextAttemptAtUtc);
        Assert.True(persistedMessage.NextAttemptAtUtc > DateTime.UtcNow);

        Assert.Contains(
            "No outbox handler is registered",
            persistedMessage.LastError);

        Assert.Null(persistedMessage.LockToken);
        Assert.Null(persistedMessage.LockedUntilUtc);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMaximumAttemptsReached_MarksMessageAsFailed()
    {
        var messageId = await SeedMessageAtMaximumRetryAttemptAsync();

        await RunUntilAsync(
            messageId,
            message => message.Status == OutboxMessageStatus.Failed);

        var persistedMessage = await FindMessageAsync(messageId);

        Assert.NotNull(persistedMessage);
        Assert.Equal(OutboxMessageStatus.Failed, persistedMessage.Status);
        Assert.Equal(5, persistedMessage.AttemptCount);
        Assert.NotNull(persistedMessage.LastError);
        Assert.Null(persistedMessage.NextAttemptAtUtc);
        Assert.Null(persistedMessage.LockToken);
        Assert.Null(persistedMessage.LockedUntilUtc);
    }

    private async Task RunUntilAsync(
        Guid messageId,
        Func<OutboxMessage, bool> condition)
    {
        var service = new OutboxBackgroundService(
            fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            fixture.Services.GetRequiredService<TimeProvider>(),
            NullLogger<OutboxBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);

        try
        {
            var timeoutAt = DateTime.UtcNow.AddSeconds(10);

            while (DateTime.UtcNow < timeoutAt)
            {
                var message = await FindMessageAsync(messageId);

                if (message is not null && condition(message))
                {
                    return;
                }

                await Task.Delay(50);
            }

            var finalMessage = await FindMessageAsync(messageId);

            Assert.Fail(
                $"Outbox message '{messageId}' did not reach the expected state. " +
                $"Current status: {finalMessage?.Status.ToString() ?? "Not found"}. " +
                $"Attempts: {finalMessage?.AttemptCount.ToString() ?? "N/A"}.");
        }
        finally
        {
            using var cancellationTokenSource =
                new CancellationTokenSource(TimeSpan.FromSeconds(5));

            await service.StopAsync(cancellationTokenSource.Token);
        }
    }

    private async Task<TestUser> CreateUserAsync()
    {
        var email = $"outbox-test-{Guid.NewGuid():N}@example.com";

        await using var scope = fixture.Services.CreateAsyncScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email
        };

        var result = await userManager.CreateAsync(user);

        Assert.True(
            result.Succeeded,
            string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));

        return new TestUser(user.Id, email);
    }

    private async Task<Guid> SeedMessageAsync(
        string type,
        string payload)
    {
        var message = OutboxMessage.Create(
            type,
            payload,
            DateTime.UtcNow.AddMinutes(-1));

        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.OutboxMessages.ExecuteDeleteAsync();

        dbContext.OutboxMessages.Add(message);

        await dbContext.SaveChangesAsync();

        fixture.EmailSender.Clear();
        fixture.EmailSender.ShouldFail = false;

        return message.Id;
    }

    private async Task<Guid> SeedMessageAtMaximumRetryAttemptAsync()
    {
        var now = DateTime.UtcNow;

        var message = OutboxMessage.Create(
            "integration-test.unknown",
            """{"test":true}""",
            now.AddMinutes(-30));

        // Simulate four previous failed processing attempts.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var claimedAt = now.AddMinutes(-10);

            message.Claim(
                claimedAt,
                claimedAt.AddMinutes(1));

            var lockToken = message.LockToken
                ?? throw new InvalidOperationException(
                    "Claimed message must have a lock token.");

            message.Fail(
                lockToken,
                "Previous processing failure.",
                claimedAt.AddSeconds(1),
                claimedAt.AddMinutes(2),
                maximumAttempts: 10);
        }

        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.OutboxMessages.ExecuteDeleteAsync();

        dbContext.OutboxMessages.Add(message);

        await dbContext.SaveChangesAsync();

        fixture.EmailSender.Clear();
        fixture.EmailSender.ShouldFail = false;

        return message.Id;
    }

    private async Task<OutboxMessage?> FindMessageAsync(Guid messageId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        return await dbContext.OutboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(message => message.Id == messageId);
    }

    private sealed record TestUser(Guid Id, string Email);
}
