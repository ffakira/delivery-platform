
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;
using Delivery.Infrastructure.Identity;
using Delivery.Infrastructure.Persistence;
using Delivery.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.IntegrationTests.Restaurants;

[Collection(DatabaseCollection.Name)]
public sealed class ListMyRestaurantsTests(DatabaseFixture fixture)
{
    private const string Password = "StrongPassword123!";

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
        using var authenticatedClient =
            await CreateAuthenticatedClientAsync();

        using var response = await authenticatedClient.Client.GetAsync(
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
        using var owner = await CreateAuthenticatedClientAsync();
        using var member = await CreateAuthenticatedClientAsync();
        using var outsider = await CreateAuthenticatedClientAsync();

        var firstRestaurantId = await CreateRestaurantAsync(
            owner.Client,
            "First Restaurant");

        var secondRestaurantId = await CreateRestaurantAsync(
            outsider.Client,
            "Second Restaurant");

        var thirdRestaurantId = await CreateRestaurantAsync(
            outsider.Client,
            "Third Restaurant");

        var ownerUserId = await GetUserIdAsync(owner.Email);
        var memberUserId = await GetUserIdAsync(member.Email);

        await AddMembershipAsync(
            secondRestaurantId,
            ownerUserId,
            RestaurantRole.Manager,
            MembershipStatus.Active);

        await AddMembershipAsync(
            thirdRestaurantId,
            ownerUserId,
            RestaurantRole.Staff,
            MembershipStatus.Suspended);

        await AddMembershipAsync(
            firstRestaurantId,
            memberUserId,
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

    private async Task<AuthenticatedClient> CreateAuthenticatedClientAsync()
    {
        var client = fixture.CreateClient();

        try
        {
            var email = $"user-{Guid.NewGuid():N}@example.com";

            var registrationToken = await GetCsrfTokenAsync(client);

            using var registrationRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/auth/register")
            {
                Content = JsonContent.Create(new
                {
                    email,
                    password = Password
                })
            };

            registrationRequest.Headers.Add(
                "X-CSRF-TOKEN",
                registrationToken);

            using var registrationResponse = await client.SendAsync(
                registrationRequest);

            Assert.Equal(
                HttpStatusCode.Created,
                registrationResponse.StatusCode);

            var loginToken = await GetCsrfTokenAsync(client);

            using var loginRequest = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/auth/login")
            {
                Content = JsonContent.Create(new
                {
                    email,
                    password = Password
                })
            };

            loginRequest.Headers.Add(
                "X-CSRF-TOKEN",
                loginToken);

            using var loginResponse = await client.SendAsync(loginRequest);

            Assert.Equal(
                HttpStatusCode.NoContent,
                loginResponse.StatusCode);

            return new AuthenticatedClient(client, email);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private async Task<Guid> CreateRestaurantAsync(
        HttpClient client,
        string name)
    {
        var csrfToken = await GetCsrfTokenAsync(client);

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

    private async Task<Guid> GetUserIdAsync(string email)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);

        return user.Id;
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

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync(
            "/api/auth/csrf-token");

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement
            .GetProperty("requestToken")
            .GetString()!;
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

    private sealed class AuthenticatedClient(
        HttpClient client,
        string email) : IDisposable
    {
        public HttpClient Client { get; } = client;

        public string Email { get; } = email;

        public void Dispose()
        {
            Client.Dispose();
        }
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
