using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using FluentValidation;

namespace BankingSystem.Application.Accounts.CreateAccount;

public sealed record CreateAccountCommand(
    string OwnerName,
    string Currency,
    decimal InitialBalance) : ICommand<Result<CreateAccountResponse>>;

public sealed record CreateAccountResponse(
    Guid AccountId,
    string OwnerName,
    decimal Balance,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt);

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    private static readonly HashSet<string> SupportedCurrencies = ["USD", "EUR", "GBP"];

    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.OwnerName)
            .NotEmpty().WithMessage("OwnerName is required.")
            .MaximumLength(100);

        RuleFor(x => x.Currency)
            .NotEmpty()
            .Must(c => SupportedCurrencies.Contains(c.ToUpperInvariant()))
            .WithMessage($"Currency must be one of: {string.Join(", ", SupportedCurrencies)}.");

        RuleFor(x => x.InitialBalance)
            .GreaterThanOrEqualTo(0).WithMessage("Initial balance cannot be negative.");
    }
}

public sealed class CreateAccountHandler(
    IAccountRepository accountRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateAccountCommand, Result<CreateAccountResponse>>
{
    public async Task<Result<CreateAccountResponse>> HandleAsync(
        CreateAccountCommand command, CancellationToken ct = default)
    {
        if (!Enum.TryParse<Currency>(command.Currency, ignoreCase: true, out var currency))
            return Error.Validation("Account.InvalidCurrency", $"Unsupported currency: {command.Currency}.");

        var accountResult = Account.Create(command.OwnerName, currency, command.InitialBalance);
        if (accountResult.IsFailure)
            return accountResult.Error;

        var account = accountResult.Value;
        await accountRepository.AddAsync(account, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new CreateAccountResponse(
            account.Id,
            account.OwnerName,
            account.Balance,
            account.Currency.ToString(),
            account.Status.ToString(),
            account.CreatedAt);
    }
}
