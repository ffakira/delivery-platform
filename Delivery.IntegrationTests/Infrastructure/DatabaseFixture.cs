using Delivery.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Delivery.IntegrationTests.Infrastructure;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("delivery_test")
            .WithUsername("delivery")
            .WithPassword("delivery_test_password")
            .Build();

    private DeliveryApiFactory? _factory;

    public HttpClient Client { get; private set; } = null!;

    public IServiceProvider Services =>
        _factory?.Services
        ?? throw new InvalidOperationException(
            "The database fixture has not been initialized.");

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new DeliveryApiFactory(
            _postgres.GetConnectionString());

        Client = CreateClient();

        await using var scope = _factory.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    public HttpClient CreateClient()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException(
                "The database fixture has not been initialized.");
        }

        return _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    public RecordingEmailSender EmailSender =>
        Services.GetRequiredService<RecordingEmailSender>();
}
