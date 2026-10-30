
using Delivery.Domain.Restaurants.ValueObjects;

namespace Delivery.UnitTests.Restaurants.ValueObjects;

public sealed class RestaurantAddressTests
{
    [Fact]
    public void Create_WithValidAddress_ReturnsAddress()
    {
        var address = RestaurantAddress.Create(
            street: "Avenida Beira-Mar Norte",
            number: "1000",
            complement: "Sala 2",
            neighborhood: "Centro",
            city: "Florianópolis",
            state: "SC",
            postalCode: "88015-700"
        );

        Assert.Equal("Avenida Beira-Mar Norte", address.Street);
        Assert.Equal("1000", address.Number);
        Assert.Equal("Sala 2", address.Complement);
        Assert.Equal("Centro", address.Neighborhood);
        Assert.Equal("Florianópolis", address.City);
        Assert.Equal("SC", address.State);
        Assert.Equal("88015700", address.PostalCode);
    }

    [Fact]
    public void Create_WithOptionalComplement_ReturnsNullComplement()
    {
        var address = RestaurantAddress.Create(
            street: "Avenida Beira-Mar Norte",
            number: "1000",
            complement: null,
            neighborhood: "Centro",
            city: "Florianópolis",
            state: "SC",
            postalCode: "88015-700"
        );

        Assert.Null(address.Complement);
    }

    [Fact]
    public void Create_WithWhitespaceComplement_ReturnsNullComplement()
    {
        var address = RestaurantAddress.Create(
            street: "Avenida Beira-Mar Norte",
            number: "1000",
            complement: "   ",
            neighborhood: "Centro",
            city: "Florianópolis",
            state: "SC",
            postalCode: "88015-700"
        );

        Assert.Null(address.Complement);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyStreet_ThrowsArgumentException(string street)
    {
        Assert.Throws<ArgumentException>(() =>
            RestaurantAddress.Create(
                street, "1000", null, "Centro", "Florianópolis", "SC", "88015-700"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyNumber_ThrowsArgumentException(string number)
    {
        Assert.Throws<ArgumentException>(() =>
            RestaurantAddress.Create(
                "Avenida Beira-Mar Norte", number, null,
                "Centro", "Florianópolis", "SC", "88015-700"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("S")]
    [InlineData("SCC")]
    [InlineData("1C")]
    public void Create_WithInvalidState_ThrowsArgumentException(string state)
    {
        Assert.Throws<ArgumentException>(() =>
            RestaurantAddress.Create(
                "Avenida Beira-Mar Norte", "1000", null,
                "Centro", "Florianópolis", state, "88015-700"));
    }

    [Theory]
    [InlineData("8801570")]
    [InlineData("880157001")]
    [InlineData("88015-70A")]
    [InlineData("ABCDEFGH")]
    public void Create_WithInvalidPostalCode_ThrowsArgumentException(string postalCode)
    {
        Assert.Throws<ArgumentException>(() =>
            RestaurantAddress.Create(
                "Avenida Beira-Mar Norte", "1000", null,
                "Centro", "Florianópolis", "SC", postalCode));
    }

    [Fact]
    public void Create_WithNullStreet_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RestaurantAddress.Create(
                null!, "1000", null,
                "Centro", "Florianópolis", "SC", "88015-700"));
    }
}
