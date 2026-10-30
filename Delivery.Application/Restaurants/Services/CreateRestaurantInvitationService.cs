using System.Security.Cryptography;
using System.Text;
using Delivery.Application.Abstractions.Persistence;
using Delivery.Application.Abstractions.Security;
using Delivery.Application.Restaurants.Commands;
using Delivery.Application.Restaurants.Repositories;
using Delivery.Application.Restaurants.Results;
using Delivery.Domain.Restaurants.Enums;

namespace Delivery.Application.Restaurants.Services;

public sealed class CreateRestaurantInvitationService(
    IRestaurantRepository restaurantRepository,
    IUnitOfWork unitOfWork,
    IInvitationTokenGenerator tokenGenerator,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan InvitationLifetime =
        TimeSpan.FromDays(7);

    public async Task<CreateRestaurantInvitationResult> ExecuteAsync(
        CreateRestaurantInvitationCommand command,
        Guid inviterUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (inviterUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Inviter user ID cannot be empty.",
                nameof(inviterUserId));
        }

        if (command.RestaurantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Restaurant ID cannot be empty.",
                nameof(command));
        }

        if (!Enum.IsDefined(command.Role) ||
            command.Role == RestaurantRole.Owner)
        {
            throw new ArgumentException(
                "Invitation role must be Manager or Staff.",
                nameof(command));
        }

        var restaurant = await restaurantRepository
            .FindByIdForMemberAsync(
                command.RestaurantId,
                inviterUserId,
                cancellationToken);

        if (restaurant is null)
        {
            throw new InvalidOperationException(
                "Restaurant was not found or the user is not an active member.");
        }

        var inviterMembership = restaurant.Memberships
            .SingleOrDefault(membership =>
                membership.UserId == inviterUserId &&
                membership.Status == MembershipStatus.Active);

        if (inviterMembership?.Role != RestaurantRole.Owner)
        {
            throw new UnauthorizedAccessException(
                "Only the restaurant owner can invite members.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAtUtc = now.Add(InvitationLifetime);

        var token = tokenGenerator.Generate();

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        var invitation = restaurant.InviteByEmail(
            command.Email,
            command.Role,
            tokenHash,
            inviterUserId,
            now,
            expiresAtUtc);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateRestaurantInvitationResult(
            invitation.Id,
            token,
            invitation.ExpiresAtUtc);
    }
}
