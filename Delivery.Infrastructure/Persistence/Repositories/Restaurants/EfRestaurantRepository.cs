using Delivery.Application.Restaurants.Repositories;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;
using Microsoft.EntityFrameworkCore;

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

    public async Task<IReadOnlyList<Restaurant>> ListByMemberAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Restaurants
            .AsNoTracking()
            .Where(restaurant =>
                restaurant.Memberships.Any(membership =>
                    membership.UserId == userId &&
                    membership.Status == MembershipStatus.Active))
            .Include(restaurant =>
                restaurant.Memberships.Where(membership =>
                    membership.UserId == userId &&
                    membership.Status == MembershipStatus.Active))
            .OrderBy(restaurant => restaurant.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Restaurant?> FindByIdForMemberAsync(
        Guid restaurantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Restaurants
            .Where(restaurant =>
                restaurant.Id == restaurantId &&
                restaurant.Memberships.Any(membership =>
                    membership.UserId == userId &&
                    membership.Status == MembershipStatus.Active))
            .Include(restaurant =>
                restaurant.Memberships.Where(membership =>
                    membership.UserId == userId &&
                    membership.Status == MembershipStatus.Active))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
