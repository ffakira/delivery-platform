using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Delivery.Application.Restaurants.Queries;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;
using Delivery.Domain.Restaurants.ValueObjects;
using Delivery.Infrastructure.Persistence;
using Delivery.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.IntegrationTests.Restaurants;

[Collection(DatabaseCollection.Name)]
public sealed class GetRestaurantDetailsTests(DatabaseFixture fixture)
{
    private static readonly DateTime CreatedAtUtc =
        new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetRestaurantDetails_WithActiveOwner_ReturnsRestaurantDetails()
    {
        using var owner = await fixture.CreateAuthenticatedClientAsync();

        var restaurantId = await SeedRestaurantAsync(owner.UserId);

        using var response = await owner.Client.GetAsync(
            $"/api/restaurants/{restaurantId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<RestaurantDetails>();

        Assert.NotNull(result);
        Assert.Equal(restaurantId, result.Id);
        Assert.Equal("Sample Restaurant", result.Name);
        Assert.Equal("Rua das Flores", result.Street);
        Assert.Equal("123", result.Number);
        Assert.Equal("Sala 2", result.Complement);
        Assert.Equal("Centro", result.Neighborhood);
        Assert.Equal("Tubarão", result.City);
        Assert.Equal("SC", result.State);
        Assert.Equal("88701000", result.PostalCode);
        Assert.Equal(RestaurantRole.Owner, result.Role);
        Assert.Equal(CreatedAtUtc, result.CreatedAtUtc);
    }

    [Fact]
    public async Task GetRestaurantDetails_WithActiveManager_ReturnsRestaurantDetails()
    {
        using var owner = await fixture.CreateAuthenticatedClientAsync();
        using var manager = await fixture.CreateAuthenticatedClientAsync();

        var restaurantId = await SeedRestaurantAsync(
            owner.UserId,
            manager.UserId,
            MembershipStatus.Active,
            RestaurantRole.Manager);

        using var response = await manager.Client.GetAsync(
            $"/api/restaurants/{restaurantId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<RestaurantDetails>();

        Assert.NotNull(result);
        Assert.Equal(restaurantId, result.Id);
        Assert.Equal(RestaurantRole.Manager, result.Role);
    }

    [Fact]
    public async Task GetRestaurantDetails_WithInvitedMembership_ReturnsNotFound()
    {
        using var owner = await fixture.CreateAuthenticatedClientAsync();
        using var invitedUser = await fixture.CreateAuthenticatedClientAsync();

        var restaurantId = await SeedRestaurantAsync(
            owner.UserId,
            invitedUser.UserId,
            MembershipStatus.Invited);

        using var response = await invitedUser.Client.GetAsync(
            $"/api/restaurants/{restaurantId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRestaurantDetails_WithSuspendedMembership_ReturnsNotFound()
    {
        using var owner = await fixture.CreateAuthenticatedClientAsync();
        using var suspendedUser = await fixture.CreateAuthenticatedClientAsync();

        var restaurantId = await SeedRestaurantAsync(
            owner.UserId,
            suspendedUser.UserId,
            MembershipStatus.Suspended);

        using var response = await suspendedUser.Client.GetAsync(
            $"/api/restaurants/{restaurantId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRestaurantDetails_WithUserWithoutMembership_ReturnsNotFound()
    {
        using var owner = await fixture.CreateAuthenticatedClientAsync();
        using var unrelatedUser = await fixture.CreateAuthenticatedClientAsync();

        var restaurantId = await SeedRestaurantAsync(owner.UserId);

        using var response = await unrelatedUser.Client.GetAsync(
            $"/api/restaurants/{restaurantId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRestaurantDetails_WithUnknownRestaurantId_ReturnsNotFound()
    {
        using var user = await fixture.CreateAuthenticatedClientAsync();

        using var response = await user.Client.GetAsync(
            $"/api/restaurants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRestaurantDetails_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync(
            $"/api/restaurants/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Guid> SeedRestaurantAsync(
        Guid ownerUserId,
        Guid? memberUserId = null,
        MembershipStatus? membershipStatus = null,
        RestaurantRole memberRole = RestaurantRole.Staff)
    {
        var restaurant = Restaurant.Create(
            "Sample Restaurant",
            Cnpj.Create(CreateValidCnpj()),
            RestaurantAddress.Create(
                "Rua das Flores",
                "123",
                "Sala 2",
                "Centro",
                "Tubarão",
                "SC",
                "88701000"),
            ownerUserId,
            CreatedAtUtc);

        if (memberUserId.HasValue)
        {
            var membership = restaurant.InviteMember(
                memberUserId.Value,
                memberRole,
                CreatedAtUtc);

            switch (membershipStatus)
            {
                case MembershipStatus.Active:
                    membership.Activate(CreatedAtUtc);
                    break;

                case MembershipStatus.Suspended:
                    membership.Activate(CreatedAtUtc);
                    membership.Suspend();
                    break;

                case MembershipStatus.Invited:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(membershipStatus));
            }
        }

        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.Set<Restaurant>().AddAsync(restaurant);
        await dbContext.SaveChangesAsync();

        return restaurant.Id;
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
