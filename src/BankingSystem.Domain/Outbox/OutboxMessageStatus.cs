namespace BankingSystem.Domain.Outbox;

public enum OutboxMessageStatus
{
    Pending = 0,
    Processed = 1,
    DeadLetter = 2
}