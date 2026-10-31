using Delivery.Domain.Restaurants.Enums;
using Delivery.Domain.Restaurants.ValueObjects;

namespace Delivery.Domain.Restaurants.Entities;

public sealed class Restaurant
{
    private readonly List<RestaurantMembership> _memberships = [];
    private readonly List<RestaurantInvitation> _invitations = [];

    public Guid Id { get; }
    public string Name { get; private set; }
    public Cnpj Cnpj { get; }
    public RestaurantAddress Address { get; private set; }
    public DateTime CreatedAtUtc { get; }

    public IReadOnlyCollection<RestaurantMembership> Memberships =>
        _memberships.AsReadOnly();

    public IReadOnlyCollection<RestaurantInvitation> Invitations =>
        _invitations.AsReadOnly();

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
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID cannot be empty.",
                nameof(userId));
        }

        if (role == RestaurantRole.Owner)
        {
            throw new ArgumentException(
                "Owner role cannot be assigned through an invitation.",
                nameof(role));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "The specified restaurant role is invalid.");
        }

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

    public RestaurantInvitation InviteByEmail(
        string email,
        RestaurantRole role,
        string tokenHash,
        Guid invitedByUserId,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (_invitations.Any(invitation =>
                invitation.Status == InvitationStatus.Pending &&
                string.Equals(
                    invitation.Email,
                    normalizedEmail,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "This email already has a pending invitation.");
        }

        var invitation = RestaurantInvitation.Create(
            Id,
            normalizedEmail,
            role,
            tokenHash,
            invitedByUserId,
            createdAtUtc,
            expiresAtUtc);

        _invitations.Add(invitation);

        return invitation;
    }

    public RestaurantMembership AcceptInvitation(
        string tokenHash,
        Guid userId,
        string email,
        DateTime acceptedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID cannot be empty",
                nameof(userId));
        }

        var invitation = _invitations.SingleOrDefault(invitation =>
            string.Equals(
                invitation.TokenHash,
                tokenHash,
                StringComparison.Ordinal));

        if (invitation is null)
        {
            throw new InvalidOperationException(
                "The invitation was not found.");
        }

        if (_memberships.Any(membership => membership.UserId == userId))
        {
            throw new InvalidOperationException(
                "This user already has a membership in the restaurant.");
        }
        
        invitation.Accept(userId, email, acceptedAtUtc);

        var membership = RestaurantMembership.CreateFromAcceptedInvitation(
            Id,
            userId,
            invitation.Role,
            acceptedAtUtc);
        
        _memberships.Add(membership);
        return membership;
    }
}
