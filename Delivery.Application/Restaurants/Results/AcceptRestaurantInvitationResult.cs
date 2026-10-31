using Delivery.Domain.Restaurants.Enums;

namespace Delivery.Application.Restaurants.Results;

public sealed record AcceptRestaurantInvitationResult(
    Guid RestaurantId,
    Guid MembershipId,
    RestaurantRole Role);
