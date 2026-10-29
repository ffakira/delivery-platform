using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Delivery.Api.Extensions;

public static class HostingServiceExtensions
{
    public const string CorsPolicy = "Frontend";

    public static IServiceCollection AddHosting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var origins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?? [];

        if (origins.Length == 0)
        {
            throw new InvalidOperationException(
                "At least one CORS origin must be configured.");
        }

        services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicy, policy =>
            {
                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        services.Configure<HostOptions>(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
