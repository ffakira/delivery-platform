namespace Delivery.Application.Restaurants.Commands;

public sealed record CreateRestaurantCommand(
    string Name,
    string Cnpj,
    string Street,
    string Number,
    string? Complement,
    string Neighborhood,
    string City,
    string State,
    string PostalCode);
