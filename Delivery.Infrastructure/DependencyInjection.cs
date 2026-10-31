using Delivery.Application.Abstractions.Email;
using Delivery.Application.Abstractions.Security;
using Delivery.Infrastructure.Email;
using Delivery.Infrastructure.Persistence;
using Delivery.Infrastructure.Persistence.Outbox;
using Delivery.Infrastructure.Persistence.Outbox.Handlers;
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

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Host),
                "Email host is required.")
            .Validate(
                options => options.Port is > 0 and <= 65535,
                "Email port must be between 1 and 65535.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.FromAddress),
                "Email sender address is required.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.FromName),
                "Email sender name is required.")
            .Validate(
                options =>
                    string.IsNullOrWhiteSpace(options.Username) ==
                    string.IsNullOrWhiteSpace(options.Password),
                "Email username and password must either both be configured or both be empty.")
            .Validate(
                options => Uri.TryCreate(
                    options.ConfirmationUrl,
                    UriKind.Absolute,
                    out var uri) &&
                    (uri.Scheme == Uri.UriSchemeHttps ||
                     uri.Scheme == Uri.UriSchemeHttp),
                "A valid absolute email confirmation URL is required.")
            .ValidateOnStart();

        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.AddScoped<
            IOutboxMessageHandler,
            EmailConfirmationMessageHandler>();

        if (configuration.GetValue<bool>("Outbox:Enabled", true))
        {
            services.AddHostedService<OutboxBackgroundService>();
        }

        return services;
    }
}
