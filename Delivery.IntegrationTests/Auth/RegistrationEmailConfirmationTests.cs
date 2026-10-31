
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Delivery.Infrastructure.Identity;
using Delivery.Infrastructure.Persistence;
using Delivery.Infrastructure.Persistence.Outbox;
using Delivery.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.IntegrationTests.Auth;

[Collection(DatabaseCollection.Name)]
public sealed class RegistrationEmailConfirmationTests(
    DatabaseFixture fixture)
{
    private const string Password = "StrongPassword123!";

    [Fact]
    public async Task Register_WithValidDetails_PersistsPendingOutboxMessage()
    {
        fixture.EmailSender.Clear();

        using var client = fixture.CreateClient();

        var email = CreateUniqueEmail();

        using var response = await RegisterAsync(client, email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var user = await FindUserAsync(email);

        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);

        var message = await FindOutboxMessageAsync(user.Id);

        Assert.NotNull(message);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(0, message.AttemptCount);

        // Registration must not send the email synchronously.
        Assert.Empty(fixture.EmailSender.Messages);
    }

    [Fact]
    public async Task ConfirmationLink_GetRequest_DoesNotConfirmEmail()
    {
        using var client = fixture.CreateClient();

        var email = CreateUniqueEmail();

        using var registrationResponse = await RegisterAsync(
            client,
            email);

        Assert.Equal(
            HttpStatusCode.Created,
            registrationResponse.StatusCode);

        var confirmationUrl = await CreateConfirmationUrlAsync(email);

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
        using var client = fixture.CreateClient();

        var email = CreateUniqueEmail();

        using var registrationResponse = await RegisterAsync(
            client,
            email);

        Assert.Equal(
            HttpStatusCode.Created,
            registrationResponse.StatusCode);

        var confirmationUrl = await CreateConfirmationUrlAsync(email);

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
        using var client = fixture.CreateClient();

        var email = CreateUniqueEmail();

        using var registrationResponse = await RegisterAsync(
            client,
            email);

        Assert.Equal(
            HttpStatusCode.Created,
            registrationResponse.StatusCode);

        var confirmationUrl = await CreateConfirmationUrlAsync(email);

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
    public async Task Register_WhenEmailDeliveryIsUnavailable_StillQueuesConfirmationMessage()
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

            var message = await FindOutboxMessageAsync(user.Id);

            Assert.NotNull(message);
            Assert.Equal(OutboxMessageStatus.Pending, message.Status);

            // The registration request does not attempt SMTP delivery.
            Assert.Empty(fixture.EmailSender.Messages);
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

    private async Task<OutboxMessage?> FindOutboxMessageAsync(Guid userId)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var messages = await dbContext.OutboxMessages
            .AsNoTracking()
            .ToListAsync();

        return messages.SingleOrDefault(message =>
        {
            using var document = JsonDocument.Parse(message.Payload);

            return document.RootElement.TryGetProperty(
                "userId",
                out var property)
                && property.GetGuid() == userId;
        });
    }

    private async Task<Uri> CreateConfirmationUrlAsync(string email)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);

        var token = await userManager.GenerateEmailConfirmationTokenAsync(
            user);

        var encodedToken = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(token));

        var query = QueryHelpers.AddQueryString(
            "/api/auth/confirm-email",
            new Dictionary<string, string?>
            {
                ["userId"] = user.Id.ToString(),
                ["token"] = encodedToken
            });

        return new Uri(
            fixture.CreateClient().BaseAddress!,
            query);
    }

    private static string CreateUniqueEmail()
    {
        return $"registration-{Guid.NewGuid():N}@example.com";
    }
}
