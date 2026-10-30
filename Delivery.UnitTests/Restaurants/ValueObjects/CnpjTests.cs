
using Delivery.Domain.Restaurants.ValueObjects;

namespace Delivery.UnitTests.Restaurants.ValueObjects;

public sealed class CnpjTests
{
    [Theory]
    [InlineData("11222333000181")]
    [InlineData("11.222.333/0001-81")]
    public void Create_WithValidNumericCnpj_ReturnsNormalizedValue(string input)
    {
        var cnpj = Cnpj.Create(input);

        Assert.Equal("11222333000181", cnpj.Value);
    }

    [Fact]
    public void Create_WithValidAlphanumericCnpj_ReturnsNormalizedValue()
    {
        var cnpj = Cnpj.Create("12ABC34501DE35");

        Assert.Equal("12ABC34501DE35", cnpj.Value);
    }

    [Fact]
    public void Create_WithLowercaseAlphanumericCnpj_NormalizesToUppercase()
    {
        var cnpj = Cnpj.Create("12abc34501de35");

        Assert.Equal("12ABC34501DE35", cnpj.Value);
    }

    [Theory]
    [InlineData("11222333000180")]
    [InlineData("12ABC34501DE34")]
    public void TryCreate_WithInvalidCheckDigits_ReturnsFalse(string input)
    {
        var result = Cnpj.TryCreate(input, out var cnpj);

        Assert.False(result);
        Assert.Null(cnpj);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1122233300018")]
    [InlineData("112223330001811")]
    [InlineData("11222333000A81")]
    [InlineData("12ABC34501D@35")]
    public void TryCreate_WithInvalidInput_ReturnsFalse(string input)
    {
        var result = Cnpj.TryCreate(input, out var cnpj);

        Assert.False(result);
        Assert.Null(cnpj);
    }

    [Fact]
    public void TryCreate_WithNullInput_ReturnsFalse()
    {
        var result = Cnpj.TryCreate(null, out var cnpj);

        Assert.False(result);
        Assert.Null(cnpj);
    }

    [Fact]
    public void Create_WithInvalidCnpj_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => Cnpj.Create("11222333000180")
        );
    }

    [Fact]
    public void TwoCnpjInstances_WithSameValue_AreEqual()
    {
        var first = Cnpj.Create("11222333000181");
        var second = Cnpj.Create("11.222.333/0001-81");

        Assert.Equal(first, second);
    }
}
