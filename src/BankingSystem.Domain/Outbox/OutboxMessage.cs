namespace BankingSystem.Domain.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(
        string type,
        string payload,
        string correlationId)
    {
        Id = Guid.NewGuid();
        Type = type;
        Payload = payload;
        CorrelationId = correlationId;
        OccurredAt = DateTimeOffset.UtcNow;
        Status = OutboxMessageStatus.Pending;
    }

    public Guid Id { get; private set; }

    public string Type { get; private set; } = default!;

    public string Payload { get; private set; } = default!;

    public string CorrelationId { get; private set; } = default!;

    public OutboxMessageStatus Status { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? Error { get; private set; }

    public int RetryCount { get; private set; }

    public void MarkProcessed()
    {
        Status = OutboxMessageStatus.Processed;
        ProcessedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string error)
    {
        RetryCount++;
        Error = error;

        Status = RetryCount >= 5
            ? OutboxMessageStatus.DeadLetter
            : OutboxMessageStatus.Pending;
    }
}