namespace Delivery.Domain.Restaurants.ValueObjects;

public sealed record RestaurantAddress
{
    public string Street { get; }
    public string Number { get; }
    public string? Complement { get; }
    public string Neighborhood { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }

    private RestaurantAddress(
        string street,
        string number,
        string? complement,
        string neighborhood,
        string city,
        string state,
        string postalCode)
    {
        Street = street;
        Number = number;
        Complement = complement;
        Neighborhood = neighborhood;
        City = city;
        State = state;
        PostalCode = postalCode;
    }

    public static RestaurantAddress Create(
        string street,
        string number,
        string? complement,
        string neighborhood,
        string city,
        string state,
        string postalCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(neighborhood);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);

        var normalizedState = state.Trim().ToUpperInvariant();
        var normalizedPostalCode = NormalizePostalCode(postalCode);

        if (normalizedState.Length != 2 || !normalizedState.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("State must be a valid two-letter UF.", nameof(state));
        }

        if (normalizedPostalCode.Length != 8 || !normalizedPostalCode.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("Post code must contain exactly 8 digits.", nameof(postalCode));
        }

        return new RestaurantAddress(
            street.Trim(),
            number.Trim(),
            NormalizeOptional(complement),
            neighborhood.Trim(),
            city.Trim(),
            normalizedState,
            normalizedPostalCode);
    }

    private static string NormalizePostalCode(string postalCode)
    {
        return postalCode
            .Trim()
            .Replace("-", string.Empty);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
