using Delivery.Domain.Restaurants.Entities;
using Delivery.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations.Restaurants;

public sealed class RestaurantMembershipConfiguration : IEntityTypeConfiguration<RestaurantMembership>
{
    public void Configure(EntityTypeBuilder<RestaurantMembership> builder)
    {
        builder.ToTable("RestaurantMemberships");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.Property(m => m.RestaurantId)
            .IsRequired();
        
        builder.Property(m => m.UserId)
            .IsRequired();

        builder.Property(m => m.Role)
            .HasConversion<int>()
            .IsRequired();
        
        builder.Property(m => m.Status)
            .HasConversion<int>()
            .IsRequired();
        
        builder.Property(m => m.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(m => new
            {
                m.RestaurantId,
                m.UserId
            })
            .IsUnique()
            .HasDatabaseName("UX_RestaurantMemberships_RestaurantId_UserId");

        builder.HasIndex(m => m.RestaurantId)
            .IsUnique()
            .HasFilter("\"Role\" = 1")
            .HasDatabaseName("UX_RestaurantMemberships_OneOwner");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
