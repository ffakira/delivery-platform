namespace Delivery.Application.Restaurants.Queries;

public record GetRestaurantDetailsQuery(
    Guid RestaurantId,
    Guid UserId);
