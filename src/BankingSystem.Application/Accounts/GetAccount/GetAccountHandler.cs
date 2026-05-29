using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Domain.Accounts;
using BankingSystem.Domain.Common;
using FluentValidation;

namespace BankingSystem.Application.Accounts.GetAccount;

public sealed record GetAccountQuery(Guid AccountId) : IQuery<Result<AccountResponse>>;

public sealed record AccountResponse(
    Guid AccountId,
    string OwnerName,
    decimal Balance,
    string Currency,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed class GetAccountQueryValidator : AbstractValidator<GetAccountQuery>
{
    public GetAccountQueryValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
    }
}

public sealed class GetAccountHandler(IAccountRepository accountRepository)
    : IQueryHandler<GetAccountQuery, Result<AccountResponse>>
{
    public async Task<Result<AccountResponse>> HandleAsync(
        GetAccountQuery query, CancellationToken ct = default)
    {
        var result = await accountRepository.GetByIdAsync(query.AccountId, ct);
        if (result.IsFailure)
            return result.Error;

        var account = result.Value;
        return new AccountResponse(
            account.Id,
            account.OwnerName,
            account.Balance,
            account.Currency.ToString(),
            account.Status.ToString(),
            account.CreatedAt,
            account.UpdatedAt);
    }
}
