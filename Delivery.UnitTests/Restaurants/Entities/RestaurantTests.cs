
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

    private static readonly DateTime CreatedAtUtc =
        new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

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
