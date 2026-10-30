
using System.Security.Cryptography;
using System.Text;
using Delivery.Application.Abstractions.Security;
using Delivery.Application.Restaurants.Commands;
using Delivery.Application.Restaurants.Services;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;
using Delivery.Domain.Restaurants.ValueObjects;
using Delivery.UnitTests.Restaurants.Fakes;

namespace Delivery.UnitTests.Restaurants.Services;

public sealed class CreateRestaurantInvitationServiceTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private const string RawToken =
        "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public async Task ExecuteAsync_WithOwner_CreatesInvitationAndSaves()
    {
        var ownerUserId = Guid.NewGuid();
        var restaurant = CreateRestaurant(ownerUserId);
        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();

        var service = CreateService(repository, unitOfWork);

        var result = await service.ExecuteAsync(
            CreateCommand(restaurant.Id),
            ownerUserId);

        var invitation = Assert.Single(restaurant.Invitations);

        Assert.Equal(invitation.Id, result.InvitationId);
        Assert.Equal(RawToken, result.Token);
        Assert.Equal("staff@example.com", invitation.Email);
        Assert.Equal(RestaurantRole.Staff, invitation.Role);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);

        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(RawToken)));

        Assert.Equal(expectedHash, invitation.TokenHash);
        Assert.Equal(FixedTime.UtcDateTime, invitation.CreatedAtUtc);
        Assert.Equal(
            FixedTime.AddDays(7).UtcDateTime,
            result.ExpiresAtUtc);

        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(RestaurantRole.Manager)]
    [InlineData(RestaurantRole.Staff)]
    public async Task ExecuteAsync_WithNonOwner_ThrowsUnauthorizedAccessException(
        RestaurantRole role)
    {
        var ownerUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();

        var restaurant = CreateRestaurant(ownerUserId);

        var membership = restaurant.InviteMember(
            memberUserId,
            role,
            FixedTime.UtcDateTime);

        membership.Activate(FixedTime.UtcDateTime);

        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();

        var service = CreateService(repository, unitOfWork);

        var act = () => service.ExecuteAsync(
            CreateCommand(restaurant.Id),
            memberUserId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(act);

        Assert.Empty(restaurant.Invitations);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonMember_ThrowsInvalidOperationException()
    {
        var ownerUserId = Guid.NewGuid();
        var unknownUserId = Guid.NewGuid();

        var restaurant = CreateRestaurant(ownerUserId);
        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();

        var service = CreateService(repository, unitOfWork);

        var act = () => service.ExecuteAsync(
            CreateCommand(restaurant.Id),
            unknownUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Empty(restaurant.Invitations);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyInviterId_ThrowsArgumentException()
    {
        var restaurant = CreateRestaurant(Guid.NewGuid());
        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();

        var service = CreateService(repository, unitOfWork);

        var act = () => service.ExecuteAsync(
            CreateCommand(restaurant.Id),
            Guid.Empty);

        await Assert.ThrowsAsync<ArgumentException>(act);

        Assert.Empty(restaurant.Invitations);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithOwnerRole_ThrowsArgumentException()
    {
        var ownerUserId = Guid.NewGuid();
        var restaurant = CreateRestaurant(ownerUserId);
        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();

        var service = CreateService(repository, unitOfWork);

        var command = CreateCommand(restaurant.Id) with
        {
            Role = RestaurantRole.Owner
        };

        var act = () => service.ExecuteAsync(command, ownerUserId);

        await Assert.ThrowsAsync<ArgumentException>(act);

        Assert.Empty(restaurant.Invitations);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicatePendingInvitation_ThrowsAndDoesNotSave()
    {
        var ownerUserId = Guid.NewGuid();
        var restaurant = CreateRestaurant(ownerUserId);

        restaurant.InviteByEmail(
            "staff@example.com",
            RestaurantRole.Staff,
            new string('A', 64),
            ownerUserId,
            FixedTime.UtcDateTime,
            FixedTime.AddDays(7).UtcDateTime);

        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();

        var service = CreateService(repository, unitOfWork);

        var act = () => service.ExecuteAsync(
            CreateCommand(restaurant.Id),
            ownerUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Single(restaurant.Invitations);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    private static CreateRestaurantInvitationService CreateService(
        FakeRestaurantRepository repository,
        FakeUnitOfWork unitOfWork)
    {
        return new CreateRestaurantInvitationService(
            repository,
            unitOfWork,
            new FakeInvitationTokenGenerator(),
            new FixedTimeProvider(FixedTime));
    }

    private static Restaurant CreateRestaurant(Guid ownerUserId)
    {
        return Restaurant.Create(
            "Sample Restaurant",
            Cnpj.Create("11.222.333/0001-81"),
            RestaurantAddress.Create(
                "Rua das Flores",
                "123",
                null,
                "Centro",
                "Tubarão",
                "SC",
                "88701-000"),
            ownerUserId,
            FixedTime.UtcDateTime);
    }

    private static CreateRestaurantInvitationCommand CreateCommand(
        Guid restaurantId)
    {
        return new CreateRestaurantInvitationCommand(
            restaurantId,
            "staff@example.com",
            RestaurantRole.Staff);
    }

    private sealed class FakeInvitationTokenGenerator
        : IInvitationTokenGenerator
    {
        public string Generate() => RawToken;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
