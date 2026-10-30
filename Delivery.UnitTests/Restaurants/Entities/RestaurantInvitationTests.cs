using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;

namespace Delivery.UnitTests.Restaurants.Entities;

public sealed class RestaurantInvitationTests
{
    private static readonly Guid RestaurantId =
        Guid.NewGuid();

    private static readonly Guid InvitedByUserId =
        Guid.NewGuid();

    private static readonly Guid RecipientUserId =
        Guid.NewGuid();

    private static readonly DateTime CreatedAtUtc =
        new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime ExpiresAtUtc =
        CreatedAtUtc.AddDays(7);

    private static RestaurantInvitation CreateInvitation(
        string email = "employee@example.com",
        RestaurantRole role = RestaurantRole.Staff)
    {
        return RestaurantInvitation.Create(
            RestaurantId,
            email,
            role,
            "hashed-invitation-token",
            InvitedByUserId,
            CreatedAtUtc,
            ExpiresAtUtc);
    }

    [Fact]
    public void Create_WithValidData_CreatesPendingInvitation()
    {
        var invitation = CreateInvitation(
            "Employee@Example.com");

        Assert.NotEqual(Guid.Empty, invitation.Id);
        Assert.Equal(RestaurantId, invitation.RestaurantId);
        Assert.Equal("employee@example.com", invitation.Email);
        Assert.Equal(RestaurantRole.Staff, invitation.Role);
        Assert.Equal("hashed-invitation-token", invitation.TokenHash);
        Assert.Equal(InvitedByUserId, invitation.InvitedByUserId);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Null(invitation.AcceptedByUserId);
        Assert.Null(invitation.AcceptedAtUtc);
        Assert.Equal(CreatedAtUtc, invitation.CreatedAtUtc);
        Assert.Equal(ExpiresAtUtc, invitation.ExpiresAtUtc);
    }

    [Fact]
    public void Create_WithManagerRole_CreatesInvitation()
    {
        var invitation = CreateInvitation(
            role: RestaurantRole.Manager);

        Assert.Equal(RestaurantRole.Manager, invitation.Role);
    }

    [Fact]
    public void Create_WithEmptyRestaurantId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            RestaurantInvitation.Create(
                Guid.Empty,
                "employee@example.com",
                RestaurantRole.Staff,
                "token-hash",
                InvitedByUserId,
                CreatedAtUtc,
                ExpiresAtUtc));
    }

    [Fact]
    public void Create_WithEmptyInviterId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            RestaurantInvitation.Create(
                RestaurantId,
                "employee@example.com",
                RestaurantRole.Staff,
                "token-hash",
                Guid.Empty,
                CreatedAtUtc,
                ExpiresAtUtc));
    }

    [Fact]
    public void Create_WithOwnerRole_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateInvitation(role: RestaurantRole.Owner));
    }

    [Fact]
    public void Create_WithInvalidRole_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateInvitation(role: (RestaurantRole)99));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-email")]
    [InlineData("employee@")]
    public void Create_WithInvalidEmail_ThrowsArgumentException(
        string email)
    {
        Assert.Throws<ArgumentException>(() =>
            CreateInvitation(email));
    }

    [Fact]
    public void Create_WithEmptyTokenHash_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            RestaurantInvitation.Create(
                RestaurantId,
                "employee@example.com",
                RestaurantRole.Staff,
                "",
                InvitedByUserId,
                CreatedAtUtc,
                ExpiresAtUtc));
    }

    [Fact]
    public void Create_WithExpirationBeforeCreation_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            RestaurantInvitation.Create(
                RestaurantId,
                "employee@example.com",
                RestaurantRole.Staff,
                "token-hash",
                InvitedByUserId,
                CreatedAtUtc,
                CreatedAtUtc));
    }

    [Fact]
    public void Accept_WithMatchingEmail_ActivatesInvitation()
    {
        var invitation = CreateInvitation();

        var acceptedAtUtc = CreatedAtUtc.AddHours(1);

        invitation.Accept(
            RecipientUserId,
            "EMPLOYEE@example.com",
            acceptedAtUtc);

        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal(RecipientUserId, invitation.AcceptedByUserId);
        Assert.Equal(acceptedAtUtc, invitation.AcceptedAtUtc);
    }

    [Fact]
    public void Accept_WithDifferentEmail_ThrowsInvalidOperationException()
    {
        var invitation = CreateInvitation();

        Assert.Throws<InvalidOperationException>(() =>
            invitation.Accept(
                RecipientUserId,
                "another@example.com",
                CreatedAtUtc.AddHours(1)));

        Assert.Equal(InvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public void Accept_WithEmptyUserId_ThrowsArgumentException()
    {
        var invitation = CreateInvitation();

        Assert.Throws<ArgumentException>(() =>
            invitation.Accept(
                Guid.Empty,
                "employee@example.com",
                CreatedAtUtc.AddHours(1)));
    }

    [Fact]
    public void Accept_AfterExpiration_ThrowsInvalidOperationException()
    {
        var invitation = CreateInvitation();

        Assert.Throws<InvalidOperationException>(() =>
            invitation.Accept(
                RecipientUserId,
                "employee@example.com",
                ExpiresAtUtc));

        Assert.Equal(InvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public void Accept_BeforeCreation_ThrowsArgumentException()
    {
        var invitation = CreateInvitation();

        Assert.Throws<ArgumentException>(() =>
            invitation.Accept(
                RecipientUserId,
                "employee@example.com",
                CreatedAtUtc.AddSeconds(-1)));
    }

    [Fact]
    public void Accept_AlreadyAcceptedInvitation_ThrowsInvalidOperationException()
    {
        var invitation = CreateInvitation();

        invitation.Accept(
            RecipientUserId,
            "employee@example.com",
            CreatedAtUtc.AddHours(1));

        Assert.Throws<InvalidOperationException>(() =>
            invitation.Accept(
                RecipientUserId,
                "employee@example.com",
                CreatedAtUtc.AddHours(2)));
    }

    [Fact]
    public void Revoke_PendingInvitation_ChangesStatusToRevoked()
    {
        var invitation = CreateInvitation();

        var revokedAtUtc = CreatedAtUtc.AddHours(1);

        invitation.Revoke(revokedAtUtc);

        Assert.Equal(InvitationStatus.Revoked, invitation.Status);
        Assert.Equal(revokedAtUtc, invitation.RevokedAtUtc);
    }

    [Fact]
    public void Revoke_AlreadyRevokedInvitation_ThrowsInvalidOperationException()
    {
        var invitation = CreateInvitation();

        invitation.Revoke(CreatedAtUtc.AddHours(1));

        Assert.Throws<InvalidOperationException>(() =>
            invitation.Revoke(CreatedAtUtc.AddHours(2)));
    }

    [Fact]
    public void Revoke_BeforeCreation_ThrowsArgumentException()
    {
        var invitation = CreateInvitation();

        Assert.Throws<ArgumentException>(() =>
            invitation.Revoke(CreatedAtUtc.AddSeconds(-1)));
    }

    [Fact]
    public void Expire_WhenExpirationTimeReached_ChangesStatusToExpired()
    {
        var invitation = CreateInvitation();

        invitation.Expire(ExpiresAtUtc);

        Assert.Equal(InvitationStatus.Expired, invitation.Status);
    }

    [Fact]
    public void Expire_BeforeExpirationTime_ThrowsInvalidOperationException()
    {
        var invitation = CreateInvitation();

        Assert.Throws<InvalidOperationException>(() =>
            invitation.Expire(ExpiresAtUtc.AddSeconds(-1)));

        Assert.Equal(InvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public void Expire_AlreadyExpiredInvitation_ThrowsInvalidOperationException()
    {
        var invitation = CreateInvitation();

        invitation.Expire(ExpiresAtUtc);

        Assert.Throws<InvalidOperationException>(() =>
            invitation.Expire(ExpiresAtUtc.AddMinutes(1)));
    }

    [Fact]
    public void Accept_RevokedInvitation_ThrowsInvalidOperationException()
    {
        var invitation = CreateInvitation();

        invitation.Revoke(CreatedAtUtc.AddHours(1));

        Assert.Throws<InvalidOperationException>(() =>
            invitation.Accept(
                RecipientUserId,
                "employee@example.com",
                CreatedAtUtc.AddHours(2)));
    }
}
