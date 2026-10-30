using Delivery.Domain.Restaurants.Entities;
using Delivery.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations.Restaurants;

public sealed class RestaurantInvitationConfiguration
    : IEntityTypeConfiguration<RestaurantInvitation>
{
    public void Configure(EntityTypeBuilder<RestaurantInvitation> builder)
    {
        builder.ToTable("RestaurantInvitations");

        builder.HasKey(ri => ri.Id);

        builder.Property(ri => ri.Id)
            .ValueGeneratedNever();

        builder.Property(ri => ri.RestaurantId)
            .IsRequired();

        builder.Property(ri => ri.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(ri => ri.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(ri => ri.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(ri => ri.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(ri => ri.InvitedByUserId)
            .IsRequired();

        builder.Property(ri => ri.AcceptedByUserId)
            .IsRequired(false);

        builder.Property(ri => ri.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(ri => ri.ExpiresAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(ri => ri.AcceptedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.Property(ri => ri.RevokedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        // An invitation token hash must never be duplicated.
        builder.HasIndex(ri => ri.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_RestaurantInvitations_TokenHash");

        // Only one pending invitation per restaurant and email.
        builder.HasIndex(ri => new
            {
                ri.RestaurantId,
                ri.Email
            })
            .IsUnique()
            .HasFilter("\"Status\" = 1")
            .HasDatabaseName(
                "UX_RestaurantInvitations_Pending_RestaurantId_Email");

        builder.HasIndex(ri => ri.RestaurantId)
            .HasDatabaseName("IX_RestaurantInvitations_RestaurantId");

        // The user who created the invitation.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(ri => ri.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // The user who accepted the invitation.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(ri => ri.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
