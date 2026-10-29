using System;
using Delivery.Api.Infrastructure.Identity;
using Delivery.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.Api.Extensions;

public static class IdentityServiceExtensions
{
    public static IServiceCollection AddApplicationIdentity(
        this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        return services;
    }
}