namespace Delivery.UnitTests;

public sealed class GreetingTests
{
    [Fact]
    public void CreateMessage_ReturnsGreetingForName()
    {
        // Arrange
        const string name = "World";

        // Act
        var result = Greeting.CreateMessage(name);

        // Assert
        Assert.Equal("Hello, World!", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void CreateMessage_RejectsEmptyOrWhitespaceName(string name)
    {
        Assert.Throws<ArgumentException>(
            () => Greeting.CreateMessage(name));
    }
}
