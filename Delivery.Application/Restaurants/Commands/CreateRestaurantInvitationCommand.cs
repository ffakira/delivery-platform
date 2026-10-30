using Delivery.Domain.Restaurants.Enums;

namespace Delivery.Application.Restaurants.Commands;

public sealed record CreateRestaurantInvitationCommand(
    Guid RestaurantId,
    string Email,
    RestaurantRole Role);
