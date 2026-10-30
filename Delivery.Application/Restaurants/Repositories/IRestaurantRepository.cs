using Delivery.Domain.Restaurants.Entities;

namespace Delivery.Application.Restaurants.Repositories;

public interface IRestaurantRepository
{
    Task AddAsync(
        Restaurant restaurant,
        CancellationToken cancellationToken = default);
}
