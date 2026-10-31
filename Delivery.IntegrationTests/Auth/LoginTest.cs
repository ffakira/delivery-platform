using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Delivery.Infrastructure.Identity;
using Delivery.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.IntegrationTests.Auth;

[Collection(DatabaseCollection.Name)]
public sealed class LoginTest(DatabaseFixture fixture)
{
    private const string Password = "StrongPassword123!";

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsNoContentAndSetsAuthCookie()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";

        await RegisterUserAsync(email);
        await ConfirmUserEmailAsync(email);

        var csrfToken = await GetCsrfTokenAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email,
                password = Password
            })
        };

        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        using var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith("__Host-Delivery.Auth="));
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";

        await RegisterUserAsync(email);
        await ConfirmUserEmailAsync(email);

        var csrfToken = await GetCsrfTokenAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email,
                password = "WrongPassword123!"
            })
        };

        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        using var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task RegisterUserAsync(string email)
    {
        var csrfToken = await GetCsrfTokenAsync();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/register")
        {
            Content = JsonContent.Create(new
            {
                email,
                password = Password
            })
        };

        request.Headers.Add("X-CSRF-TOKEN", csrfToken);

        using var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task ConfirmUserEmailAsync(string email)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);

        var confirmationToken =
            await userManager.GenerateEmailConfirmationTokenAsync(user);

        var result = await userManager.ConfirmEmailAsync(
            user,
            confirmationToken);

        Assert.True(
            result.Succeeded,
            string.Join(
                ", ",
                result.Errors.Select(error => error.Description)));
    }

    private async Task<string> GetCsrfTokenAsync()
    {
        using var response = await fixture.Client.GetAsync(
            "/api/auth/csrf-token");

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement
            .GetProperty("requestToken")
            .GetString()
            ?? throw new InvalidOperationException(
                "The CSRF token response did not contain a request token.");
    }
}
