using System;
using System.Threading;
using System.Threading.Tasks;
using Delivery.Api.Endpoints.Security;
using Delivery.Application.Restaurants.Commands;
using Delivery.Application.Restaurants.Services;
using Delivery.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;

namespace Delivery.Api.Endpoints;

public static class RestaurantEndpoints
{
    public static IEndpointRouteBuilder MapRestaurantEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("api/restaraunts")
            .RequireAuthorization();

        group.MapPost("/", CreateRestaurant);

        return endpoints;
    }
    
    private static async Task<IResult> CreateRestaurant(
        CreateRestaurantRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager,
        CreateRestaurantService createRestaurantService,
        CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsAntiforgeryValidAsync(context))
        {
            return Results.BadRequest(
                new { message = "Invalid anti-forgery token. "});
        }
        
        var user = await userManager.GetUserAsync(context.User);

        if (user is null)
        {
            return Results.Unauthorized();
        }

        var command = new CreateRestaurantCommand(
            request.Name,
            request.Cnpj,
            request.Street,
            request.Number,
            request.Complement,
            request.Neighborhood,
            request.City,
            request.State,
            request.PostalCode);

        try
        {
            var restaurantId = await createRestaurantService.ExecuteAsync(
                command,
                user.Id,
                cancellationToken);

            return Results.Created(
                $"/api/restaraunts/{restaurantId}",
                new { id = restaurantId });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(
                new { message = exception.Message });
        }
    }

    private sealed record CreateRestaurantRequest(
        string Name,
        string Cnpj,
        string Street,
        string Number,
        string? Complement,
        string Neighborhood,
        string City,
        string State,
        string PostalCode);
}