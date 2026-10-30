using Delivery.Application.Abstractions.Persistence;
using Delivery.Application.Restaurants.Commands;
using Delivery.Application.Restaurants.Repositories;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.ValueObjects;

namespace Delivery.Application.Restaurants.Services;

public class CreateRestaurantService(
    IRestaurantRepository restaurantRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<Guid> ExecuteAsync(
        CreateRestaurantCommand command,
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        
        var cnpj = Cnpj.Create(command.Cnpj);

        var address = RestaurantAddress.Create(
            command.Street,
            command.Number,
            command.Complement,
            command.Neighborhood,
            command.City,
            command.State,
            command.PostalCode);

        var restaraunt = Restaurant.Create(
            command.Name,
            cnpj,
            address,
            ownerUserId,
            timeProvider.GetUtcNow().UtcDateTime);

        await restaurantRepository.AddAsync(
            restaraunt,
            cancellationToken);
        
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return restaraunt.Id;
    }
}