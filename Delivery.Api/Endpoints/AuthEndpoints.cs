
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Delivery.Api.Endpoints.Security;
using Delivery.Application.Abstractions.Email;
using Delivery.Infrastructure.Email;
using Delivery.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Delivery.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapGet("/csrf-token", GetCsrfToken)
            .AllowAnonymous();

        group.MapPost("/login", Login)
            .AllowAnonymous();

        group.MapPost("/register", Register)
            .AllowAnonymous();

        group.MapPost("/resend-confirmation", ResendConfirmation)
            .AllowAnonymous();

        group.MapGet("/confirm-email", ShowEmailConfirmation)
            .AllowAnonymous();

        group.MapPost("/confirm-email", ConfirmEmail)
            .AllowAnonymous();

        group.MapPost("/logout", Logout)
            .AllowAnonymous();

        group.MapGet("/me", GetCurrentUser)
            .RequireAuthorization();

        return endpoints;
    }

    private static IResult GetCsrfToken(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);

        return Results.Ok(new { requestToken = tokens.RequestToken });
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        if (!await antiforgery.IsAntiforgeryValidAsync(context))
        {
            return Results.BadRequest(
                new { message = "Invalid anti-forgery token." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(
                new { message = "Email and password are required." });
        }

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return InvalidCredentials();
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            request.Password,
            isPersistent: false,
            lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        return Results.NoContent();
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        ILogger<RegisterRequest> logger,
        CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsAntiforgeryValidAsync(context))
        {
            return Results.BadRequest(
                new { message = "Invalid anti-forgery token." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(
                new { message = "Email and password are required." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var result = await userManager.CreateAsync(
            user,
            request.Password);

        if (!result.Succeeded)
        {
            return Results.BadRequest(new
            {
                message = "Registration failed.",
                errors = result.Errors.Select(error => error.Description)
            });
        }

        try
        {
            await SendConfirmationEmailAsync(
                user,
                userManager,
                emailSender,
                emailOptions.Value,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to send confirmation email to user {UserId}.",
                user.Id);

            // The account exists, but confirmation email delivery failed.
            // The user can request another confirmation email.
            return Results.Created(
                "/api/auth/me",
                new
                {
                    id = user.Id,
                    email = user.Email,
                    emailConfirmationRequired = true,
                    confirmationEmailSent = false
                });
        }

        return Results.Created(
            "/api/auth/me",
            new
            {
                id = user.Id,
                email = user.Email,
                emailConfirmationRequired = true,
                confirmationEmailSent = true
            });
    }

    private static async Task<IResult> Logout(
        HttpContext context,
        IAntiforgery antiforgery,
        SignInManager<ApplicationUser> signInManager)
    {
        if (!await antiforgery.IsAntiforgeryValidAsync(context))
        {
            return Results.BadRequest(
                new { message = "Invalid anti-forgery token." });
        }

        await signInManager.SignOutAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUser(
        UserManager<ApplicationUser> userManager,
        HttpContext context)
    {
        var user = await userManager.GetUserAsync(context.User);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var roles = await userManager.GetRolesAsync(user);

        return Results.Ok(new CurrentUserResponse(
            user.Id,
            user.Email,
            roles));
    }

    private static async Task<IResult> ResendConfirmation(
        ResendConfirmationEmailRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        ILogger<ResendConfirmationEmailRequest> logger,
        CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsAntiforgeryValidAsync(context))
        {
            return Results.BadRequest(
                new { message = "Invalid anti-forgery token." });
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(
                new { message = "Email is required" });
        }

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is not null && !user.EmailConfirmed)
        {
            try
            {
                await SendConfirmationEmailAsync(
                    user,
                    userManager,
                    emailSender,
                    emailOptions.Value,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to resend email confirmation to user {UserId}.",
                    user.Id);
            }
        }

        return Results.Accepted(
            value: new
            {
                message = "If the account exists and requires confirmation, a confirmation email will be sent."
            });
    }

    private static async Task SendConfirmationEmailAsync(
        ApplicationUser user,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        EmailOptions options,
        CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(
            user);
        
        var encodedToken = WebEncoders.Base64UrlEncode(
            System.Text.Encoding.UTF8.GetBytes(token));

        var confirmationUrl = QueryHelpers.AddQueryString(
            options.ConfirmationUrl,
            new Dictionary<string, string?>
            {
                ["userId"] = user.Id.ToString(),
                ["token"] = encodedToken
            });

        var message = new EmailMessage(
            user.Email!,
            "Confirm your Delivery Platform account",
            $"""
             Welcome to Delivery Platform!

             Please confirm your email address by opening this link:

             {confirmationUrl}

             If you did not create this account, you can ignore this email.
             """,
            $"""
             <h1>Welcome to Delivery Platform!</h1>
             <p>Please confirm your email address:</p>
             <p><a href="{HtmlEncoder.Default.Encode(confirmationUrl)}">Confirm email address</a></p>
             <p>If you did not create this account, you can ignore this email.</p>
             """);
        
        await emailSender.SendAsync(message, cancellationToken);
    }

    private static IResult ShowEmailConfirmation(string? userId, string? token)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(token) ||
            !Guid.TryParse(userId, out _))
        {
            return Results.BadRequest(
                "The confirmation link is invalid.");
        }
        
        var encodedUserId = HtmlEncoder.Default.Encode(userId);
        var encodedToken =  HtmlEncoder.Default.Encode(token);
        
        var html = $"""
                    <!DOCTYPE html>
                    <html lang="en">
                    <head>
                        <meta charset="utf-8">
                        <meta name="viewport" content="width=device-width, initial-scale=1">
                        <title>Confirm your email</title>
                    </head>
                    <body>
                        <main>
                            <h1>Confirm your email address</h1>
                            <p>Click the button below to confirm your account.</p>
                            <form method="post" action="/api/auth/confirm-email">
                                <input type="hidden" name="userId" value="{encodedUserId}">
                                <input type="hidden" name="token" value="{encodedToken}">
                                <button type="submit">Confirm email address</button>
                            </form>
                        </main>
                    </body>
                    </html>
                    """;

        return Results.Content(
            html,
            "text/html",
            System.Text.Encoding.UTF8);
    }

    private static async Task<IResult> ConfirmEmail(
        HttpRequest request,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);

        var userId = form["userId"].ToString();
        var encodedToken = form["token"].ToString();

        if (!Guid.TryParse(userId, out var parsedUserId) || string.IsNullOrWhiteSpace(encodedToken))
        {
            return Results.BadRequest(
                "The confirmation link is invalid.");
        }

        var user = await userManager.FindByIdAsync(
            parsedUserId.ToString());

        if (user is null)
        {
            return Results.BadRequest(
                "The confirmation link is invalid or has expired.");
        }

        if (user.EmailConfirmed)
        {
            return Results.Content(
                "<h1>Email already confirmed</h1>",
                "text/html");
        }

        try
        {
            var token = System.Text.Encoding.UTF8.GetString(
                WebEncoders.Base64UrlDecode(encodedToken));

            var result = await userManager.ConfirmEmailAsync(
                user,
                token);

            if (!result.Succeeded)
            {
                return Results.BadRequest(
                    "The confirmation link is invalid or has expired.");
            }
        }
        catch (FormatException)
        {
            return Results.BadRequest(
                "The confirmation link is invalid or has expired.");
        }
        
        return Results.Content(
            "<h1>Your email has been confirmed successfully.</h1>",
            "text/html");
    }

    private static IResult InvalidCredentials()
    {
        return Results.Json(
            new { message = "Username or password is incorrect." },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    private sealed record LoginRequest(
        string? Email,
        string? Password);

    private sealed record RegisterRequest(
        string? Email,
        string? Password);

    private sealed record CurrentUserResponse(
        Guid Id,
        string? Email,
        IList<string> Roles);
}
