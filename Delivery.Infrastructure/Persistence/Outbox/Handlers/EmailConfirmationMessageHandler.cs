using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Delivery.Application.Abstractions.Email;
using Delivery.Infrastructure.Email;
using Delivery.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Delivery.Infrastructure.Persistence.Outbox.Handlers;

public sealed class EmailConfirmationMessageHandler(
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    IOptions<EmailOptions> emailOptions)
    : IOutboxMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private const string Subject =
        "Confirm your Delivery Platform account";

    public string MessageType => "identity.email-confirmation";

    public async Task HandleAsync(
        string payload,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        var message = JsonSerializer.Deserialize<EmailConfirmationPayload>(
            payload,
            JsonOptions);

        if (message is null || message.UserId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "The email confirmation message payload is invalid.");
        }

        var user = await userManager.FindByIdAsync(
            message.UserId.ToString());

        if (user is null)
        {
            throw new InvalidOperationException(
                $"User '{message.UserId}' was not found.");
        }

        if (user.EmailConfirmed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException(
                $"User '{message.UserId}' does not have an email address.");
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(
            user);

        var encodedToken = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(token));

        var confirmationUrl = QueryHelpers.AddQueryString(
            emailOptions.Value.ConfirmationUrl,
            new Dictionary<string, string?>
            {
                ["userId"] = user.Id.ToString(),
                ["token"] = encodedToken
            });

        var email = new EmailMessage(
            user.Email,
            Subject,
            $"""
             Welcome to Delivery Platform!

             Please confirm your email address by opening this link:

             {confirmationUrl}

             If you did not create this account, you can ignore this email.
             """,
            $"""
             <h1>Welcome to Delivery Platform!</h1>
             <p>Please confirm your email address:</p>
             <p>
                 <a href="{HtmlEncoder.Default.Encode(confirmationUrl)}">
                     Confirm email address
                 </a>
             </p>
             <p>If you did not create this account, you can ignore this email.</p>
             """);

        await emailSender.SendAsync(email, cancellationToken);
    }

    private sealed record EmailConfirmationPayload(Guid UserId);
}
