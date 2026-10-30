using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;
using Delivery.Domain.Restaurants.ValueObjects;

namespace Delivery.UnitTests.Restaurants.Entities;

public sealed class RestaurantMembershipTests
{
    private static readonly Guid RestaurantId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid UserId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly DateTime CreatedAtUtc =
        new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Invite_WithValidData_CreatesInvitedMembership()
    {
        var membership = CreateMembership();

        Assert.NotEqual(Guid.Empty, membership.Id);
        Assert.Equal(RestaurantId, membership.RestaurantId);
        Assert.Equal(UserId, membership.UserId);
        Assert.Equal(RestaurantRole.Staff, membership.Role);
        Assert.Equal(MembershipStatus.Invited, membership.Status);
        Assert.Equal(CreatedAtUtc, membership.CreatedAtUtc);
        Assert.Null(membership.ActivatedAtUtc);
    }

    [Fact]
    public void Invite_WithEmptyRestaurantId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            RestaurantMembership.Invite(
                Guid.Empty,
                UserId,
                RestaurantRole.Staff,
                CreatedAtUtc));
    }

    [Fact]
    public void Invite_WithEmptyUserId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            RestaurantMembership.Invite(
                RestaurantId,
                Guid.Empty,
                RestaurantRole.Staff,
                CreatedAtUtc));
    }

    [Fact]
    public void Invite_WithOwnerRole_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            RestaurantMembership.Invite(
                RestaurantId,
                UserId,
                RestaurantRole.Owner,
                CreatedAtUtc));
    }

    [Fact]
    public void Invite_WithUndefinedRole_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RestaurantMembership.Invite(
                RestaurantId,
                UserId,
                (RestaurantRole)999,
                CreatedAtUtc));
    }

    [Fact]
    public void Activate_WithInvitedMembership_ChangesStatusToActive()
    {
        var membership = CreateMembership();
        var activatedAtUtc = CreatedAtUtc.AddMinutes(5);

        membership.Activate(activatedAtUtc);

        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal(activatedAtUtc, membership.ActivatedAtUtc);
    }

    [Fact]
    public void Activate_WithActiveMembership_ThrowsInvalidOperationException()
    {
        var membership = CreateActiveMembership();

        Assert.Throws<InvalidOperationException>(() =>
            membership.Activate(CreatedAtUtc.AddMinutes(10)));
    }

    [Fact]
    public void Suspend_WithActiveMembership_ChangesStatusToSuspended()
    {
        var membership = CreateActiveMembership();

        membership.Suspend();

        Assert.Equal(MembershipStatus.Suspended, membership.Status);
    }

    [Fact]
    public void Suspend_WithInvitedMembership_ThrowsInvalidOperationException()
    {
        var membership = CreateMembership();

        Assert.Throws<InvalidOperationException>(() =>
            membership.Suspend());
    }

    [Fact]
    public void Suspend_WithSuspendedMembership_ThrowsInvalidOperationException()
    {
        var membership = CreateActiveMembership();
        membership.Suspend();

        Assert.Throws<InvalidOperationException>(() =>
            membership.Suspend());
    }

    [Fact]
    public void Suspend_WithOwnerMembership_ThrowsInvalidOperationException()
    {
        var membership = CreateOwnerMembership();

        Assert.Throws<InvalidOperationException>(() =>
            membership.Suspend());
    }

    [Fact]
    public void ChangeRole_WithActiveMembership_ChangesRole()
    {
        var membership = CreateActiveMembership();

        membership.ChangeRole(RestaurantRole.Manager);

        Assert.Equal(RestaurantRole.Manager, membership.Role);
    }

    [Fact]
    public void ChangeRole_WithInvitedMembership_ThrowsInvalidOperationException()
    {
        var membership = CreateMembership();

        Assert.Throws<InvalidOperationException>(() =>
            membership.ChangeRole(RestaurantRole.Manager));
    }

    [Fact]
    public void ChangeRole_WithSuspendedMembership_ThrowsInvalidOperationException()
    {
        var membership = CreateActiveMembership();
        membership.Suspend();

        Assert.Throws<InvalidOperationException>(() =>
            membership.ChangeRole(RestaurantRole.Manager));
    }

    [Fact]
    public void ChangeRole_WithOwnerRole_ThrowsArgumentOutOfRangeException()
    {
        var membership = CreateActiveMembership();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            membership.ChangeRole(RestaurantRole.Owner));
    }

    [Fact]
    public void ChangeRole_WithUndefinedRole_ThrowsArgumentOutOfRangeException()
    {
        var membership = CreateActiveMembership();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            membership.ChangeRole((RestaurantRole)999));
    }

    [Fact]
    public void ChangeRole_WithOwnerMembership_ThrowsInvalidOperationException()
    {
        var membership = CreateOwnerMembership();

        Assert.Throws<InvalidOperationException>(() =>
            membership.ChangeRole(RestaurantRole.Manager));
    }

    private static RestaurantMembership CreateMembership()
    {
        return RestaurantMembership.Invite(
            RestaurantId,
            UserId,
            RestaurantRole.Staff,
            CreatedAtUtc);
    }

    private static RestaurantMembership CreateActiveMembership()
    {
        var membership = CreateMembership();

        membership.Activate(CreatedAtUtc.AddMinutes(5));

        return membership;
    }

    private static RestaurantMembership CreateOwnerMembership()
    {
        var restaurant = Restaurant.Create(
            "Delivery Restaurant",
            Cnpj.Create("11222333000181"),
            RestaurantAddress.Create(
                "Avenida Beira-Mar Norte",
                "1000",
                null,
                "Centro",
                "Florianópolis",
                "SC",
                "88015-700"),
            UserId,
            CreatedAtUtc);

        return Assert.Single(restaurant.Memberships);
    }
}
