using System.Net.Mail;
using Delivery.Domain.Restaurants.Enums;

namespace Delivery.Domain.Restaurants.Entities;

public sealed class RestaurantInvitation
{
    public Guid Id { get; private set; }
    
    public Guid RestaurantId { get; private set; }

    public string Email { get; private set; } = null!;

    public RestaurantRole Role { get; private set; }

    public string TokenHash { get; private set; } = null!;
    
    public InvitationStatus Status { get; private set; }
    
    public Guid InvitedByUserId { get; private set; }

    public Guid? AcceptedByUserId { get; private set; }
    
    public DateTime CreatedAtUtc { get; private set; }
    
    public DateTime ExpiresAtUtc { get; private set; }
    
    public DateTime? AcceptedAtUtc { get; private set; }
    
    public DateTime? RevokedAtUtc { get; private set; }
    
    private  RestaurantInvitation()
    {
    }

    private RestaurantInvitation(
        Guid id,
        Guid restaurantId,
        string email,
        RestaurantRole role,
        string tokenHash,
        Guid invitedByUserId,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        Id = id;
        RestaurantId = restaurantId;
        Email = email;
        Role = role;
        TokenHash = tokenHash;
        InvitedByUserId = invitedByUserId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = InvitationStatus.Pending;
    }

    public static RestaurantInvitation Create(
        Guid restaurantId,
        string email,
        RestaurantRole role,
        string tokenHash,
        Guid invitedByUserId,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        if (restaurantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Restaurant ID cannot be empty.",
                nameof(restaurantId));
        }

        if (invitedByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Inviting user ID cannot be empty.",
                nameof(invitedByUserId));
        }

        if (!Enum.IsDefined(role) || role == RestaurantRole.Owner)
        {
            throw new ArgumentException(
                "Invitation role must be Manager or Staff.",
                nameof(role));
        }
        
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        var normalizedEmail = NormalizeEmail(email);

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException(
                "Invitation expiration must be after its creation.",
                nameof(expiresAtUtc));
        }

        return new RestaurantInvitation(
            Guid.NewGuid(),
            restaurantId,
            normalizedEmail,
            role,
            tokenHash,
            invitedByUserId,
            createdAtUtc,
            expiresAtUtc);
    }

    public void Accept(
        Guid userId,
        string email,
        DateTime acceptedAtUtc)
    {
        EnsurePending();

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID cannot be empty.",
                nameof(userId));
        }

        var normalizedEmail = NormalizeEmail(email);

        if (!string.Equals(
                Email,
                normalizedEmail,
                StringComparison.InvariantCultureIgnoreCase))
        {
            throw new InvalidOperationException(
                "The authenticated user's email does not match the invitation");
        }

        if (acceptedAtUtc >= ExpiresAtUtc)
        {
            throw new InvalidOperationException(
                "The invitation has expired.");
        }

        if (acceptedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentException(
                "Acceptance timestamp cannot precede invitation creation.",
                nameof(acceptedAtUtc));
        }
        
        AcceptedByUserId = userId;
        AcceptedAtUtc = acceptedAtUtc;
        Status = InvitationStatus.Accepted;
    }

    public void Revoke(DateTime revokedAtUtc)
    {
        EnsurePending();

        if (revokedAtUtc < CreatedAtUtc)
        {
            throw new ArgumentException(
                "Revocation timestamp cannot precede invitation creation.",
                nameof(revokedAtUtc));
        }
        
        RevokedAtUtc = revokedAtUtc;
        Status = InvitationStatus.Revoked;
    }

    public void Expire(DateTime currentTimeUtc)
    {
        EnsurePending();

        if (currentTimeUtc < ExpiresAtUtc)
        {
            throw new InvalidOperationException(
                "The invitation has not expired yet.");
        }
        
        Status = InvitationStatus.Expired;
    }

    private void EnsurePending()
    {
        if (Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException(
                "Only pending invitations can change their status.");
        }
    }

    private static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalizedEmail = email.Trim().ToLowerInvariant();

        try
        {
            var address = new MailAddress(normalizedEmail);

            if (!string.Equals(
                    address.Address,
                    normalizedEmail,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Email address is invalid.",
                    nameof(email));
            }
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "email address is invalid.",
                nameof(email),
                exception);
        }
        
        return normalizedEmail;
    }
}