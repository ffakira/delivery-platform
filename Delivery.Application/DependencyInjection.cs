using Delivery.Application.Restaurants.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<CreateRestaurantService>();
        services.AddScoped<ListMyRestaurantsService>();
        services.AddScoped<GetRestaurantDetailsService>();

        return services;
    }
}
