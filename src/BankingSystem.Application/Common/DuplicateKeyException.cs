namespace BankingSystem.Application.Common;

public sealed class DuplicateKeyException(string message) : Exception(message);
