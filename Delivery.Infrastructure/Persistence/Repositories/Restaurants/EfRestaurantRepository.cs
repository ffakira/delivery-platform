using Delivery.Application.Restaurants.Repositories;
using Delivery.Domain.Restaurants.Entities;

namespace Delivery.Infrastructure.Persistence.Repositories.Restaurants;

public class EfRestaurantRepository(
    ApplicationDbContext dbContext) : IRestaurantRepository
{
    public async Task AddAsync(
        Restaurant restaurant,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Restaurants.AddAsync(
            restaurant,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
