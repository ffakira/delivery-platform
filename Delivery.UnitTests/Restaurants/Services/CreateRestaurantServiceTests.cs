
using Delivery.Application.Restaurants.Commands;
using Delivery.Application.Restaurants.Repositories;
using Delivery.Application.Restaurants.Services;
using Delivery.Domain.Restaurants.Entities;
using Delivery.UnitTests.Restaurants.Fakes;

namespace Delivery.UnitTests.Restaurants.Services;

public sealed class CreateRestaurantServiceTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_WithValidData_CreatesRestaurantWithOwner()
    {
        var repository = new FakeRestaurantRepository();
        var service = CreateService(repository);
        var ownerUserId = Guid.NewGuid();

        var restaurantId = await service.ExecuteAsync(
            CreateValidCommand(),
            ownerUserId);

        var restaurant = Assert.Single(repository.Restaurants);

        Assert.Equal(restaurantId, restaurant.Id);
        Assert.Equal("Sample Restaurant", restaurant.Name);
        Assert.Equal("11222333000181", restaurant.Cnpj.Value);
        Assert.Equal(FixedTime.UtcDateTime, restaurant.CreatedAtUtc);

        var membership = Assert.Single(restaurant.Memberships);

        Assert.Equal(ownerUserId, membership.UserId);
        Assert.Equal(Delivery.Domain.Restaurants.Enums.RestaurantRole.Owner, membership.Role);
        Assert.Equal(Delivery.Domain.Restaurants.Enums.MembershipStatus.Active, membership.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCnpj_ThrowsArgumentException()
    {
        var repository = new FakeRestaurantRepository();
        var service = CreateService(repository);

        var command = CreateValidCommand() with
        {
            Cnpj = "00000000000000"
        };

        var act = () => service.ExecuteAsync(command, Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(act);

        Assert.Empty(repository.Restaurants);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidAddress_ThrowsArgumentException()
    {
        var repository = new FakeRestaurantRepository();
        var service = CreateService(repository);

        var command = CreateValidCommand() with
        {
            PostalCode = "123"
        };

        var act = () => service.ExecuteAsync(command, Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(act);

        Assert.Empty(repository.Restaurants);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyOwnerId_ThrowsArgumentException()
    {
        var repository = new FakeRestaurantRepository();
        var service = CreateService(repository);

        var act = () => service.ExecuteAsync(
            CreateValidCommand(),
            Guid.Empty);

        await Assert.ThrowsAsync<ArgumentException>(act);

        Assert.Empty(repository.Restaurants);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullCommand_ThrowsArgumentNullException()
    {
        var repository = new FakeRestaurantRepository();
        var service = CreateService(repository);

        var act = () => service.ExecuteAsync(null!, Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentNullException>(act);

        Assert.Empty(repository.Restaurants);
    }

    private static CreateRestaurantService CreateService(
        IRestaurantRepository repository)
    {
        return new CreateRestaurantService(
            repository,
            new FakeUnitOfWork(),
            new FixedTimeProvider(FixedTime));
    }

    private static CreateRestaurantCommand CreateValidCommand()
    {
        return new CreateRestaurantCommand(
            Name: "  Sample Restaurant  ",
            Cnpj: "11.222.333/0001-81",
            Street: "Rua das Flores",
            Number: "123",
            Complement: "Sala 2",
            Neighborhood: "Centro",
            City: "Tubarão",
            State: "sc",
            PostalCode: "88701-000");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
