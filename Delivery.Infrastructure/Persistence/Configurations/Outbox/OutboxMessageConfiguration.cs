using Delivery.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Delivery.Infrastructure.Persistence.Configurations.Outbox;

public sealed class OutboxMessageConfiguration
    : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(om => om.Id);

        builder.Property(om => om.Type)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(om => om.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(om => om.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(om => om.AttemptCount)
            .IsRequired();

        builder.Property(om => om.CreatedAtUtc)
            .IsRequired();

        builder.Property(om => om.LockToken);

        builder.Property(om => om.LastError)
            .HasMaxLength(2000);

        builder.HasIndex(om => new
            {
                om.Status,
                om.NextAttemptAtUtc,
                om.CreatedAtUtc
            })
            .HasDatabaseName("IX_OutboxMessages_Processing");
    }
}
