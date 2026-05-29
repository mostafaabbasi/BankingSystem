namespace BankingSystem.Api.Dtos.Requests;

public sealed record TransferRequest(
    Guid FromAccountId,
    Guid ToAccountId,
    decimal Amount,
    string Currency);
