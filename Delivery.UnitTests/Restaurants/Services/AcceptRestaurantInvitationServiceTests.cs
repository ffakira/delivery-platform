using System.Security.Cryptography;
using System.Text;
using Delivery.Application.Restaurants.Commands;
using Delivery.Application.Restaurants.Services;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;
using Delivery.Domain.Restaurants.ValueObjects;
using Delivery.UnitTests.Restaurants.Fakes;

namespace Delivery.UnitTests.Restaurants.Services;

public sealed class AcceptRestaurantInvitationServiceTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private const string RawToken =
        "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    private const string InviteeEmail = "staff@example.com";

    [Fact]
    public async Task ExecuteAsync_WithValidInvitation_AcceptsAndSaves()
    {
        var ownerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();

        var restaurant = CreateRestaurant(ownerUserId);
        CreateInvitation(restaurant, ownerUserId);

        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var result = await service.ExecuteAsync(
            CreateCommand(),
            inviteeUserId,
            InviteeEmail);

        var invitation = Assert.Single(restaurant.Invitations);
        var membership = restaurant.Memberships.Single(
            item => item.UserId == inviteeUserId);

        Assert.Equal(restaurant.Id, result.RestaurantId);
        Assert.Equal(membership.Id, result.MembershipId);
        Assert.Equal(RestaurantRole.Staff, result.Role);

        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal(inviteeUserId, invitation.AcceptedByUserId);
        Assert.Equal(FixedTime.UtcDateTime, invitation.AcceptedAtUtc);

        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal(RestaurantRole.Staff, membership.Role);
        Assert.Equal(FixedTime.UtcDateTime, membership.CreatedAtUtc);
        Assert.Equal(FixedTime.UtcDateTime, membership.ActivatedAtUtc);

        Assert.Collection(
            restaurant.Memberships,
            membership => Assert.Equal(RestaurantRole.Owner, membership.Role),
            membership => Assert.Equal(RestaurantRole.Staff, membership.Role));
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInvitation_UsesHashedTokenForLookup()
    {
        var ownerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();

        var restaurant = CreateRestaurant(ownerUserId);
        var invitation = CreateInvitation(restaurant, ownerUserId);

        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(RawToken)));

        Assert.Equal(expectedHash, invitation.TokenHash);
        Assert.NotEqual(RawToken, invitation.TokenHash);

        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        await service.ExecuteAsync(
            CreateCommand(),
            inviteeUserId,
            InviteeEmail);

        Assert.Equal(expectedHash, invitation.TokenHash);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownToken_ThrowsAndDoesNotSave()
    {
        var restaurant = CreateRestaurant(Guid.NewGuid());
        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var command = new AcceptRestaurantInvitationCommand(
            "unknown-token");

        var act = () => service.ExecuteAsync(
            command,
            Guid.NewGuid(),
            InviteeEmail);

        await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Single(restaurant.Memberships);
        Assert.Empty(restaurant.Invitations);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithMismatchedEmail_ThrowsAndDoesNotSave()
    {
        var ownerUserId = Guid.NewGuid();
        var restaurant = CreateRestaurant(ownerUserId);
        var invitation = CreateInvitation(restaurant, ownerUserId);

        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var act = () => service.ExecuteAsync(
            CreateCommand(),
            Guid.NewGuid(),
            "another@example.com");

        await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Single(restaurant.Memberships);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithExpiredInvitation_ThrowsAndDoesNotSave()
    {
        var ownerUserId = Guid.NewGuid();
        var restaurant = CreateRestaurant(ownerUserId);

        var invitation = restaurant.InviteByEmail(
            InviteeEmail,
            RestaurantRole.Staff,
            HashToken(RawToken),
            ownerUserId,
            FixedTime.AddDays(-8).UtcDateTime,
            FixedTime.AddDays(-1).UtcDateTime);

        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var act = () => service.ExecuteAsync(
            CreateCommand(),
            Guid.NewGuid(),
            InviteeEmail);

        await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Single(restaurant.Memberships);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithRevokedInvitation_ThrowsAndDoesNotSave()
    {
        var ownerUserId = Guid.NewGuid();
        var restaurant = CreateRestaurant(ownerUserId);
        var invitation = CreateInvitation(restaurant, ownerUserId);

        invitation.Revoke(FixedTime.AddMinutes(-1).UtcDateTime);

        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var act = () => service.ExecuteAsync(
            CreateCommand(),
            Guid.NewGuid(),
            InviteeEmail);

        await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Equal(InvitationStatus.Revoked, invitation.Status);
        Assert.Single(restaurant.Memberships);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithAlreadyAcceptedInvitation_ThrowsAndDoesNotSave()
    {
        var ownerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();

        var restaurant = CreateRestaurant(ownerUserId);
        var invitation = CreateInvitation(restaurant, ownerUserId);

        restaurant.AcceptInvitation(
            HashToken(RawToken),
            inviteeUserId,
            InviteeEmail,
            FixedTime.UtcDateTime);

        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var act = () => service.ExecuteAsync(
            CreateCommand(),
            inviteeUserId,
            InviteeEmail);

        await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Collection(
            restaurant.Memberships,
            membership => Assert.Equal(RestaurantRole.Owner, membership.Role),
            membership => Assert.Equal(RestaurantRole.Staff, membership.Role));
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Theory]
    [InlineData(MembershipStatus.Invited)]
    [InlineData(MembershipStatus.Active)]
    [InlineData(MembershipStatus.Suspended)]
    public async Task ExecuteAsync_WithExistingMembership_ThrowsAndDoesNotSave(
        MembershipStatus status)
    {
        var ownerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();

        var restaurant = CreateRestaurant(ownerUserId);
        var invitation = CreateInvitation(restaurant, ownerUserId);

        var existingMembership = restaurant.InviteMember(
            inviteeUserId,
            RestaurantRole.Staff,
            FixedTime.AddDays(-1).UtcDateTime);

        if (status is MembershipStatus.Active or MembershipStatus.Suspended)
        {
            existingMembership.Activate(
                FixedTime.AddHours(-12).UtcDateTime);
        }

        if (status == MembershipStatus.Suspended)
        {
            existingMembership.Suspend();
        }

        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var act = () => service.ExecuteAsync(
            CreateCommand(),
            inviteeUserId,
            InviteeEmail);

        await Assert.ThrowsAsync<InvalidOperationException>(act);

        Assert.Equal(status, existingMembership.Status);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Collection(
            restaurant.Memberships,
            membership => Assert.Equal(RestaurantRole.Owner, membership.Role),
            membership => Assert.Equal(RestaurantRole.Staff, membership.Role));
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyUserId_ThrowsArgumentException()
    {
        var restaurant = CreateRestaurant(Guid.NewGuid());
        CreateInvitation(restaurant, restaurant.Memberships.Single().UserId);

        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var act = () => service.ExecuteAsync(
            CreateCommand(),
            Guid.Empty,
            InviteeEmail);

        await Assert.ThrowsAsync<ArgumentException>(act);

        Assert.Equal(InvitationStatus.Pending, restaurant.Invitations.Single().Status);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExecuteAsync_WithEmptyToken_ThrowsArgumentException(
        string token)
    {
        var restaurant = CreateRestaurant(Guid.NewGuid());
        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var command = new AcceptRestaurantInvitationCommand(token);

        var act = () => service.ExecuteAsync(
            command,
            Guid.NewGuid(),
            InviteeEmail);

        await Assert.ThrowsAsync<ArgumentException>(act);

        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExecuteAsync_WithEmptyVerifiedEmail_ThrowsArgumentException(
        string email)
    {
        var restaurant = CreateRestaurant(Guid.NewGuid());
        var repository = new FakeRestaurantRepository(restaurant);
        var unitOfWork = new FakeUnitOfWork();
        var service = CreateService(repository, unitOfWork);

        var act = () => service.ExecuteAsync(
            CreateCommand(),
            Guid.NewGuid(),
            email);

        await Assert.ThrowsAsync<ArgumentException>(act);

        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    private static AcceptRestaurantInvitationService CreateService(
        FakeRestaurantRepository repository,
        FakeUnitOfWork unitOfWork)
    {
        return new AcceptRestaurantInvitationService(
            repository,
            unitOfWork,
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

    private static RestaurantInvitation CreateInvitation(
        Restaurant restaurant,
        Guid ownerUserId)
    {
        return restaurant.InviteByEmail(
            InviteeEmail,
            RestaurantRole.Staff,
            HashToken(RawToken),
            ownerUserId,
            FixedTime.AddDays(-1).UtcDateTime,
            FixedTime.AddDays(7).UtcDateTime);
    }

    private static AcceptRestaurantInvitationCommand CreateCommand()
    {
        return new AcceptRestaurantInvitationCommand(RawToken);
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
