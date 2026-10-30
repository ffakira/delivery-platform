using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Delivery.Domain.Restaurants.Enums;
using Delivery.Infrastructure.Persistence;
using Delivery.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.IntegrationTests.Restaurants;

[Collection(DatabaseCollection.Name)]
public sealed class ListMyRestaurantsTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task ListMyRestaurants_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/api/restaurants/");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task ListMyRestaurants_WithNoMemberships_ReturnsEmptyCollection()
    {
        using var user = await fixture.CreateAuthenticatedClientAsync();

        using var response = await user.Client.GetAsync(
            "/api/restaurants/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var restaurants = await response.Content
            .ReadFromJsonAsync<List<RestaurantResponse>>();

        Assert.NotNull(restaurants);
        Assert.Empty(restaurants);
    }

    [Fact]
    public async Task ListMyRestaurants_WithMultipleMemberships_ReturnsOnlyActiveRestaurantsForCurrentUser()
    {
        using var owner = await fixture.CreateAuthenticatedClientAsync();
        using var member = await fixture.CreateAuthenticatedClientAsync();
        using var outsider = await fixture.CreateAuthenticatedClientAsync();

        var firstRestaurantId = await CreateRestaurantAsync(
            owner.Client,
            "First Restaurant");

        var secondRestaurantId = await CreateRestaurantAsync(
            outsider.Client,
            "Second Restaurant");

        var thirdRestaurantId = await CreateRestaurantAsync(
            outsider.Client,
            "Third Restaurant");

        await AddMembershipAsync(
            secondRestaurantId,
            owner.UserId,
            RestaurantRole.Manager,
            MembershipStatus.Active);

        await AddMembershipAsync(
            thirdRestaurantId,
            owner.UserId,
            RestaurantRole.Staff,
            MembershipStatus.Suspended);

        await AddMembershipAsync(
            firstRestaurantId,
            member.UserId,
            RestaurantRole.Staff,
            MembershipStatus.Invited);

        using var response = await owner.Client.GetAsync(
            "/api/restaurants/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var restaurants = await response.Content
            .ReadFromJsonAsync<List<RestaurantResponse>>();

        Assert.NotNull(restaurants);
        Assert.Equal(2, restaurants.Count);

        Assert.Contains(
            restaurants,
            restaurant => restaurant.Id == firstRestaurantId);

        Assert.Contains(
            restaurants,
            restaurant => restaurant.Id == secondRestaurantId);

        Assert.DoesNotContain(
            restaurants,
            restaurant => restaurant.Id == thirdRestaurantId);

        var secondRestaurant = Assert.Single(
            restaurants,
            restaurant => restaurant.Id == secondRestaurantId);

        Assert.Equal(RestaurantRole.Manager, secondRestaurant.Role);
    }

    private async Task<Guid> CreateRestaurantAsync(
        HttpClient client,
        string name)
    {
        var csrfToken = await client.GetCsrfTokenAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/restaurants/")
        {
            Content = JsonContent.Create(new
            {
                name,
                cnpj = CreateValidCnpj(),
                street = "Main Street",
                number = "100",
                complement = (string?)null,
                neighborhood = "Downtown",
                city = "Florianópolis",
                state = "SC",
                postalCode = "88010000"
            })
        };

        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created, received " +
            $"{(int)response.StatusCode} {response.StatusCode}.\n" +
            $"Response body: {body}");

        var result = JsonSerializer.Deserialize<CreateRestaurantResponse>(
            body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(result);

        return result.Id;
    }

    private async Task AddMembershipAsync(
        Guid restaurantId,
        Guid userId,
        RestaurantRole role,
        MembershipStatus status)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var restaurant = await dbContext.Restaurants
            .Include(item => item.Memberships)
            .SingleAsync(item => item.Id == restaurantId);

        var membership = restaurant.InviteMember(
            userId,
            role,
            DateTime.UtcNow);

        if (status == MembershipStatus.Active)
        {
            membership.Activate(DateTime.UtcNow);
        }
        else if (status == MembershipStatus.Suspended)
        {
            membership.Activate(DateTime.UtcNow);
            membership.Suspend();
        }

        await dbContext.SaveChangesAsync();
    }

    private static string CreateValidCnpj()
    {
        var baseNumber =
            $"123{RandomNumberGenerator.GetInt32(0, 1_000_000_000):D9}";

        var firstDigit = CalculateCnpjCheckDigit(baseNumber);
        var secondDigit = CalculateCnpjCheckDigit(
            baseNumber + firstDigit);

        return $"{baseNumber}{firstDigit}{secondDigit}";
    }

    private static int CalculateCnpjCheckDigit(string value)
    {
        var sum = 0;
        var weight = 2;

        for (var i = value.Length - 1; i >= 0; i--)
        {
            sum += (value[i] - '0') * weight;
            weight = weight == 9 ? 2 : weight + 1;
        }

        var remainder = sum % 11;

        return remainder < 2 ? 0 : 11 - remainder;
    }

    private sealed record CreateRestaurantResponse(Guid Id);

    private sealed record RestaurantResponse(
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
}
