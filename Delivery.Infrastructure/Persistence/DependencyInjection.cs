using Delivery.Application.Abstractions.Outbox;
using Delivery.Application.Abstractions.Persistence;
using Delivery.Application.Restaurants.Repositories;
using Delivery.Infrastructure.Persistence.Outbox;
using Delivery.Infrastructure.Persistence.Repositories.Restaurants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IRestaurantRepository, EfRestaurantRepository>();
        services.AddScoped<IOutboxMessageWriter, EfOutboxMessageWriter>();

        return services;
    }
}
