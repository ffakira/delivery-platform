
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;
using Delivery.Infrastructure.Persistence;
using Delivery.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.IntegrationTests.Restaurants;

[Collection(DatabaseCollection.Name)]
public sealed class CreateRestaurantTests(DatabaseFixture fixture)
{
    private const string Password = "StrongPassword123!";

    [Fact]
    public async Task CreateRestaurant_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/restaurants/",
            CreateRequest());

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateRestaurant_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        using var client = await CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync(
            "/api/restaurants/",
            CreateRequest());

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateRestaurant_WithInvalidCnpj_ReturnsBadRequest()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var csrfToken = await GetCsrfTokenAsync(client);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/restaurants/")
        {
            Content = JsonContent.Create(
                CreateRequest(cnpj: "11222333000180"))
        };

        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateRestaurant_WithValidData_CreatesRestaurantAndOwnerMembership()
    {
        using var client = await CreateAuthenticatedClientAsync();

        var csrfToken = await GetCsrfTokenAsync(client);
        var cnpj = CreateValidCnpj();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/restaurants/")
        {
            Content = JsonContent.Create(CreateRequest(cnpj))
        };

        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        using var response = await client.SendAsync(request);

        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created, received " +
            $"{(int)response.StatusCode} {response.StatusCode}.\n" +
            $"Response body: {responseBody}");

        var result = JsonSerializer.Deserialize<CreateRestaurantResponse>(
            responseBody,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);

        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var restaurant = await dbContext.Set<Restaurant>()
            .Include(restaurant => restaurant.Memberships)
            .SingleOrDefaultAsync(restaurant => restaurant.Id == result.Id);

        Assert.NotNull(restaurant);
        Assert.Equal("Sample Restaurant", restaurant.Name);
        Assert.Equal(cnpj, restaurant.Cnpj.Value);

        var membership = Assert.Single(restaurant.Memberships);

        Assert.Equal(RestaurantRole.Owner, membership.Role);
        Assert.Equal(MembershipStatus.Active, membership.Status);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
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

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
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

    private static object CreateRequest(
        string cnpj = "11222333000181")
    {
        return new
        {
            name = "Sample Restaurant",
            cnpj,
            street = "Main Street",
            number = "100",
            complement = (string?)null,
            neighborhood = "Downtown",
            city = "Florianopolis",
            state = "SC",
            postalCode = "88010000"
        };
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
}
