using System.Security.Cryptography;
using System.Text;
using Delivery.Application.Restaurants.Commands;
using Delivery.Application.Restaurants.Results;
using Delivery.Application.Restaurants.Services;
using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.Enums;
using Delivery.Domain.Restaurants.ValueObjects;
using Delivery.Infrastructure.Persistence;
using Delivery.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Delivery.IntegrationTests.Restaurants;

[Collection(DatabaseCollection.Name)]
public sealed class AcceptRestaurantInvitationTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task ExecuteAsync_WithValidInvitation_PersistsAcceptedInvitationAndActiveMembership()
    {
        using var owner = await fixture.CreateAuthenticatedClientAsync();
        using var invitee = await fixture.CreateAuthenticatedClientAsync();

        var token = GenerateInvitationToken();

        var restaurantId = await SeedInvitationAsync(
            owner.UserId,
            invitee.Email,
            token);

        AcceptRestaurantInvitationResult result;

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<AcceptRestaurantInvitationService>();

            result = await service.ExecuteAsync(
                new AcceptRestaurantInvitationCommand(token),
                invitee.UserId,
                invitee.Email);
        }

        Assert.Equal(restaurantId, result.RestaurantId);
        Assert.Equal(RestaurantRole.Staff, result.Role);
        Assert.NotEqual(Guid.Empty, result.MembershipId);

        // Verify persistence using a fresh DbContext.
        await using var verificationScope =
            fixture.Services.CreateAsyncScope();

        var dbContext = verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var restaurant = await dbContext.Restaurants
            .Include(r => r.Invitations)
            .Include(r => r.Memberships)
            .SingleAsync(r => r.Id == restaurantId);

        var invitation = Assert.Single(restaurant.Invitations);
        var membership = Assert.Single(
            restaurant.Memberships,
            m => m.UserId == invitee.UserId);

        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal(invitee.UserId, invitation.AcceptedByUserId);
        Assert.NotNull(invitation.AcceptedAtUtc);

        Assert.Equal(result.MembershipId, membership.Id);
        Assert.Equal(invitee.UserId, membership.UserId);
        Assert.Equal(RestaurantRole.Staff, membership.Role);
        Assert.Equal(MembershipStatus.Active, membership.Status);
        Assert.NotNull(membership.ActivatedAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownToken_ThrowsAndDoesNotModifyDatabase()
    {
        using var invitee = await fixture.CreateAuthenticatedClientAsync();

        await using var scope = fixture.Services.CreateAsyncScope();

        var service = scope.ServiceProvider
            .GetRequiredService<AcceptRestaurantInvitationService>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteAsync(
                new AcceptRestaurantInvitationCommand("unknown-token"),
                invitee.UserId,
                invitee.Email));

        Assert.Equal("The invitation was not found.", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithMismatchedEmail_LeavesInvitationPending()
    {
        using var owner = await fixture.CreateAuthenticatedClientAsync();
        using var invitee = await fixture.CreateAuthenticatedClientAsync();

        var token = GenerateInvitationToken();

        var restaurantId = await SeedInvitationAsync(
            owner.UserId,
            invitee.Email,
            token);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider
                .GetRequiredService<AcceptRestaurantInvitationService>();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ExecuteAsync(
                    new AcceptRestaurantInvitationCommand(token),
                    invitee.UserId,
                    "different-email@example.com"));
        }

        // Verify failed acceptance did not persist partial changes.
        await using var verificationScope =
            fixture.Services.CreateAsyncScope();

        var dbContext = verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        var restaurant = await dbContext.Restaurants
            .Include(r => r.Invitations)
            .Include(r => r.Memberships)
            .SingleAsync(r => r.Id == restaurantId);

        var invitation = Assert.Single(restaurant.Invitations);

        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.Null(invitation.AcceptedByUserId);
        Assert.Null(invitation.AcceptedAtUtc);

        Assert.DoesNotContain(
            restaurant.Memberships,
            membership => membership.UserId == invitee.UserId);
    }

    private async Task<Guid> SeedInvitationAsync(
        Guid ownerUserId,
        string inviteeEmail,
        string token)
    {
        var now = DateTime.UtcNow;

        var restaurant = Restaurant.Create(
            "Integration Test Restaurant",
            Cnpj.Create(CreateValidCnpj()),
            RestaurantAddress.Create(
                street: "Main Street",
                number: "100",
                complement: null,
                neighborhood: "Downtown",
                city: "Florianopolis",
                state: "SC",
                postalCode: "88010000"),
            ownerUserId,
            now);

        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        restaurant.InviteByEmail(
            inviteeEmail,
            RestaurantRole.Staff,
            tokenHash,
            ownerUserId,
            now,
            now.AddDays(7));

        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        await dbContext.Restaurants.AddAsync(restaurant);
        await dbContext.SaveChangesAsync();

        return restaurant.Id;
    }

    private static string GenerateInvitationToken()
    {
        return Convert.ToHexString(
            RandomNumberGenerator.GetBytes(32));
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
}
