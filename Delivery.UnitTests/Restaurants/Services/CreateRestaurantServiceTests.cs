using Delivery.Application.Restaurants.Commands;
using Delivery.Application.Restaurants.Repositories;
using Delivery.Application.Restaurants.Services;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;

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

        var restaurant = Assert.IsType<Restaurant>(repository.Restaurant);

        Assert.Equal(restaurantId, restaurant.Id);
        Assert.Equal("Akira Japanese Restaurant", restaurant.Name);
        Assert.Equal("11222333000181", restaurant.Cnpj.Value);
        Assert.Equal(FixedTime.UtcDateTime, restaurant.CreatedAtUtc);

        var membership = Assert.Single(restaurant.Memberships);

        Assert.Equal(ownerUserId, membership.UserId);
        Assert.Equal(restaurant.Id, membership.RestaurantId);
        Assert.Equal(RestaurantRole.Owner, membership.Role);
        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal(FixedTime.UtcDateTime, membership.ActivatedAtUtc);

        Assert.True(repository.AddWasCalled);
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

        Assert.Null(repository.Restaurant);
        Assert.False(repository.AddWasCalled);
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

        Assert.Null(repository.Restaurant);
        Assert.False(repository.AddWasCalled);
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

        Assert.Null(repository.Restaurant);
        Assert.False(repository.AddWasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullCommand_ThrowsArgumentNullException()
    {
        var repository = new FakeRestaurantRepository();
        var service = CreateService(repository);

        var act = () => service.ExecuteAsync(null!, Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentNullException>(act);

        Assert.Null(repository.Restaurant);
        Assert.False(repository.AddWasCalled);
    }

    private static CreateRestaurantService CreateService(
        IRestaurantRepository repository)
    {
        return new CreateRestaurantService(
            repository,
            new FixedTimeProvider(FixedTime));
    }

    private static CreateRestaurantCommand CreateValidCommand()
    {
        return new CreateRestaurantCommand(
            Name: "  Akira Japanese Restaurant  ",
            Cnpj: "11.222.333/0001-81",
            Street: "Rua das Flores",
            Number: "123",
            Complement: "Sala 2",
            Neighborhood: "Centro",
            City: "Tubarão",
            State: "sc",
            PostalCode: "88701-000");
    }

    private sealed class FakeRestaurantRepository : IRestaurantRepository
    {
        public Restaurant? Restaurant { get; private set; }

        public bool AddWasCalled { get; private set; }

        public Task AddAsync(
            Restaurant restaurant,
            CancellationToken cancellationToken = default)
        {
            AddWasCalled = true;
            Restaurant = restaurant;

            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
