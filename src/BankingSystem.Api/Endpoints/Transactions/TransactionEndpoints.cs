using BankingSystem.Api.Common;
using BankingSystem.Api.Dtos.Requests;
using BankingSystem.Application.Common.Dispatcher;
using BankingSystem.Application.Transactions.GetTransaction;
using BankingSystem.Application.Transactions.Transfer;
using Microsoft.AspNetCore.Mvc;

namespace BankingSystem.Api.Endpoints.Transactions;

public sealed class TransactionEndpoints : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/transactions")
            .WithTags("Transactions");

        group.MapPost("/transfer", InitiateTransferAsync)
            .WithName("InitiateTransfer")
            .WithSummary("Initiate a money transfer between accounts")
            .Produces<TransferResponse>(StatusCodes.Status202Accepted)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", GetTransactionAsync)
            .WithName("GetTransaction")
            .WithSummary("Get transaction details by ID")
            .Produces<TransactionResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> InitiateTransferAsync(
        [FromBody] TransferRequest request,
        IDispatcher dispatcher,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        var command = new TransferCommand(
            request.FromAccountId,
            request.ToAccountId,
            request.Amount,
            request.Currency,
            idempotencyKey);

        var result = await dispatcher.SendAsync(command, ct);

        return result.IsFailure
            ? HttpResults.Problem(result.Error)
            : Results.Accepted($"/api/transactions/{result.Value.TransactionId}", result.Value);
    }

    private static async Task<IResult> GetTransactionAsync(
        Guid id,
        IDispatcher dispatcher,
        CancellationToken ct)
    {
        var result = await dispatcher.QueryAsync(new GetTransactionQuery(id), ct);

        return result.IsFailure
            ? HttpResults.Problem(result.Error)
            : Results.Ok(result.Value);
    }
}
