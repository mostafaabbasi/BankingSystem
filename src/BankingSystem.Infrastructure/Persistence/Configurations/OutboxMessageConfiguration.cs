using BankingSystem.Domain.Outbox;
using BankingSystem.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankingSystem.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .ValueGeneratedNever();

        builder.Property(o => o.Type)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(o => o.Payload)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(o => o.CorrelationId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(o => o.OccurredAt)
            .IsRequired();

        builder.Property(o => o.ProcessedAt);

        builder.Property(o => o.Error)
            .HasMaxLength(2000);

        builder.Property(o => o.RetryCount)
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(o => new { o.Status, o.OccurredAt })
            .HasDatabaseName("ix_outbox_messages_status_occurred_at");
    }
}
