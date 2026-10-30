using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Delivery.IntegrationTests.Infrastructure;

public sealed class DeliveryApiFactory : WebApplicationFactory<Program>
{
    public DeliveryApiFactory(string connectionString)
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__DefaultConnection",
            connectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Cors:AllowedOrigins:0"] = "https://localhost"
                });
        });
    }
}