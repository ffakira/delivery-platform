using Delivery.Domain.Restaurants.Enums;

namespace Delivery.Application.Restaurants.Queries;

public record RestaurantDetails(
    Guid Id,
    string Name,
    string Cnpj,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    string PostalCode,
    RestaurantRole Role,
    DateTime CreatedAtUtc);
