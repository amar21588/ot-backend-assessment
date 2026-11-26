# OT Assessment — Design Notes

## 1. Solution overview

Project | Responsibility |

OT.Assessment.App - Accepts wager requests, publishes them to RabbitMQ, and exposes the player wager and top-spender read endpoints 
OT.Assessment.Consumer - Runs as a .NET Worker Service, consumes wager messages, and persists them to SQL Server 
OT.Assessment.Contracts - Holds the message contract shared by the API and consumer 

## 2. Key design choices


### Asynchronous messaging with RabbitMQ

RabbitMQ separates request acceptance from database persistence. The API and consumer can be scaled independently, and the queue can absorb short-lived bursts while the database catches up.

The wager queue is declared as durable, and published messages use persistent delivery mode. Publisher confirms are enabled so the publisher can detect whether RabbitMQ accepted a publication. The API reports a service error if it cannot publish rather than returning a successful acceptance response for a message it failed to enqueue.

The API and consumer must declare the same exchange, queue, binding, and queue arguments. RabbitMQ rejects incompatible redeclarations, so topology configuration is kept consistent between the two projects.

### Reliable consumer processing

The consumer acknowledges a message only after the SQL operation succeeds. If persistence fails, the message is not acknowledged as successfully processed.

The implementation uses a retry queue with a time-to-live delay and a dead-letter route back to the main queue. A bounded retry count prevents a permanently failing message from circulating forever. Messages that exceed the retry limit are routed to a dead-letter queue for investigation and controlled replay.

Malformed or invalid messages should be isolated rather than repeatedly retried. The dead-letter queue should be monitored because messages there require operational attention.

### Idempotent database writes

Message delivery can be repeated. For example, the consumer may successfully commit a database transaction but stop before RabbitMQ receives the acknowledgement. RabbitMQ can then redeliver the message.

The database write therefore uses `WagerId` as the unique identity and checks for an existing row before inserting. The primary key remains the final uniqueness safeguard. This makes repeated delivery safe and avoids counting the same wager more than once.

### SQL Server and Dapper

Dapper is used for lightweight data access without the overhead of a full change-tracking ORM. The read endpoints call stored procedures so that filtering, sorting, pagination, aggregation, and query plans remain close to the database.

Indexes support the main access patterns:

- Player wager history: filter by `AccountId`, then sort by `CreatedDateTime` descending.
- Top spenders: aggregate wager `Amount` by `AccountId`.

Amounts use a fixed-precision decimal type rather than floating point. Timestamps retain offset information. The wager identifier is used as a secondary sort key to make pagination ordering deterministic when timestamps are equal.

### SQL Server and Dapper

Dapper is used for lightweight data access without the overhead of a full change-tracking ORM. The read endpoints call stored procedures so that filtering, sorting, pagination, aggregation, and query plans remain close to the database.

Indexes support the main access patterns:

- Player wager history: filter by `AccountId`, then sort by `CreatedDateTime` descending.
- Top spenders: aggregate wager `Amount` by `AccountId`.

Amounts use a fixed-precision decimal type rather than floating point. Timestamps retain offset information. The wager identifier is used as a secondary sort key to make pagination ordering deterministic when timestamps are equal.

## 3. Local setup (Windows, without Docker)

### Prerequisites

- .NET 10 SDK
- Visual Studio 2022
- SQL Server 
- RabbitMQ Server installed as a Windows service
- Erlang/OTP version supported by the installed RabbitMQ release

### Configure RabbitMQ

1. Install Erlang
2. Install RabbitMQ
3.Confirm that RabbitMQ is running with [rabbitmqctl status, rabbitmq-plugins enable rabbitmq_management]
4. The local development configuration uses `localhost:5672` and the default local `guest` account.

### Restore and build

Run these commands from the solution root:

powershell commands

dotnet clean
dotnet restore
dotnet build
dotnet run

## 4. Verification and test plan

### Verify the end-to-end message flow

1. Start SQL Server and RabbitMQ.
2. Start the consumer and confirm that it connects and begins consuming.
3. Start the API and confirm that it connects to RabbitMQ.
4. Submit one known test wager.
5. Confirm that the API returns `202 Accepted`.
6. Confirm in the consumer logs that the message was received and persisted.
7. Query `dbo.CasinoWagers` and verify the row by its `WagerId`.
8. Call the player wager GET endpoint using the wager's `AccountId`.
9. Call the top-spenders endpoint and verify that the wager amount contributes to the expected account total.

A successful POST response proves publication was accepted; it does not, by itself, prove that the consumer persisted the wager.

### Verify pagination and aggregation

Test the player endpoint with:

- The default page size and page number.
- A page size of 1.
- A later page.
- An account with no wagers.
- An invalid page number or page size, according to the API's validation behavior.

Test top spenders with:

- The default count.
- A count of 1.
- More than one account with different total wager amounts.
- An account with multiple wagers, to confirm amounts are summed by account.

## 5. Challenges encountered

.NET 10 and legacy remoting namespaces
Matching queue topology
Diagnosing asynchronous persistence
Running dependencies natively
Docker implementation

## 6. Logging, tracing, and observability
Application logs should include the wager ID and relevant operation outcome so that a wager can be followed across the API, message broker, and consumer. Avoid logging credentials, connection strings, or sensitive session payloads.

## 7. Production-ready improvements

1. Configuration and secrets
2. Authentication and authorization
3. Input validation
4. API resilience,Outbox pattern, Idempotency contract,Retry policy,Publisher reliability
5. Database performance (EF)
6. Observability and alerting

