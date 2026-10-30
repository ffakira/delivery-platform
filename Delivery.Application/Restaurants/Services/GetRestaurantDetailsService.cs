using Delivery.Application.Restaurants.Queries;
using Delivery.Application.Restaurants.Repositories;
using Delivery.Domain.Restaurants.Enums;

namespace Delivery.Application.Restaurants.Services;

public sealed class GetRestaurantDetailsService(
    IRestaurantRepository restaurantRepository)
{
    public async Task<RestaurantDetails?> ExecuteAsync(
        GetRestaurantDetailsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.RestaurantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Restaurant ID cannot be empty.",
                nameof(query));
        }

        if (query.UserId == Guid.Empty)
        {
            throw new ArgumentException(
                "User ID cannot be empty.",
                nameof(query));
        }

        var restaurant = await restaurantRepository.FindByIdForMemberAsync(
            query.RestaurantId,
            query.UserId,
            cancellationToken);

        if (restaurant is null)
        {
            return null;
        }

        var membership = restaurant.Memberships.Single(item =>
            item.UserId == query.UserId &&
            item.Status == MembershipStatus.Active);

        return new RestaurantDetails(
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
    }
}
