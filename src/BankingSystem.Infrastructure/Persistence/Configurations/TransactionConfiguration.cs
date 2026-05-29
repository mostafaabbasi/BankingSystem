using BankingSystem.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankingSystem.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.FromAccountId)
            .IsRequired();

        builder.Property(t => t.ToAccountId)
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(t => t.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.IdempotencyKey)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.CorrelationId)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(t => t.FailureReason)
            .HasMaxLength(1000);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(t => t.UpdatedAt);

        builder.Property(t => t.CompletedAt);

        builder.HasIndex(t => t.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ix_transactions_idempotency_key");

        builder.HasIndex(t => t.CorrelationId)
            .HasDatabaseName("ix_transactions_correlation_id");

        builder.HasIndex(t => t.FromAccountId)
            .HasDatabaseName("ix_transactions_from_account_id");

        builder.HasIndex(t => t.ToAccountId)
            .HasDatabaseName("ix_transactions_to_account_id");

        builder.Ignore(t => t.DomainEvents);
    }
}