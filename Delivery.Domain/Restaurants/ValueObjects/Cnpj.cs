
namespace Delivery.Domain.Restaurants.ValueObjects;

public sealed record Cnpj
{
    public string Value { get; }

    private Cnpj(string value)
    {
        Value = value;
    }

    public static Cnpj Create(string value)
    {
        if (!TryCreate(value, out var cnpj))
        {
            throw new ArgumentException("Invalid CNPJ.", nameof(value));
        }

        return cnpj;
    }

    public static bool TryCreate(string? value, out Cnpj? cnpj)
    {
        cnpj = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = Normalize(value);

        if (normalized.Length != 14)
        {
            return false;
        }

        for (var i = 0; i < 12; i++)
        {
            if (!char.IsAsciiLetterOrDigit(normalized[i]))
            {
                return false;
            }
        }

        if (!char.IsAsciiDigit(normalized[12]) ||
            !char.IsAsciiDigit(normalized[13]))
        {
            return false;
        }

        // Reject CNPJs with 12 identical base characters.
        if (normalized.AsSpan(1, 11).IndexOfAnyExcept(normalized[0]) < 0)
        {
            return false;
        }

        if (!HasValidCheckDigits(normalized))
        {
            return false;
        }

        cnpj = new Cnpj(normalized);

        return true;
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .Replace(".", string.Empty)
            .Replace("/", string.Empty)
            .Replace("-", string.Empty)
            .ToUpperInvariant();
    }

    private static bool HasValidCheckDigits(string value)
    {
        var firstDigit = CalculateCheckDigit(value.AsSpan(0, 12));

        if (firstDigit != value[12] - '0')
        {
            return false;
        }

        var secondDigit = CalculateCheckDigit(value.AsSpan(0, 13));

        return secondDigit == value[13] - '0';
    }

    private static int CalculateCheckDigit(ReadOnlySpan<char> value)
    {
        var sum = 0;
        var weight = 2;

        for (var i = value.Length - 1; i >= 0; i--)
        {
            var characterValue = value[i] - 48;

            sum += characterValue * weight;

            weight = weight == 9 ? 2 : weight + 1;
        }

        var remainder = sum % 11;

        return remainder < 2 ? 0 : 11 - remainder;
    }

    public override string ToString() => Value;
}
