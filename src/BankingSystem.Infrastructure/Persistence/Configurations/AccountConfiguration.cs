using BankingSystem.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BankingSystem.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.OwnerName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Balance)
            .HasPrecision(18, 4)
            .IsRequired();

        builder.Property(a => a.Currency)
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.UpdatedAt);

        builder.Property(a => a.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate()
            .IsRequired();

        builder.HasIndex(a => a.OwnerName).HasDatabaseName("ix_accounts_owner_name");

        builder.Ignore(a => a.DomainEvents);
    }
}
