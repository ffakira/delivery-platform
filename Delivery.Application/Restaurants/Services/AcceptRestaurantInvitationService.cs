using System.Security.Cryptography;
using System.Text;
using Delivery.Application.Abstractions.Persistence;
using Delivery.Application.Restaurants.Commands;
using Delivery.Application.Restaurants.Repositories;
using Delivery.Application.Restaurants.Results;

namespace Delivery.Application.Restaurants.Services;

public sealed class AcceptRestaurantInvitationService(
    IRestaurantRepository restaurantRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<AcceptRestaurantInvitationResult> ExecuteAsync(
        AcceptRestaurantInvitationCommand command,
        Guid userId,
        string verifiedEmail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Token);
        ArgumentException.ThrowIfNullOrWhiteSpace(verifiedEmail);

        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID cannot be empty.",
                nameof(userId));
        }

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(command.Token)));

        var restaurant = await restaurantRepository
            .FindByInvitationTokenHashAsync(
                tokenHash,
                cancellationToken);

        if (restaurant is null)
        {
            throw new InvalidOperationException(
                "The invitation was not found.");
        }

        var membership = restaurant.AcceptInvitation(
            tokenHash,
            userId,
            verifiedEmail,
            timeProvider.GetUtcNow().UtcDateTime);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AcceptRestaurantInvitationResult(
            restaurant.Id,
            membership.Id,
            membership.Role);
    }
}
