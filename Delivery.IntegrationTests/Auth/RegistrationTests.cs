using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Delivery.IntegrationTests.Infrastructure;

namespace Delivery.IntegrationTests.Auth;

[Collection(DatabaseCollection.Name)]
public sealed class RegistrationTests(DatabaseFixture fixture)
{
    private const string Password = "StringPassword123!";

    [Fact]
    public async Task Register_WithValidData_CreatesUser()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var csrfToken = await GetCsrfTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
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

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsBadRequest()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var csrfToken = await GetCsrfTokenAsync();

        var firstResponse = await RegisterAsync(email, csrfToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        firstResponse.Dispose();

        using var secondResponse = await RegisterAsync(email, csrfToken);
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
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
            .GetString()!;
    }

    private Task<HttpResponseMessage> RegisterAsync(string email, string csrfToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new
            {
                email,
                password = Password
            })
        };

        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return fixture.Client.SendAsync(request);
    }
}