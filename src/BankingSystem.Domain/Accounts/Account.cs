using BankingSystem.Domain.Common;

namespace BankingSystem.Domain.Accounts;

public sealed class Account : Entity
{
    private Account() { }

    private Account(
        Guid id,
        string ownerName,
        Currency currency,
        decimal initialBalance)
    {
        Id = id;
        OwnerName = ownerName;
        Currency = currency;
        Balance = initialBalance;
        Status = AccountStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new AccountCreatedEvent(Id, OwnerName, Currency, Balance));
    }

    public string OwnerName { get; private set; } = default!;
    public decimal Balance { get; private set; }
    public Currency Currency { get; private set; }
    public AccountStatus Status { get; private set; }

    public uint RowVersion { get; private set; }

    public static Result<Account> Create(
        string ownerName,
        Currency currency,
        decimal initialBalance = 0m)
    {
        if (string.IsNullOrWhiteSpace(ownerName))
            return Error.Validation("Account.InvalidOwner", "OwnerName cannot be empty.");

        if (initialBalance < 0)
            return Error.Validation("Account.NegativeBalance", "Initial balance cannot be negative.");

        return new Account(Guid.NewGuid(), ownerName, currency, initialBalance);
    }

    public Result Debit(decimal amount, Guid transactionId)
    {
        if (Status != AccountStatus.Active)
            return Error.Business("Account.NotActive", $"Account {Id} is not active.");

        if (amount <= 0)
            return Error.Validation("Account.InvalidAmount", "Debit amount must be positive.");

        if (Balance < amount)
            return Error.Business("Account.InsufficientFunds",
                $"Insufficient funds. Available: {Balance}, Requested: {amount}.");

        Balance -= amount;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new AccountDebitedEvent(Id, transactionId, amount, Balance));
        return Result.Success();
    }

    public Result Credit(decimal amount, Guid transactionId)
    {
        if (Status != AccountStatus.Active)
            return Error.Business("Account.NotActive", $"Account {Id} is not active.");

        if (amount <= 0)
            return Error.Validation("Account.InvalidAmount", "Credit amount must be positive.");

        Balance += amount;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new AccountCreditedEvent(Id, transactionId, amount, Balance));
        return Result.Success();
    }

    public Result Suspend()
    {
        if (Status == AccountStatus.Closed)
            return Error.Business("Account.AlreadyClosed", "Cannot suspend a closed account.");

        Status = AccountStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Close()
    {
        if (Balance != 0)
            return Error.Business("Account.NonZeroBalance", "Cannot close account with non-zero balance.");

        Status = AccountStatus.Closed;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }
}
