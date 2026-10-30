
using Delivery.Domain.Restaurants.Enums;
using Delivery.Domain.Restaurants.ValueObjects;

namespace Delivery.Domain.Restaurants.Entities;

public sealed class Restaurant
{
    private readonly List<RestaurantMembership> _memberships = [];

    public Guid Id { get; }
    public string Name { get; private set; }
    public Cnpj Cnpj { get; }
    public RestaurantAddress Address { get; private set; }
    public DateTime CreatedAtUtc { get; }

    public IReadOnlyCollection<RestaurantMembership> Memberships =>
        _memberships.AsReadOnly();

    private Restaurant()
    {
        Name = null!;
        Cnpj = null!;
        Address = null!;
    }

    private Restaurant(
        Guid id,
        string name,
        Cnpj cnpj,
        RestaurantAddress address,
        DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        Cnpj = cnpj;
        Address = address;
        CreatedAtUtc = createdAtUtc;
    }

    public static Restaurant Create(
        string name,
        Cnpj cnpj,
        RestaurantAddress address,
        Guid ownerUserId,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(cnpj);
        ArgumentNullException.ThrowIfNull(address);

        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Owner user ID cannot be empty.",
                nameof(ownerUserId));
        }

        var restaurant = new Restaurant(
            Guid.NewGuid(),
            name.Trim(),
            cnpj,
            address,
            createdAtUtc);

        restaurant._memberships.Add(
            RestaurantMembership.CreateOwner(
                restaurant.Id,
                ownerUserId,
                createdAtUtc));

        return restaurant;
    }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
    }

    public void ChangeAddress(RestaurantAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        Address = address;
    }

    public RestaurantMembership InviteMember(
        Guid userId,
        RestaurantRole role,
        DateTime invitedAtUtc)
    {
        if (_memberships.Any(membership => membership.UserId == userId))
        {
            throw new InvalidOperationException(
                "This user already has a membership in the restaurant.");
        }

        var membership = RestaurantMembership.Invite(
            Id,
            userId,
            role,
            invitedAtUtc);

        _memberships.Add(membership);

        return membership;
    }
}
