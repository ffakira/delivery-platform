using Delivery.Application.Restaurants.Queries;
using Delivery.Application.Restaurants.Repositories;
using Delivery.Domain.Restaurants.Enums;

namespace Delivery.Application.Restaurants.Services;

public sealed class ListMyRestaurantsService(
    IRestaurantRepository restaurantRepository)
{
    public async Task<IReadOnlyList<RestaurantListItem>> ExecuteAsync(
        ListMyRestaurantsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.UserId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID cannot be empty.",
                nameof(query));
        }

        var restaurants = await restaurantRepository.ListByMemberAsync(
            query.UserId,
            cancellationToken);

        return restaurants
            .Select(restaurant =>
            {
                var membership = restaurant.Memberships.Single(membership =>
                    membership.UserId == query.UserId &&
                    membership.Status == MembershipStatus.Active);

                return new RestaurantListItem(
                    restaurant.Id,
                    restaurant.Name,
                    restaurant.Cnpj.Value,
                    restaurant.Address.Street,
                    restaurant.Address.Number,
                    restaurant.Address.Complement,
                    restaurant.Address.Neighborhood,
                    restaurant.Address.City,
                    restaurant.Address.State,
                    restaurant.Address.PostalCode,
                    membership.Role,
                    restaurant.CreatedAtUtc);
            })
            .ToArray();
    }
}
