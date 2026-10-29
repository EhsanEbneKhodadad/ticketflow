# Durable jobs and notifications

Ticket writes and an outbox row for each new audit event commit in the same EF Core SaveChanges transaction. The outbox stores event identifiers and participant IDs rather than reply bodies, filenames, or credentials. Notification messages contain only the ticket number and a link, and internal events do not notify customers.

The worker uses Hangfire's PostgreSQL storage for a recurring dispatcher. The dispatcher claims rows with `FOR UPDATE SKIP LOCKED` and a two-minute lease, retries failures with backoff, and holds an event for replay after five failed attempts. Failed event IDs and failure types are logged without email addresses or ticket text. The delivery model is **at least once**: a crash after SMTP accepts a message but before the outbox row is completed can cause a duplicate. The `X-TicketFlow-Event` email header identifies a logical event; it is not a guarantee of deduplication by SMTP.

Customer reminders are scheduled once for each ticket version after 24 hours in WaitingForCustomer. Escalations are scheduled once for each version after 48 hours in Open or InProgress. The assigned agent receives escalations; if no agent is assigned, the first administrator by ID receives it. Set `Jobs__ReminderHours` and `Jobs__EscalationHours` to positive integers to change the thresholds. Before sending, the processor rechecks the ticket version and status, so stale reminders are discarded. These thresholds are demo defaults and do not represent business SLAs.

## Bootstrap and operations

Normal API and worker startup do not migrate schemas. Apply application migrations first, then explicitly prepare or upgrade Hangfire storage:

```powershell
dotnet run --project apps/backend/src/TicketFlow.Api -- --migrate
dotnet run --project apps/backend/src/TicketFlow.Worker -- --prepare-jobs
$env:Jobs__Enabled = "true"
dotnet run --project apps/backend/src/TicketFlow.Worker
```

Provide `ConnectionStrings__Database`, `Mail__Host`, `Mail__Port`, and `App__PublicUrl` as in local setup. The Compose app profile runs a dedicated `jobs-init` step after the database migration and enables jobs in the worker. Existing installations need the new application migration and explicit `--prepare-jobs` before enabling jobs. Search's full periodic reconciliation remains enabled separately for index-loss recovery.

Find failed IDs using a trusted database connection:

```sql
SELECT "Id", "Kind", "Attempts", "FailedAt" FROM outbox_messages WHERE "FailedAt" IS NOT NULL ORDER BY "FailedAt";
```

After fixing the failure, replay one event using a trusted operator environment:

```powershell
dotnet run --project apps/backend/src/TicketFlow.Worker -- --replay <event-guid>
```

Replay only resets failed outbox rows; the running worker sends them on a subsequent dispatch. The command does not expose an unauthenticated HTTP endpoint. Avoid replaying without checking whether SMTP already accepted a previous attempt.
