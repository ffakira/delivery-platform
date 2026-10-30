using Delivery.Application.Restaurants.Repositories;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;

namespace Delivery.UnitTests.Restaurants.Fakes;

public sealed class FakeRestaurantRepository(
    params Restaurant[] restaurants) : IRestaurantRepository
{
    private readonly List<Restaurant> _restaurants = [.. restaurants];

    public IReadOnlyList<Restaurant> Restaurants => _restaurants;

    public Task AddAsync(
        Restaurant restaurant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(restaurant);
        cancellationToken.ThrowIfCancellationRequested();

        _restaurants.Add(restaurant);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Restaurant>> ListByMemberAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<Restaurant> result = _restaurants
            .Where(restaurant =>
                restaurant.Memberships.Any(membership =>
                    membership.UserId == userId &&
                    membership.Status == MembershipStatus.Active))
            .OrderBy(restaurant => restaurant.Name)
            .ToArray();

        return Task.FromResult(result);
    }

    public Task<Restaurant?> FindByIdForMemberAsync(
        Guid restaurantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var restaurant = _restaurants.SingleOrDefault(item =>
            item.Id == restaurantId &&
            item.Memberships.Any(membership =>
                membership.UserId == userId &&
                membership.Status == MembershipStatus.Active));

        return Task.FromResult(restaurant);
    }

    public Task<Restaurant?> FindByInvitationTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);
        cancellationToken.ThrowIfCancellationRequested();
        
        var restaurant = _restaurants.SingleOrDefault(item =>
            item.Invitations.Any(invitation =>
                invitation.TokenHash == tokenHash));

        return Task.FromResult(restaurant);
    }
}
