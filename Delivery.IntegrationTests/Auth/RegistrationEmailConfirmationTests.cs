using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Delivery.Infrastructure.Identity;
using Delivery.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.IntegrationTests.Auth;

[Collection(DatabaseCollection.Name)]
public sealed class RegistrationEmailConfirmationTests(
    DatabaseFixture fixture)
{
    private const string Password = "StrongPassword123!";

    [Fact]
    public async Task Register_WithValidDetails_SendsConfirmationEmail()
    {
        fixture.EmailSender.Clear();

        using var client = fixture.CreateClient();

        var email = CreateUniqueEmail();

        using var response = await RegisterAsync(client, email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var message = Assert.Single(fixture.EmailSender.Messages);

        Assert.Equal(email, message.To);
        Assert.Equal(
            "Confirm your Delivery Platform account",
            message.Subject);

        Assert.Contains("Welcome to Delivery Platform!", message.TextBody);
        Assert.NotNull(message.HtmlBody);
        Assert.Contains("Confirm email address", message.HtmlBody);

        var confirmationUrl = ExtractConfirmationUrl(message.TextBody);

        Assert.Equal(
            "/api/auth/confirm-email",
            confirmationUrl.AbsolutePath);

        Assert.True(
            QueryHelpers.ParseQuery(confirmationUrl.Query)
                .ContainsKey("userId"));

        Assert.True(
            QueryHelpers.ParseQuery(confirmationUrl.Query)
                .ContainsKey("token"));
    }

    [Fact]
    public async Task ConfirmationLink_GetRequest_DoesNotConfirmEmail()
    {
        fixture.EmailSender.Clear();

        using var client = fixture.CreateClient();

        var email = CreateUniqueEmail();

        using var registrationResponse = await RegisterAsync(
            client,
            email);

        Assert.Equal(
            HttpStatusCode.Created,
            registrationResponse.StatusCode);

        var message = Assert.Single(fixture.EmailSender.Messages);
        var confirmationUrl = ExtractConfirmationUrl(message.TextBody);

        using var response = await client.GetAsync(confirmationUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Confirm your email address", html);

        var user = await FindUserAsync(email);

        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmEmail_WithValidToken_ConfirmsUserEmail()
    {
        fixture.EmailSender.Clear();

        using var client = fixture.CreateClient();

        var email = CreateUniqueEmail();

        using var registrationResponse = await RegisterAsync(
            client,
            email);

        Assert.Equal(
            HttpStatusCode.Created,
            registrationResponse.StatusCode);

        var message = Assert.Single(fixture.EmailSender.Messages);
        var confirmationUrl = ExtractConfirmationUrl(message.TextBody);

        using var response = await SubmitConfirmationAsync(
            client,
            confirmationUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await FindUserAsync(email);

        Assert.NotNull(user);
        Assert.True(user.EmailConfirmed);
    }

    [Fact]
    public async Task ConfirmEmail_WithInvalidToken_ReturnsBadRequest()
    {
        fixture.EmailSender.Clear();

        using var client = fixture.CreateClient();

        var email = CreateUniqueEmail();

        using var registrationResponse = await RegisterAsync(
            client,
            email);

        Assert.Equal(
            HttpStatusCode.Created,
            registrationResponse.StatusCode);

        var message = Assert.Single(fixture.EmailSender.Messages);
        var confirmationUrl = ExtractConfirmationUrl(message.TextBody);

        var query = QueryHelpers.ParseQuery(confirmationUrl.Query);

        var invalidConfirmationUrl = new Uri(
            QueryHelpers.AddQueryString(
                confirmationUrl.GetLeftPart(UriPartial.Path),
                new Dictionary<string, string?>
                {
                    ["userId"] = query["userId"].ToString(),
                    ["token"] = "invalid-token"
                }));

        using var response = await SubmitConfirmationAsync(
            client,
            invalidConfirmationUrl);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var user = await FindUserAsync(email);

        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task Register_WhenEmailDeliveryFails_StillCreatesUser()
    {
        fixture.EmailSender.Clear();
        fixture.EmailSender.ShouldFail = true;

        try
        {
            using var client = fixture.CreateClient();

            var email = CreateUniqueEmail();

            using var response = await RegisterAsync(client, email);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var user = await FindUserAsync(email);

            Assert.NotNull(user);
            Assert.False(user.EmailConfirmed);
        }
        finally
        {
            fixture.EmailSender.ShouldFail = false;
        }
    }

    private async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email)
    {
        var csrfToken = await client.GetCsrfTokenAsync();

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

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SubmitConfirmationAsync(
        HttpClient client,
        Uri confirmationUrl)
    {
        var query = QueryHelpers.ParseQuery(confirmationUrl.Query);

        var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["userId"] = query["userId"].ToString(),
                ["token"] = query["token"].ToString()
            });

        return await client.PostAsync(
            "/api/auth/confirm-email",
            content);
    }

    private async Task<ApplicationUser?> FindUserAsync(string email)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        return await userManager.FindByEmailAsync(email);
    }

    private static Uri ExtractConfirmationUrl(string textBody)
    {
        var url = textBody
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line =>
                Uri.TryCreate(line, UriKind.Absolute, out _));

        return Uri.TryCreate(url, UriKind.Absolute, out var result)
            ? result
            : throw new InvalidOperationException(
                "The confirmation email did not contain a valid URL.");
    }

    private static string CreateUniqueEmail()
    {
        return $"registration-{Guid.NewGuid():N}@example.com";
    }
}
