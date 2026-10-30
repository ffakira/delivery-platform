namespace Delivery.Application.Restaurants.Results;

public sealed record CreateRestaurantInvitationResult(
    Guid InvitationId,
    string Token,
    DateTime ExpiresAtUtc);