using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;
using Delivery.Domain.Restaurants.ValueObjects;

namespace Delivery.UnitTests.Restaurants.Entities;

public sealed class RestaurantTests
{
    private static readonly Guid OwnerUserId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid ManagerUserId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly Guid StaffUserId =
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    
    private static readonly Guid RecipientUserId =
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private const string InvitationTokenHash = "accepted-invitation-token-hash";

    private static readonly DateTime CreatedAtUtc =
        new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime InvitationExpiresAtUtc =
        CreatedAtUtc.AddDays(7);

    [Fact]
    public void Create_WithValidData_CreatesRestaurant()
    {
        var restaurant = Restaurant.Create(
            name: "  Delivery Restaurant  ",
            cnpj: CreateCnpj(),
            address: CreateAddress(),
            ownerUserId: OwnerUserId,
            createdAtUtc: CreatedAtUtc);

        Assert.NotEqual(Guid.Empty, restaurant.Id);
        Assert.Equal("Delivery Restaurant", restaurant.Name);
        Assert.Equal(CreateCnpj(), restaurant.Cnpj);
        Assert.Equal(CreateAddress(), restaurant.Address);
        Assert.Equal(CreatedAtUtc, restaurant.CreatedAtUtc);
    }

    [Fact]
    public void Create_WithValidData_CreatesActiveOwnerMembership()
    {
        var restaurant = CreateRestaurant();

        var ownerMembership = Assert.Single(restaurant.Memberships);

        Assert.NotEqual(Guid.Empty, ownerMembership.Id);
        Assert.Equal(restaurant.Id, ownerMembership.RestaurantId);
        Assert.Equal(OwnerUserId, ownerMembership.UserId);
        Assert.Equal(RestaurantRole.Owner, ownerMembership.Role);
        Assert.Equal(MembershipStatus.Active, ownerMembership.Status);
        Assert.Equal(CreatedAtUtc, ownerMembership.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, ownerMembership.ActivatedAtUtc);
    }

    [Fact]
    public void Create_WithValidData_StartsWithEmptyInvitationCollection()
    {
        var restaurant = CreateRestaurant();

        Assert.Empty(restaurant.Invitations);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ThrowsArgumentException(string name)
    {
        Assert.Throws<ArgumentException>(() =>
            Restaurant.Create(
                name,
                CreateCnpj(),
                CreateAddress(),
                OwnerUserId,
                CreatedAtUtc));
    }

    [Fact]
    public void Create_WithNullCnpj_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Restaurant.Create(
                "Delivery Restaurant",
                null!,
                CreateAddress(),
                OwnerUserId,
                CreatedAtUtc));
    }

    [Fact]
    public void Create_WithNullAddress_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Restaurant.Create(
                "Delivery Restaurant",
                CreateCnpj(),
                null!,
                OwnerUserId,
                CreatedAtUtc));
    }

    [Fact]
    public void Create_WithEmptyOwnerUserId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Restaurant.Create(
                "Delivery Restaurant",
                CreateCnpj(),
                CreateAddress(),
                Guid.Empty,
                CreatedAtUtc));
    }

    [Fact]
    public void Rename_WithValidName_UpdatesRestaurantName()
    {
        var restaurant = CreateRestaurant();

        restaurant.Rename("  New Restaurant Name  ");

        Assert.Equal("New Restaurant Name", restaurant.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_WithInvalidName_ThrowsArgumentException(string name)
    {
        var restaurant = CreateRestaurant();

        Assert.Throws<ArgumentException>(() =>
            restaurant.Rename(name));
    }

    [Fact]
    public void ChangeAddress_WithValidAddress_UpdatesRestaurantAddress()
    {
        var restaurant = CreateRestaurant();

        var newAddress = RestaurantAddress.Create(
            "Avenida Paulista",
            "1000",
            "Sala 5",
            "Bela Vista",
            "São Paulo",
            "SP",
            "01310-100");

        restaurant.ChangeAddress(newAddress);

        Assert.Equal(newAddress, restaurant.Address);
    }

    [Fact]
    public void ChangeAddress_WithNullAddress_ThrowsArgumentNullException()
    {
        var restaurant = CreateRestaurant();

        Assert.Throws<ArgumentNullException>(() =>
            restaurant.ChangeAddress(null!));
    }

    [Fact]
    public void InviteMember_WithManagerRole_AddsInvitedMembership()
    {
        var restaurant = CreateRestaurant();
        var invitedAtUtc = CreatedAtUtc.AddMinutes(10);

        var membership = restaurant.InviteMember(
            ManagerUserId,
            RestaurantRole.Manager,
            invitedAtUtc);

        Assert.Equal(restaurant.Id, membership.RestaurantId);
        Assert.Equal(ManagerUserId, membership.UserId);
        Assert.Equal(RestaurantRole.Manager, membership.Role);
        Assert.Equal(MembershipStatus.Invited, membership.Status);
        Assert.Equal(invitedAtUtc, membership.CreatedAtUtc);
        Assert.Contains(membership, restaurant.Memberships);
        Assert.Equal(2, restaurant.Memberships.Count);
    }

    [Fact]
    public void InviteMember_WithStaffRole_AddsInvitedMembership()
    {
        var restaurant = CreateRestaurant();

        var membership = restaurant.InviteMember(
            StaffUserId,
            RestaurantRole.Staff,
            CreatedAtUtc.AddMinutes(10));

        Assert.Equal(RestaurantRole.Staff, membership.Role);
        Assert.Equal(MembershipStatus.Invited, membership.Status);
        Assert.Equal(2, restaurant.Memberships.Count);
    }

    [Fact]
    public void InviteMember_WithExistingUser_ThrowsInvalidOperationException()
    {
        var restaurant = CreateRestaurant();

        Assert.Throws<InvalidOperationException>(() =>
            restaurant.InviteMember(
                OwnerUserId,
                RestaurantRole.Manager,
                CreatedAtUtc.AddMinutes(10)));
    }

    [Fact]
    public void InviteMember_WithEmptyUserId_ThrowsArgumentException()
    {
        var restaurant = CreateRestaurant();

        Assert.Throws<ArgumentException>(() =>
            restaurant.InviteMember(
                Guid.Empty,
                RestaurantRole.Staff,
                CreatedAtUtc.AddMinutes(10)));
    }

    [Fact]
    public void InviteMember_WithOwnerRole_ThrowsArgumentException()
    {
        var restaurant = CreateRestaurant();

        Assert.Throws<ArgumentException>(() =>
            restaurant.InviteMember(
                ManagerUserId,
                RestaurantRole.Owner,
                CreatedAtUtc.AddMinutes(10)));
    }

    [Fact]
    public void InviteMember_WithUndefinedRole_ThrowsArgumentOutOfRangeException()
    {
        var restaurant = CreateRestaurant();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            restaurant.InviteMember(
                ManagerUserId,
                (RestaurantRole)999,
                CreatedAtUtc.AddMinutes(10)));
    }

    [Fact]
    public void InviteByEmail_WithManagerRole_AddsPendingInvitation()
    {
        var restaurant = CreateRestaurant();
        var invitedAtUtc = CreatedAtUtc.AddMinutes(10);

        var invitation = restaurant.InviteByEmail(
            "manager@example.com",
            RestaurantRole.Manager,
            "manager-token-hash",
            OwnerUserId,
            invitedAtUtc,
            InvitationExpiresAtUtc);

        Assert.NotEqual(Guid.Empty, invitation.Id);
        Assert.Equal(restaurant.Id, invitation.RestaurantId);
        Assert.Equal("manager@example.com", invitation.Email);
        Assert.Equal(RestaurantRole.Manager, invitation.Role);
        Assert.Equal("manager-token-hash", invitation.TokenHash);
        Assert.Equal(OwnerUserId, invitation.InvitedByUserId);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Equal(invitedAtUtc, invitation.CreatedAtUtc);
        Assert.Equal(InvitationExpiresAtUtc, invitation.ExpiresAtUtc);
        Assert.Contains(invitation, restaurant.Invitations);
        Assert.Single(restaurant.Invitations);
    }

    [Fact]
    public void InviteByEmail_WithStaffRole_AddsPendingInvitation()
    {
        var restaurant = CreateRestaurant();

        var invitation = restaurant.InviteByEmail(
            "staff@example.com",
            RestaurantRole.Staff,
            "staff-token-hash",
            OwnerUserId,
            CreatedAtUtc,
            InvitationExpiresAtUtc);

        Assert.Equal(RestaurantRole.Staff, invitation.Role);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Single(restaurant.Invitations);
    }

    [Fact]
    public void InviteByEmail_WithDifferentEmail_AddsSeparateInvitations()
    {
        var restaurant = CreateRestaurant();

        var firstInvitation = restaurant.InviteByEmail(
            "manager@example.com",
            RestaurantRole.Manager,
            "first-token-hash",
            OwnerUserId,
            CreatedAtUtc,
            InvitationExpiresAtUtc);

        var secondInvitation = restaurant.InviteByEmail(
            "staff@example.com",
            RestaurantRole.Staff,
            "second-token-hash",
            OwnerUserId,
            CreatedAtUtc.AddMinutes(1),
            InvitationExpiresAtUtc);

        Assert.Equal(2, restaurant.Invitations.Count);
        Assert.Contains(firstInvitation, restaurant.Invitations);
        Assert.Contains(secondInvitation, restaurant.Invitations);
    }

    [Fact]
    public void InviteByEmail_WithDuplicatePendingEmail_ThrowsInvalidOperationException()
    {
        var restaurant = CreateRestaurant();

        restaurant.InviteByEmail(
            "manager@example.com",
            RestaurantRole.Manager,
            "first-token-hash",
            OwnerUserId,
            CreatedAtUtc,
            InvitationExpiresAtUtc);

        Assert.Throws<InvalidOperationException>(() =>
            restaurant.InviteByEmail(
                "manager@example.com",
                RestaurantRole.Staff,
                "second-token-hash",
                OwnerUserId,
                CreatedAtUtc.AddMinutes(1),
                InvitationExpiresAtUtc));
    }

    [Fact]
    public void InviteByEmail_WithDifferentEmailCasing_RejectsDuplicatePendingInvitation()
    {
        var restaurant = CreateRestaurant();

        restaurant.InviteByEmail(
            "manager@example.com",
            RestaurantRole.Manager,
            "first-token-hash",
            OwnerUserId,
            CreatedAtUtc,
            InvitationExpiresAtUtc);

        Assert.Throws<InvalidOperationException>(() =>
            restaurant.InviteByEmail(
                "MANAGER@EXAMPLE.COM",
                RestaurantRole.Staff,
                "second-token-hash",
                OwnerUserId,
                CreatedAtUtc.AddMinutes(1),
                InvitationExpiresAtUtc));
    }

    [Fact]
    public void InviteByEmail_AfterPreviousInvitationWasRevoked_AllowsNewInvitation()
    {
        var restaurant = CreateRestaurant();

        var firstInvitation = restaurant.InviteByEmail(
            "manager@example.com",
            RestaurantRole.Manager,
            "first-token-hash",
            OwnerUserId,
            CreatedAtUtc,
            InvitationExpiresAtUtc);

        firstInvitation.Revoke(CreatedAtUtc.AddMinutes(5));

        var secondInvitation = restaurant.InviteByEmail(
            "manager@example.com",
            RestaurantRole.Manager,
            "second-token-hash",
            OwnerUserId,
            CreatedAtUtc.AddMinutes(10),
            InvitationExpiresAtUtc);

        Assert.Equal(InvitationStatus.Revoked, firstInvitation.Status);
        Assert.Equal(InvitationStatus.Pending, secondInvitation.Status);
        Assert.Equal(2, restaurant.Invitations.Count);
    }

    [Fact]
    public void InviteByEmail_WithOwnerRole_ThrowsArgumentException()
    {
        var restaurant = CreateRestaurant();

        Assert.Throws<ArgumentException>(() =>
            restaurant.InviteByEmail(
                "manager@example.com",
                RestaurantRole.Owner,
                "token-hash",
                OwnerUserId,
                CreatedAtUtc,
                InvitationExpiresAtUtc));

        Assert.Empty(restaurant.Invitations);
    }

    [Fact]
    public void InviteByEmail_WithEmptyInviterId_ThrowsArgumentException()
    {
        var restaurant = CreateRestaurant();

        Assert.Throws<ArgumentException>(() =>
            restaurant.InviteByEmail(
                "manager@example.com",
                RestaurantRole.Manager,
                "token-hash",
                Guid.Empty,
                CreatedAtUtc,
                InvitationExpiresAtUtc));

        Assert.Empty(restaurant.Invitations);
    }

    [Fact]
    public void InviteByEmail_WithInvalidEmail_ThrowsArgumentException()
    {
        var restaurant = CreateRestaurant();

        Assert.Throws<ArgumentException>(() =>
            restaurant.InviteByEmail(
                "invalid-email",
                RestaurantRole.Manager,
                "token-hash",
                OwnerUserId,
                CreatedAtUtc,
                InvitationExpiresAtUtc));

        Assert.Empty(restaurant.Invitations);
    }

    [Fact]
    public void InviteByEmail_WithEmptyTokenHash_ThrowsArgumentException()
    {
        var restaurant = CreateRestaurant();

        Assert.Throws<ArgumentException>(() =>
            restaurant.InviteByEmail(
                "manager@example.com",
                RestaurantRole.Manager,
                "",
                OwnerUserId,
                CreatedAtUtc,
                InvitationExpiresAtUtc));

        Assert.Empty(restaurant.Invitations);
    }

    [Fact]
    public void InviteByEmail_WithExpirationBeforeCreation_ThrowsArgumentException()
    {
        var restaurant = CreateRestaurant();

        Assert.Throws<ArgumentException>(() =>
            restaurant.InviteByEmail(
                "manager@example.com",
                RestaurantRole.Manager,
                "token-hash",
                OwnerUserId,
                CreatedAtUtc,
                CreatedAtUtc));

        Assert.Empty(restaurant.Invitations);
    }

    [Fact]
    public void AcceptInvitation_WithValidInvitation_CreateActiveMembership()
    {
        var restaurant = CreateRestaurant();

        var invitation = restaurant.InviteByEmail(
            "employee@example.com",
            RestaurantRole.Manager,
            InvitationTokenHash,
            OwnerUserId,
            CreatedAtUtc,
            InvitationExpiresAtUtc);

        var acceptedAtUtc = CreatedAtUtc.AddHours(3);

        var membership = restaurant.AcceptInvitation(
            InvitationTokenHash,
            RecipientUserId,
            "employee@example.com",
            acceptedAtUtc);
        
        Assert.Equal(restaurant.Id, membership.RestaurantId);
        Assert.Equal(RecipientUserId, membership.UserId);
        Assert.Equal(RestaurantRole.Manager, membership.Role);
        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.Equal(acceptedAtUtc, membership.CreatedAtUtc);
        Assert.Equal(acceptedAtUtc, membership.ActivatedAtUtc);
        
        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal(RecipientUserId, invitation.AcceptedByUserId);
        Assert.Equal(acceptedAtUtc, invitation.AcceptedAtUtc);

        Assert.Contains(membership, restaurant.Memberships);
        Assert.Equal(2, restaurant.Memberships.Count);
        Assert.Contains(membership, restaurant.Memberships);

        Assert.Contains(
            restaurant.Memberships,
            item => item.UserId == OwnerUserId &&
                    item.Role == RestaurantRole.Owner &&
                    item.Status == MembershipStatus.Active);
        Assert.Single(restaurant.Invitations);
    }

    [Fact]
    public void AcceptInvitation_WithUnknownTokenHash_ThrowsInvalidOperationException()
    {
        var restaurant = CreateRestaurant();

        Assert.Throws<InvalidOperationException>(() =>
            restaurant.AcceptInvitation(
                "unknown-token-hash",
                RecipientUserId,
                "employee@example.com",
                CreatedAtUtc.AddHours(1)));

        Assert.Single(restaurant.Memberships);
        Assert.Empty(restaurant.Invitations);
    }

    [Fact]
    public void AcceptInvitation_WithDifferentEmail_LeavesInvitationPending()
    {
        var restaurant = CreateRestaurant();

        var invitation = restaurant.InviteByEmail(
            "employee@example.com",
            RestaurantRole.Staff,
            InvitationTokenHash,
            OwnerUserId,
            CreatedAtUtc,
            InvitationExpiresAtUtc);

        Assert.Throws<InvalidOperationException>(() =>
            restaurant.AcceptInvitation(
                InvitationTokenHash,
                RecipientUserId,
                "another@example.com",
                CreatedAtUtc.AddHours(1)));
        
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Null(invitation.AcceptedByUserId);
        Assert.Null(invitation.AcceptedAtUtc);
        Assert.Single(restaurant.Memberships);
    }

    [Theory]
    [InlineData(MembershipStatus.Invited)]
    [InlineData(MembershipStatus.Active)]
    [InlineData(MembershipStatus.Suspended)]
    public void AcceptInvitation_WithExistingMembership_RejectsAcceptance(
        MembershipStatus existingStatus)
    {
        var restaurant = CreateRestaurant();

        var invitation = restaurant.InviteByEmail(
            "employee@example.com",
            RestaurantRole.Staff,
            InvitationTokenHash,
            OwnerUserId,
            CreatedAtUtc,
            InvitationExpiresAtUtc);

        var existingMembership = restaurant.InviteMember(
            RecipientUserId,
            RestaurantRole.Staff,
            CreatedAtUtc.AddMinutes(1));

        if (existingStatus is MembershipStatus.Active or MembershipStatus.Suspended)
        {
            existingMembership.Activate((CreatedAtUtc.AddMinutes(2)));
        }

        if (existingStatus == MembershipStatus.Suspended)
        {
            existingMembership.Suspend();
        }

        Assert.Throws<InvalidOperationException>(() =>
            restaurant.AcceptInvitation(
                InvitationTokenHash,
                RecipientUserId,
                "employee@example.com",
                CreatedAtUtc.AddHours(1)));
        
        Assert.Equal(existingStatus, existingMembership.Status);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Null(invitation.AcceptedByUserId);
        Assert.Equal(2, restaurant.Memberships.Count);
    }

    [Fact]
    public void AcceptInvitation_WithAlreadyAcceptedInvitation_ThrowsInvalidOperationException()
    {
        var restaurant = CreateRestaurant();

        restaurant.InviteByEmail(
            "employee@example.com",
            RestaurantRole.Staff,
            InvitationTokenHash,
            OwnerUserId,
            CreatedAtUtc,
            InvitationExpiresAtUtc);

        restaurant.AcceptInvitation(
            InvitationTokenHash,
            RecipientUserId,
            "employee@example.com",
            CreatedAtUtc.AddHours(1));
        
        Assert.Throws<InvalidOperationException>(() =>
            restaurant.AcceptInvitation(
                InvitationTokenHash,
                RecipientUserId,
                "employee@example.com",
                CreatedAtUtc.AddHours(2)));

        Assert.Equal(2, restaurant.Memberships.Count);
    }

    private static Restaurant CreateRestaurant()
    {
        return Restaurant.Create(
            "Delivery Restaurant",
            CreateCnpj(),
            CreateAddress(),
            OwnerUserId,
            CreatedAtUtc);
    }

    private static Cnpj CreateCnpj()
    {
        return Cnpj.Create("11222333000181");
    }

    private static RestaurantAddress CreateAddress()
    {
        return RestaurantAddress.Create(
            "Avenida Beira-Mar Norte",
            "1000",
            "Sala 2",
            "Centro",
            "Florianópolis",
            "SC",
            "88015-700");
    }
}
