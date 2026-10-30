using Microsoft.AspNetCore.Mvc.Testing;

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
    }
}
