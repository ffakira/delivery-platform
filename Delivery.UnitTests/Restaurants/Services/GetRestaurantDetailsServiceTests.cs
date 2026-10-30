using Delivery.Application.Restaurants.Queries;
using Delivery.Application.Restaurants.Services;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;
using Delivery.Domain.Restaurants.ValueObjects;
using Delivery.UnitTests.Restaurants.Fakes;

namespace Delivery.UnitTests.Restaurants.Services;

public sealed class GetRestaurantDetailsServiceTests
{
    private static readonly DateTime CreatedAtUtc =
        new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExecuteAsync_WithActiveOwnerMembership_ReturnsRestaurantDetails()
    {
        var ownerUserId = Guid.NewGuid();
        var restaurant = CreateRestaurant(ownerUserId);
        var repository = new FakeRestaurantRepository(restaurant);
        var service = new GetRestaurantDetailsService(repository);

        var result = await service.ExecuteAsync(
            new GetRestaurantDetailsQuery(restaurant.Id, ownerUserId),
            CancellationToken.None);

        Assert.NotNull(result);

        Assert.Equal(restaurant.Id, result.Id);
        Assert.Equal("Sample Restaurant", result.Name);
        Assert.Equal("11222333000181", result.Cnpj);
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
    public async Task ExecuteAsync_WithActiveManagerMembership_ReturnsRestaurantDetails()
    {
        var ownerUserId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();
        var restaurant = CreateRestaurant(ownerUserId);

        var membership = restaurant.InviteMember(
            managerUserId,
            RestaurantRole.Manager,
            CreatedAtUtc);

        membership.Activate(CreatedAtUtc);

        var repository = new FakeRestaurantRepository(restaurant);
        var service = new GetRestaurantDetailsService(repository);

        var result = await service.ExecuteAsync(
            new GetRestaurantDetailsQuery(restaurant.Id, managerUserId),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(restaurant.Id, result.Id);
        Assert.Equal(RestaurantRole.Manager, result.Role);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvitedMembership_ReturnsNull()
    {
        var ownerUserId = Guid.NewGuid();
        var invitedUserId = Guid.NewGuid();
        var restaurant = CreateRestaurant(ownerUserId);

        restaurant.InviteMember(
            invitedUserId,
            RestaurantRole.Staff,
            CreatedAtUtc);

        var repository = new FakeRestaurantRepository(restaurant);
        var service = new GetRestaurantDetailsService(repository);

        var result = await service.ExecuteAsync(
            new GetRestaurantDetailsQuery(restaurant.Id, invitedUserId),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithSuspendedMembership_ReturnsNull()
    {
        var ownerUserId = Guid.NewGuid();
        var staffUserId = Guid.NewGuid();
        var restaurant = CreateRestaurant(ownerUserId);

        var membership = restaurant.InviteMember(
            staffUserId,
            RestaurantRole.Staff,
            CreatedAtUtc);

        membership.Activate(CreatedAtUtc);
        membership.Suspend();

        var repository = new FakeRestaurantRepository(restaurant);
        var service = new GetRestaurantDetailsService(repository);

        var result = await service.ExecuteAsync(
            new GetRestaurantDetailsQuery(restaurant.Id, staffUserId),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithUserWithoutMembership_ReturnsNull()
    {
        var restaurant = CreateRestaurant(Guid.NewGuid());
        var repository = new FakeRestaurantRepository(restaurant);
        var service = new GetRestaurantDetailsService(repository);

        var result = await service.ExecuteAsync(
            new GetRestaurantDetailsQuery(restaurant.Id, Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownRestaurantId_ReturnsNull()
    {
        var repository = new FakeRestaurantRepository();
        var service = new GetRestaurantDetailsService(repository);

        var result = await service.ExecuteAsync(
            new GetRestaurantDetailsQuery(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyRestaurantId_ThrowsArgumentException()
    {
        var repository = new FakeRestaurantRepository();
        var service = new GetRestaurantDetailsService(repository);

        var act = () => service.ExecuteAsync(
            new GetRestaurantDetailsQuery(Guid.Empty, Guid.NewGuid()),
            CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyUserId_ThrowsArgumentException()
    {
        var repository = new FakeRestaurantRepository();
        var service = new GetRestaurantDetailsService(repository);

        var act = () => service.ExecuteAsync(
            new GetRestaurantDetailsQuery(Guid.NewGuid(), Guid.Empty),
            CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(act);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullQuery_ThrowsArgumentNullException()
    {
        var repository = new FakeRestaurantRepository();
        var service = new GetRestaurantDetailsService(repository);

        var act = () => service.ExecuteAsync(
            null!,
            CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentNullException>(act);
    }

    private static Restaurant CreateRestaurant(Guid ownerUserId)
    {
        return Restaurant.Create(
            "Sample Restaurant",
            Cnpj.Create("11222333000181"),
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
    }
}
