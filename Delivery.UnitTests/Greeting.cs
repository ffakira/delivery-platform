namespace Delivery.UnitTests;

public static class Greeting
{
    public static string CreateMessage(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return $"Hello, {name}!";
    }
}
