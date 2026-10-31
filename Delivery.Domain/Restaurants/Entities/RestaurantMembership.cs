using Delivery.Domain.Restaurants.Enums;

namespace Delivery.Domain.Restaurants.Entities;

public sealed class RestaurantMembership
{
    public Guid Id { get; }
    public Guid RestaurantId { get; }
    public Guid UserId { get; }
    public RestaurantRole Role { get; private set; }
    public MembershipStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; }
    public DateTime? ActivatedAtUtc { get; private set; }

    private RestaurantMembership(
        Guid id,
        Guid restaurantId,
        Guid userId,
        RestaurantRole role,
        MembershipStatus status,
        DateTime createdAtUtc,
        DateTime? activatedAtUtc)
    {
        Id = id;
        RestaurantId = restaurantId;
        UserId = userId;
        Role = role;
        Status = status;
        CreatedAtUtc = createdAtUtc;
        ActivatedAtUtc = activatedAtUtc;
    }

    public static RestaurantMembership Invite(
        Guid restaurantId,
        Guid userId,
        RestaurantRole role,
        DateTime createdAtUtc)
    {
        if (restaurantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Restaurant ID cannot be empty.",
                nameof(restaurantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID cannot be empty.",
                nameof(userId));
        }

        if (role == RestaurantRole.Owner)
        {
            throw new ArgumentException(
                "Owner membership must be assigned during restaurant creation.",
                nameof(role));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        return new RestaurantMembership(
            Guid.NewGuid(),
            restaurantId,
            userId,
            role,
            MembershipStatus.Invited,
            createdAtUtc,
            null);
    }

    internal static RestaurantMembership CreateOwner(
        Guid restaurantId,
        Guid userId,
        DateTime createdAtUtc)
    {
        if (restaurantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Restaurant ID cannot be empty.",
                nameof(restaurantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID cannot be empty.",
                nameof(userId));
        }

        return new RestaurantMembership(
            Guid.NewGuid(),
            restaurantId,
            userId,
            RestaurantRole.Owner,
            MembershipStatus.Active,
            createdAtUtc,
            createdAtUtc);
    }

    internal static RestaurantMembership CreateFromAcceptedInvitation(
        Guid restaurantId,
        Guid userId,
        RestaurantRole role,
        DateTime acceptedAtUtc)
    {
        if (restaurantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Restaurant ID cannot be empty.",
                nameof(restaurantId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID cannot be empty.",
                nameof(userId));
        }

        if (!Enum.IsDefined(role) || role == RestaurantRole.Owner)
        {
            throw new ArgumentException(
                "Membership role must be Manager or Staff.",
                nameof(role));
        }

        return new RestaurantMembership(
            Guid.NewGuid(),
            restaurantId,
            userId,
            role,
            MembershipStatus.Active,
            acceptedAtUtc,
            acceptedAtUtc);
    }

    public void Activate(DateTime activatedAtUtc)
    {
        if (Status != MembershipStatus.Invited)
        {
            throw new InvalidOperationException(
                "Only invited memberships can be activated.");
        }

        Status = MembershipStatus.Active;
        ActivatedAtUtc = activatedAtUtc;
    }

    public void Suspend()
    {
        if (Role == RestaurantRole.Owner)
        {
            throw new InvalidOperationException(
                "The restaurant owner cannot be suspended.");
        }

        if (Status != MembershipStatus.Active)
        {
            throw new InvalidOperationException(
                "Only active memberships can be suspended.");
        }

        Status = MembershipStatus.Suspended;
    }

    public void ChangeRole(RestaurantRole role)
    {
        if (Role == RestaurantRole.Owner)
        {
            throw new InvalidOperationException(
                "The restaurant owner's role cannot be changed.");
        }

        if (Status != MembershipStatus.Active)
        {
            throw new InvalidOperationException(
                "Only active memberships can change roles.");
        }

        if (!Enum.IsDefined(role) || role == RestaurantRole.Owner)
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        Role = role;
    }
}
