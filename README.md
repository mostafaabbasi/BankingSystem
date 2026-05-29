# Banking Transaction Processing System

A production-grade banking backend built with **.NET 10**, Clean Architecture, Vertical Slice organization, a custom `IDispatcher` (no MediatR), RabbitMQ choreography saga (via MassTransit), PostgreSQL, Redis, and a full observability stack.

---

## Quick Start

```bash
# Start all services
docker compose up -d

# API docs
http://localhost:8080/scalar/v1

# Health check
curl http://localhost:8080/health
```

---

## Architecture Overview

```
BankingSystem/
├── src/
│   ├── BankingSystem.Domain/               # Pure domain — no framework references
│   │   ├── Accounts/
│   │   │   ├── Account.cs                  # Sealed aggregate root
│   │   │   ├── AccountEvents.cs            # Domain events (sealed records)
│   │   │   └── IAccountRepository.cs
│   │   ├── Transactions/
│   │   │   ├── Transaction.cs              # Sealed aggregate root
│   │   │   ├── TransactionEvents.cs
│   │   │   └── ITransactionRepository.cs
│   │   └── Common/
│   │       ├── Entity.cs                   # Base entity + domain event tracking
│   │       ├── Result.cs                   # Result<T> / Error monad
│   │       └── IUnitOfWork.cs
│   │
│   ├── BankingSystem.Application/          # Use cases, vertical slices, custom dispatcher
│   │   ├── Accounts/
│   │   │   ├── CreateAccount/              # Command + validator + handler
│   │   │   └── GetAccount/                 # Query + handler
│   │   ├── Transactions/
│   │   │   ├── Transfer/                   # Command + validator + handler
│   │   │   ├── GetTransaction/             # Query + handler
│   │   │   └── Saga/
│   │   │       ├── Messages/               # Saga message contracts
│   │   │       ├── InitiateTransferSagaHandler.cs
│   │   │       ├── DebitAccountSagaHandler.cs
│   │   │       ├── CreditAccountSagaHandler.cs
│   │   │       ├── CompleteTransferSagaHandler.cs
│   │   │       └── RollbackDebitSagaHandler.cs
│   │   └── Common/
│   │       ├── Dispatcher/                 # Custom IDispatcher (replaces MediatR)
│   │       │   ├── IDispatcher.cs          # SendAsync / QueryAsync / PublishAsync
│   │       │   ├── ICommand.cs / IQuery.cs
│   │       │   ├── ICommandHandler.cs / IQueryHandler.cs
│   │       │   ├── IEventHandler.cs
│   │       │   ├── IPipelineBehavior.cs
│   │       │   └── Dispatcher.cs
│   │       ├── Behaviors/
│   │       │   ├── LoggingBehavior.cs
│   │       │   └── ValidationBehavior.cs
│   │       ├── IIdempotencyService.cs
│   │       ├── IDistributedLockService.cs
│   │       └── IMessageBus.cs
│   │
│   ├── BankingSystem.Infrastructure/       # EF Core, RabbitMQ, Redis
│   │   ├── Persistence/
│   │   │   ├── BankingDbContext.cs
│   │   │   ├── Configurations/
│   │   │   ├── Repositories/
│   │   │   └── UnitOfWork.cs
│   │   ├── Messaging/
│   │   │   ├── RabbitMqMessageBus.cs
│   │   │   ├── Consumers/                  # One MassTransit consumer per saga step
│   │   │   ├── Handlers/                   # IEventHandler<T> domain event handlers
│   │   │   └── IntegrationEvents/
│   │   ├── Outbox/
│   │   │   ├── OutboxMessage.cs
│   │   │   └── OutboxProcessorJob.cs
│   │   ├── Idempotency/
│   │   └── Locking/
│   │
│   └── BankingSystem.Api/                  # Minimal API entry point
│       ├── Common/
│       │   ├── IEndpoint.cs                # All endpoints implement this
│       │   ├── EndpointExtensions.cs       # Assembly scan + MapEndpoints()
│       │   └── HttpResults.cs
│       ├── Dtos/Requests/                  # API request models (decoupled from commands)
│       ├── Endpoints/
│       │   ├── Accounts/AccountEndpoints.cs
│       │   └── Transactions/TransactionEndpoints.cs
│       └── Program.cs
│
├── tests/
│   ├── BankingSystem.UnitTests/
│   └── BankingSystem.IntegrationTests/     # Testcontainers (Postgres, Redis, RabbitMQ)
│
├── grafana/                                # Grafana provisioning + dashboards
├── prometheus/prometheus.yml               # Prometheus scrape config
├── Directory.Packages.props                # Central Package Management (CPM)
└── docker-compose.yml
```

### Architectural Pattern

**Clean Architecture + Vertical Slices**

- `Api → Application → Domain ← Infrastructure`
- Each feature owns its command/query, validator, and handler in one folder
- **Custom `IDispatcher`** replaces MediatR — full control over the pipeline

---

## Custom Dispatcher

```
IDispatcher
  ├── SendAsync<TResponse>(ICommand<TResponse>)    → ICommandHandler + pipeline
  ├── QueryAsync<TResponse>(IQuery<TResponse>)     → IQueryHandler + pipeline
  ├── PublishAsync<TEvent>(TEvent)                 → all IEventHandler<TEvent>
  └── PublishAsync(object)                         → runtime dispatch (Outbox)
```

**Pipeline behaviors** (commands + queries only):
1. `LoggingBehavior` — timing, slow request detection (> 500 ms)
2. `ValidationBehavior` — FluentValidation, short-circuits with `Result.Failure`

---

## Data Flow: Money Transfer

```
POST /api/transactions/transfer
  Header: Idempotency-Key: <uuid>
  Body:   { fromAccountId, toAccountId, amount, currency }
  │
  ▼
TransactionEndpoints → IDispatcher.SendAsync(TransferCommand)
  │
  ├── LoggingBehavior
  └── ValidationBehavior
        │
        ▼
        TransferHandler
          1. Check Redis idempotency key       → return cached if duplicate
          2. Acquire distributed locks          → sorted by account ID (deadlock-safe)
          3. Validate accounts + pre-check balance
          4. Create Transaction (Pending) → PostgreSQL
          5. Store idempotency key in Redis (7-day TTL)
          6. Publish InitiateTransferSagaMessage → RabbitMQ
        │
        ▼
        202 Accepted { transactionId, status: "Pending", correlationId }

── Async Saga (RabbitMQ / MassTransit) ──────────────────────────────

InitiateTransferConsumer → InitiateTransferSagaHandler
  └── publishes DebitAccountMessage

DebitAccountConsumer → DebitAccountSagaHandler
  ├── [Success] publishes CreditAccountMessage
  └── [Fail]    marks Transaction.Failed → domain event → Outbox

CreditAccountConsumer → CreditAccountSagaHandler
  ├── [Success] publishes CompleteTransferMessage
  └── [Fail]    publishes RollbackDebitMessage (compensation)

CompleteTransferConsumer → CompleteTransferSagaHandler
  └── transaction.MarkCompleted() → TransactionCompletedEvent → Outbox

RollbackDebitConsumer → RollbackDebitSagaHandler
  └── restore balance → MarkFailed + MarkCompensated → Outbox

── Outbox Processor (every 5 s) ─────────────────────────────────────

IDispatcher.PublishAsync(domainEvent)
  └── TransactionCompletedEventHandler
        └── publishes TransferCompletedIntegrationEvent → RabbitMQ
```

---

## Endpoint Registration

All endpoints implement `IEndpoint` and are auto-discovered at startup:

```csharp
// Program.cs
builder.Services.AddEndpoints(Assembly.GetExecutingAssembly());
app.MapEndpoints();
```

**Request DTOs** (`Api/Dtos/Requests/`) are decoupled from Application commands. Adding a new endpoint = create a class, implement `IEndpoint`, done.

---

## Concurrency & Consistency

### Double-Spend Prevention (3 layers)

| Layer | Mechanism | Scope |
|---|---|---|
| 1 | Redis distributed lock (sorted account IDs) | Cross-instance |
| 2 | EF Core optimistic concurrency (`RowVersion`) | DB-level |
| 3 | Idempotency key (unique DB index) | Duplicate detection |

**Sorted lock keys**: always lock `min(A,B)` before `max(A,B)` — prevents A→B vs B→A deadlocks.

### Transaction Lifecycle

```
Pending → Completed
        → Failed → Compensated
```

### Consistency Model

**Strong** within each saga step (PostgreSQL ACID).  
**Eventual** across the full saga (RabbitMQ choreography). Client polls `GET /api/transactions/{id}`.

---

## API Reference

### Create Account

```
POST /api/accounts
Content-Type: application/json

{
  "ownerName": "john.doe",
  "currency": "EUR",        // EUR | USD | GBP
  "initialBalance": 1000.00
}

201 Created
{
  "accountId": "3fa85f64-...",
  "ownerName": "john.doe",
  "balance": 1000.00,
  "currency": "EUR",
  "status": "Active",
  "createdAt": "..."
}
```

### Get Account

```
GET /api/accounts/{id}

200 OK | 404 Not Found
```

### Initiate Transfer

```
POST /api/transactions/transfer
Content-Type: application/json
Idempotency-Key: <uuid>          ← required header

{
  "fromAccountId": "...",
  "toAccountId": "...",
  "amount": 250.00,
  "currency": "EUR"
}

202 Accepted
{
  "transactionId": "...",
  "status": "Pending",
  "correlationId": "...",
  "createdAt": "..."
}
```

### Get Transaction

```
GET /api/transactions/{id}

200 OK | 404 Not Found
{ "status": "Pending" | "Completed" | "Failed" | "Compensated", ... }
```

---

## Observability

| Tool | Purpose | URL |
|---|---|---|
| **Grafana** | Prometheus metrics dashboard | http://localhost:3000 |
| **Prometheus** | Metrics storage + scraping | http://localhost:9091 |
| **Zipkin** | Distributed tracing | http://localhost:9411 |
| **Kibana** | Structured log search | http://localhost:5601 |
| **Elasticsearch** | Log storage | http://localhost:9200 |

### Grafana Dashboard (Banking System)

- **HTTP Traffic**: request rate, error rate, P50/P95/P99 latency, rate by route
- **Process Resources**: CPU %, RAM (resident + virtual), thread count
- **Thread Pool**: active threads, queue length
- **.NET GC**: heap size by generation, collections/sec, allocated bytes/sec

### Zipkin — Tracing

Open http://localhost:9411 → search `serviceName = BankingSystem`.  
Filter by tag `correlationId = <value>` to trace a full transfer saga across all steps.

### Kibana — Logs

Create a data view with pattern `logs-dotnet-*`, time field `@timestamp`.  
Filter by `correlationId` to correlate logs with a specific request.

---

## Saga Pattern (Choreography)

The transfer uses **choreography** — no central orchestrator. Each step publishes a message; the next consumer reacts.

```
InitiateTransfer
     │
     ▼
DebitAccount ──(fail)──► mark Transaction.Failed (nothing to roll back yet)
     │
     ▼
CreditAccount ──(fail)──► RollbackDebit (restore source balance)
     │
     ▼
CompleteTransfer ──► TransactionCompletedEvent ──► Outbox ──► integrations
```

**Consumers are idempotent**: MassTransit retries on failure with exponential backoff. Dead-letter queues capture permanently unprocessable messages.

---

## Dockerfile — Multi-Stage Build

```
restore → test (unit, 125 tests) → publish → runtime
              ↑
        fails here = image is NOT produced
```

Unit tests run as a **build gate** inside Docker. If any test fails the image is not created. Integration tests (Testcontainers) require a live Docker daemon and must run separately before the Docker build.

---

## Non-Functional Design

### Scalability
- Stateless API — horizontally scalable
- Redis for distributed state (locks + idempotency)
- RabbitMQ consumers scale independently from the API
- PostgreSQL read replicas ready (CQRS pattern in place)

### Fault Tolerance
- MassTransit retry with exponential backoff on every queue
- Dead-letter queues for permanently failed messages
- Distributed locks expire automatically (no orphaned locks on crash)
- EF Core retry-on-failure for transient Postgres errors

### Security (Placeholders)
- `AddAuthentication()` / `AddAuthorization()` wired — swap in JWT Bearer
- TLS termination at reverse proxy
- Secrets via environment variables

---

## Trade-offs & Simplifications

| Decision | Simplified | Production approach |
|---|---|---|
| Auth | Placeholder | JWT + scope-based authz |
| Currency | Same currency only | FX rate service + multi-currency |
| Saga state | Via Transaction.Status | MassTransit StateMachine |
| Outbox | Poll every 5 s | CDC-based (Debezium) |
| Read models | Query hits write DB | Redis projections / read replicas |
| Rate limiting | None | `Microsoft.AspNetCore.RateLimiting` |

---

## Running Tests

### Unit Tests
Run anywhere — no external dependencies:
```bash
dotnet test tests/BankingSystem.UnitTests
```

### Integration Tests
Require a running Docker daemon (Testcontainers spins up Postgres, Redis, RabbitMQ):
```bash
dotnet test tests/BankingSystem.IntegrationTests
```

### In Docker Build
Unit tests are a build gate inside the Dockerfile — the image will not be produced if any unit test fails:

```
restore → test (unit) → publish → runtime
              ↑
        fails here = build stops
```

Integration tests cannot run inside `docker build` (no Docker daemon in the build context). Run them locally or in a CI step before the Docker build.

---

## Running Migrations

Migrations run automatically on startup in `Development` mode.

To run manually:
```bash
dotnet ef database update \
  --project src/BankingSystem.Infrastructure \
  --startup-project src/BankingSystem.Api \
  --connection "Host=localhost;Port=5432;Database=banking;Username=banking_user;Password=banking_pass;"
```

---

## Infrastructure Ports

| Service | Port | UI |
|---|---|---|
| API | 8080 | http://localhost:8080/scalar/v1 |
| PostgreSQL | 5432 | — |
| Redis | 6379 | — |
| RabbitMQ | 5672 / 15672 | http://localhost:15672 |
| Elasticsearch | 9200 | — |
| Kibana | 5601 | http://localhost:5601 |
| Zipkin | 9411 | http://localhost:9411 |
| Prometheus | 9091 | http://localhost:9091 |
| Grafana | 3000 | http://localhost:3000 |
