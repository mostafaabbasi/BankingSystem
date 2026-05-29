namespace BankingSystem.Api.Dtos.Requests;

public sealed record CreateAccountRequest(
    string OwnerName,
    string Currency,
    decimal InitialBalance);
