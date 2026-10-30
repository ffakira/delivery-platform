using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Delivery.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.IntegrationTests.Infrastructure;

public static class DatabaseFixtureExtensions
{
    private const string Password = "StrongPassword123!";

    public static async Task<AuthenticatedTestClient> CreateAuthenticatedClientAsync(
        this DatabaseFixture fixture,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        var client = fixture.CreateClient();

        try
        {
            var email = $"user-{Guid.NewGuid():N}@example.com";

            var registrationToken = await client.GetCsrfTokenAsync(
                cancellationToken);

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
                registrationRequest,
                cancellationToken);

            if (registrationResponse.StatusCode != HttpStatusCode.Created)
            {
                throw new InvalidOperationException(
                    $"User registration failed. Expected " +
                    $"{(int)HttpStatusCode.Created} Created, received " +
                    $"{(int)registrationResponse.StatusCode} " +
                    $"{registrationResponse.StatusCode}. " +
                    $"Response: {await registrationResponse.Content.ReadAsStringAsync(cancellationToken)}");
            }

            var loginToken = await client.GetCsrfTokenAsync(
                cancellationToken);

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

            using var loginResponse = await client.SendAsync(
                loginRequest,
                cancellationToken);

            if (loginResponse.StatusCode != HttpStatusCode.NoContent)
            {
                throw new InvalidOperationException(
                    $"User login failed. Expected " +
                    $"{(int)HttpStatusCode.NoContent} No Content, received " +
                    $"{(int)loginResponse.StatusCode} " +
                    $"{loginResponse.StatusCode}. " +
                    $"Response: {await loginResponse.Content.ReadAsStringAsync(cancellationToken)}");
            }

            await using var scope = fixture.Services.CreateAsyncScope();

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            var user = await userManager.FindByEmailAsync(email);

            if (user is null)
            {
                throw new InvalidOperationException(
                    "The registered user could not be found.");
            }

            return new AuthenticatedTestClient(
                client,
                user.Id,
                email);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public static async Task<string> GetCsrfTokenAsync(
        this HttpClient client,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        using var response = await client.GetAsync(
            "/api/auth/csrf-token",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(
            cancellationToken);

        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken);

        return document.RootElement
            .GetProperty("requestToken")
            .GetString()
            ?? throw new InvalidOperationException(
                "The CSRF token response did not contain a request token.");
    }
}

public sealed class AuthenticatedTestClient(
    HttpClient client,
    Guid userId,
    string email) : IDisposable
{
    public HttpClient Client { get; } = client;

    public Guid UserId { get; } = userId;

    public string Email { get; } = email;

    public void Dispose()
    {
        Client.Dispose();
    }
}
