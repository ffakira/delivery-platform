using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Delivery.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

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

        group.MapPost("/logout", Logout)
            .AllowAnonymous();

        group.MapGet("/me", GetCurrentUser)
            .RequireAuthorization();

        return endpoints;
    }

    private static IResult GetCsrfToken(HttpContext context, IAntiforgery antiforgery)
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
        if (!await IsAntiforgeryValid(context, antiforgery))
        {
            return Results.BadRequest(new { message = "Invalid anti-forgery token." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { message = "Email and password is required" });
        }

        var user = await userManager.FindByEmailAsync(request.Email);

        if (user is null)
        {
            return InvalidCredentials();
        }

        var result =
            await signInManager.PasswordSignInAsync(user, request.Password, isPersistent: false,
                lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            return InvalidCredentials();
        }

        return Results.NoContent();
    }

    private static async Task<IResult> Register(RegisterRequest request, HttpContext context, IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager)
    {
        if (!await IsAntiforgeryValid(context, antiforgery))
        {
            return Results.BadRequest(new { message = "Invalid anti-forgery token." });
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { message = "Email and password is required" });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return Results.BadRequest(new
                { message = "Registration failed.", errors = result.Errors.Select(error => error.Description) });
        }

        return Results.Created(
            "/api/auth/me", new
            {
                id = user.Id,
                email = user.Email
            });
    }

    private static async Task<IResult> Logout(HttpContext context, IAntiforgery antiforgery,
        SignInManager<ApplicationUser> signInManager)
    {
        if (!await IsAntiforgeryValid(context, antiforgery))
        {
            return Results.BadRequest(new { message = "Invalid anti-forgery token." });
        }

        await signInManager.SignOutAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUser(UserManager<ApplicationUser> userManager, HttpContext context)
    {
        var user = await userManager.GetUserAsync(context.User);

        if (user is null)
        {
            return Results.Unauthorized();
        }
        
        var roles = await userManager.GetRolesAsync(user);

        return Results.Ok(new CurrentUserResponse(
            user.Id,
            user.Email, roles));
    }

    private static async Task<bool> IsAntiforgeryValid(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static IResult InvalidCredentials()
    {
        return Results.Json(
            new { message = "Username or password is incorrect" },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    private sealed record LoginRequest(string? Email, string? Password);

    private sealed record RegisterRequest(string? Email, string? Password);

    private sealed record CurrentUserResponse(Guid Id, string? Email, IList<string> Roles);
}