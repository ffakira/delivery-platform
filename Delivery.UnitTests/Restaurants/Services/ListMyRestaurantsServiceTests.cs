using Delivery.Application.Restaurants.Queries;
using Delivery.Application.Restaurants.Services;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;
using Delivery.Domain.Restaurants.ValueObjects;
using Delivery.UnitTests.Restaurants.Fakes;

namespace Delivery.UnitTests.Restaurants.Services;

public sealed class ListMyRestaurantsServiceTests
{
    private static readonly DateTime CreatedAtUtc =
        new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_WithMultipleMemberships_ReturnsAllRestaurants()
    {
        var userId = Guid.NewGuid();

        var firstRestaurant = CreateRestaurant(
            "First Restaurant",
            userId);

        var secondRestaurant = CreateRestaurant(
            "Second Restaurant",
            userId);

        var repository = new FakeRestaurantRepository(
            firstRestaurant,
            secondRestaurant);

        var service = new ListMyRestaurantsService(repository);

        var result = await service.ExecuteAsync(
            new ListMyRestaurantsQuery(userId),
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.Id == firstRestaurant.Id);
        Assert.Contains(result, item => item.Id == secondRestaurant.Id);

        Assert.All(
            result,
            item => Assert.Equal(RestaurantRole.Owner, item.Role));
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMemberships_ReturnsEmptyCollection()
    {
        var repository = new FakeRestaurantRepository();
        var service = new ListMyRestaurantsService(repository);

        var result = await service.ExecuteAsync(
            new ListMyRestaurantsQuery(Guid.NewGuid()),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyUserId_ThrowsArgumentException()
    {
        var repository = new FakeRestaurantRepository();
        var service = new ListMyRestaurantsService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteAsync(
                new ListMyRestaurantsQuery(Guid.Empty),
                CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_WithNullQuery_ThrowsArgumentNullException()
    {
        var repository = new FakeRestaurantRepository();
        var service = new ListMyRestaurantsService(repository);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ExecuteAsync(
                null!,
                CancellationToken.None));
    }

    private static Restaurant CreateRestaurant(
        string name,
        Guid ownerUserId)
    {
        return Restaurant.Create(
            name,
            Cnpj.Create("11222333000181"),
            RestaurantAddress.Create(
                "Main Street",
                "100",
                null,
                "Downtown",
                "Florianópolis",
                "SC",
                "88010000"),
            ownerUserId,
            CreatedAtUtc);
    }
}
