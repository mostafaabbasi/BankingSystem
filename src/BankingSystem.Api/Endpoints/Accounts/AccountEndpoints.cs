using BankingSystem.Api.Common;
using BankingSystem.Api.Dtos.Requests;
using BankingSystem.Application.Accounts.CreateAccount;
using BankingSystem.Application.Accounts.GetAccount;
using BankingSystem.Application.Common.Dispatcher;
using Microsoft.AspNetCore.Mvc;

namespace BankingSystem.Api.Endpoints.Accounts;

public sealed class AccountEndpoints : IEndpoint
{
    public void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounts")
            .WithTags("Accounts");

        group.MapPost("/", CreateAccountAsync)
            .WithName("CreateAccount")
            .WithSummary("Create a new bank account")
            .Produces<CreateAccountResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", GetAccountAsync)
            .WithName("GetAccount")
            .WithSummary("Get account details by ID")
            .Produces<AccountResponse>()
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> CreateAccountAsync(
        [FromBody] CreateAccountRequest request,
        IDispatcher dispatcher,
        CancellationToken ct)
    {
        var command = new CreateAccountCommand(
            request.OwnerName,
            request.Currency,
            request.InitialBalance);

        var result = await dispatcher.SendAsync(command, ct);

        return result.IsFailure
            ? HttpResults.Problem(result.Error)
            : Results.Created($"/api/accounts/{result.Value.AccountId}", result.Value);
    }

    private static async Task<IResult> GetAccountAsync(
        Guid id,
        IDispatcher dispatcher,
        CancellationToken ct)
    {
        var result = await dispatcher.QueryAsync(new GetAccountQuery(id), ct);

        return result.IsFailure
            ? HttpResults.Problem(result.Error)
            : Results.Ok(result.Value);
    }
}
