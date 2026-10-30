
using System.Security.Cryptography;
using System.Text;
using Delivery.Application.Restaurants.Commands;
using Delivery.Application.Restaurants.Services;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;
using Delivery.Infrastructure.Persistence;
using Delivery.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.IntegrationTests.Restaurants;

[Collection(DatabaseCollection.Name)]
public sealed class CreateRestaurantInvitationTests(
    DatabaseFixture fixture)
{
    [Fact]
    public async Task CreateInvitation_AsOwner_PersistsInvitationWithHashedToken()
    {
        using var user = await fixture.CreateAuthenticatedClientAsync();

        await using var scope = fixture.Services.CreateAsyncScope();

        var services = scope.ServiceProvider;

        var createRestaurantService = services
            .GetRequiredService<CreateRestaurantService>();

        var createInvitationService = services
            .GetRequiredService<CreateRestaurantInvitationService>();

        var dbContext = services
            .GetRequiredService<ApplicationDbContext>();

        var restaurantId = await createRestaurantService.ExecuteAsync(
            CreateRestaurantCommand(),
            user.UserId);

        var result = await createInvitationService.ExecuteAsync(
            new CreateRestaurantInvitationCommand(
                restaurantId,
                "  invited@example.com  ",
                RestaurantRole.Manager),
            user.UserId);

        var expectedTokenHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(result.Token)));

        var invitation = await dbContext.RestaurantInvitations
            .AsNoTracking()
            .SingleOrDefaultAsync(
                invitation => invitation.Id == result.InvitationId);

        Assert.NotNull(invitation);

        Assert.Equal(restaurantId, invitation.RestaurantId);
        Assert.Equal("invited@example.com", invitation.Email);
        Assert.Equal(RestaurantRole.Manager, invitation.Role);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);

        Assert.Equal(expectedTokenHash, invitation.TokenHash);
        Assert.NotEqual(result.Token, invitation.TokenHash);

        Assert.Equal(user.UserId, invitation.InvitedByUserId);
        Assert.Null(invitation.AcceptedByUserId);
        Assert.Null(invitation.AcceptedAtUtc);
        Assert.Null(invitation.RevokedAtUtc);

        var expirationDifference = Math.Abs(
            (result.ExpiresAtUtc - invitation.ExpiresAtUtc).Ticks);

        Assert.True(
            expirationDifference <= 10,
            $"Expected expiration {result.ExpiresAtUtc:O}, " +
            $"but database returned {invitation.ExpiresAtUtc:O}.");
        Assert.True(invitation.ExpiresAtUtc > invitation.CreatedAtUtc);
    }

    [Fact]
    public async Task CreateInvitation_PersistsInvitationInRestaurantAggregate()
    {
        using var user = await fixture.CreateAuthenticatedClientAsync();

        await using var scope = fixture.Services.CreateAsyncScope();

        var services = scope.ServiceProvider;

        var createRestaurantService = services
            .GetRequiredService<CreateRestaurantService>();

        var createInvitationService = services
            .GetRequiredService<CreateRestaurantInvitationService>();

        var restaurantId = await createRestaurantService.ExecuteAsync(
            CreateRestaurantCommand(),
            user.UserId);

        var result = await createInvitationService.ExecuteAsync(
            new CreateRestaurantInvitationCommand(
                restaurantId,
                "member@example.com",
                RestaurantRole.Staff),
            user.UserId);

        var dbContext = services
            .GetRequiredService<ApplicationDbContext>();

        var restaurant = await dbContext.Restaurants
            .AsNoTracking()
            .Include(restaurant => restaurant.Invitations)
            .SingleOrDefaultAsync(
                restaurant => restaurant.Id == restaurantId);

        Assert.NotNull(restaurant);

        var invitation = Assert.Single(restaurant.Invitations);

        Assert.Equal(result.InvitationId, invitation.Id);
        Assert.Equal(restaurantId, invitation.RestaurantId);
        Assert.Equal("member@example.com", invitation.Email);
        Assert.Equal(RestaurantRole.Staff, invitation.Role);
    }

    private static CreateRestaurantCommand CreateRestaurantCommand()
    {
        var cnpj = CreateValidCnpj();

        return new CreateRestaurantCommand(
            "Integration Test Restaurant",
            cnpj,
            "Main Street",
            "100",
            null,
            "Downtown",
            "Florianopolis",
            "SC",
            "88010000");
    }

    private static string CreateValidCnpj()
    {
        var baseNumber =
            $"123{RandomNumberGenerator.GetInt32(0, 1_000_000_000):D9}";

        var firstDigit = CalculateCnpjCheckDigit(baseNumber);

        var secondDigit = CalculateCnpjCheckDigit(
            baseNumber + firstDigit);

        return $"{baseNumber}{firstDigit}{secondDigit}";
    }

    private static int CalculateCnpjCheckDigit(string value)
    {
        var sum = 0;
        var weight = 2;

        for (var i = value.Length - 1; i >= 0; i--)
        {
            sum += (value[i] - '0') * weight;
            weight = weight == 9 ? 2 : weight + 1;
        }

        var remainder = sum % 11;

        return remainder < 2 ? 0 : 11 - remainder;
    }
}
