using Delivery.Application.Abstractions.Security;
using Delivery.Infrastructure.Persistence;
using Delivery.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);

        services.AddSingleton<
            IInvitationTokenGenerator,
        SecureInvitationTokenGenerator>();

        return services;
    }
}
