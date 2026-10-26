# Backend rules

- Maintain Domain <- Application <- Infrastructure; API and Worker are composition roots.
- Domain contains framework-free business rules. Application owns use cases and interfaces.
- Group use cases by feature, with explicit inputs, outputs, validation, and authorization.
- Keep endpoints thin. Use /api/v1, typed responses, OpenAPI, and Problem Details.
- Use EF Core in Infrastructure; do not add generic repositories or a mediator by default.
- Use async I/O and propagate CancellationToken.
- PostgreSQL is authoritative; search is an eventually consistent projection.
- Commit database changes and outbox records in one transaction when the outbox is introduced.
- Never run migrations or seed data on normal API/worker startup.
- Add EF migrations using the root local dotnet-ef tool; inspect generated SQL.
- Seed only with explicit opt-in in Development. Never log connection strings or tokens.
- Enforce resource permissions in application/API code, including search, downloads, and internal notes.
- Use xUnit for behavior; Testcontainers for actual database and search behavior.
- Run dotnet build/test apps/backend/TicketFlow.sln and dotnet format --verify-no-changes.
