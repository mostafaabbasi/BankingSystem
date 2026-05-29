using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Domain.Common;
using BankingSystem.Domain.Transactions;
using FluentValidation;

namespace BankingSystem.Application.Transactions.GetTransaction;

public sealed record GetTransactionQuery(Guid TransactionId) : IQuery<Result<TransactionResponse>>;

public sealed record TransactionResponse(
    Guid TransactionId,
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string Currency,
    string Status,
    string CorrelationId,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);

public sealed class GetTransactionQueryValidator : AbstractValidator<GetTransactionQuery>
{
    public GetTransactionQueryValidator()
    {
        RuleFor(x => x.TransactionId).NotEmpty();
    }
}

public sealed class GetTransactionHandler(ITransactionRepository transactionRepository)
    : IQueryHandler<GetTransactionQuery, Result<TransactionResponse>>
{
    public async Task<Result<TransactionResponse>> HandleAsync(
        GetTransactionQuery query, CancellationToken ct = default)
    {
        var result = await transactionRepository.GetByIdAsync(query.TransactionId, ct);
        if (result.IsFailure)
            return result.Error;

        var tx = result.Value;
        return new TransactionResponse(
            tx.Id,
            tx.FromAccountId,
            tx.ToAccountId,
            tx.Amount,
            tx.Currency,
            tx.Status.ToString(),
            tx.CorrelationId,
            tx.FailureReason,
            tx.CreatedAt,
            tx.CompletedAt);
    }
}
