using Delivery.Domain.Restaurants.Entities;
using Delivery.Domain.Restaurants.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations.Restaurants;

public sealed class RestaurantConfiguration : IEntityTypeConfiguration<Restaurant>
{
    public void Configure(EntityTypeBuilder<Restaurant> builder)
    {
        builder.ToTable("Restaurants");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.Name)
            .IsRequired();

        builder.Property(r => r.Cnpj)
            .HasConversion(
                cnpj => cnpj.Value,
                value => Cnpj.Create(value))
            .HasMaxLength(14)
            .IsRequired();

        builder.HasIndex(r => r.Cnpj)
            .IsUnique()
            .HasDatabaseName("UX_Restaurants_Cnpj");
        
        builder.Property(r => r.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.OwnsOne(r => r.Address, address =>
        {
            address.Property(v => v.Street)
                .HasColumnName("Street")
                .IsRequired();
            
            address.Property(v => v.Number)
                .HasColumnName("Number")
                .IsRequired();

            address.Property(v => v.Complement)
                .HasColumnName("Complement");
            
            address.Property(v => v.Neighborhood)
                .HasColumnName("Neighborhood")
                .IsRequired();
            
            address.Property(v => v.City)
                .HasColumnName("City")
                .IsRequired();
            
            address.Property(v => v.State)
                .HasColumnName("State")
                .IsRequired();
            
            address.Property(v => v.PostalCode)
                .HasColumnName("PostalCode")
                .IsRequired();
        });

        builder.Navigation(r => r.Address)
            .IsRequired();
        
        builder.HasMany(r => r.Memberships)
            .WithOne()
            .HasForeignKey(m => m.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Memberships)
            .HasField("_memberships")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        
        builder.HasMany(r => r.Invitations)
            .WithOne()
            .HasForeignKey(i => i.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Invitations)
            .HasField("_invitations")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
