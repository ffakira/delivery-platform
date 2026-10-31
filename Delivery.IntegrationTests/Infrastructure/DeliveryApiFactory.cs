using Delivery.Application.Abstractions.Email;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Delivery.IntegrationTests.Infrastructure;

public sealed class DeliveryApiFactory : WebApplicationFactory<Program>
{
    public DeliveryApiFactory(string connectionString)
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            connectionString);

        Environment.SetEnvironmentVariable(
            "Cors__AllowedOrigins__0",
            "https://localhost");

        Environment.SetEnvironmentVariable(
            "Email__ConfirmationUrl",
            "https://localhost/api/auth/confirm-email");
    }

    public RecordingEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();

            services.AddSingleton(EmailSender);
            services.AddSingleton<IEmailSender>(EmailSender);
        });
    }
}
