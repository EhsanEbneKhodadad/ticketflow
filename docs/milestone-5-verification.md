# Milestone 5 verification

Milestone 5 adds database-backed dashboard metrics and a server-authorized admin workspace. This report records local verification on the integrated M5 branch. No screenshots were requested.

## Frontend checks

- Jest: 36 tests across 11 suites passed.
- TypeScript typecheck and ESLint passed.
- Prettier formatting check passed.
- Cypress e2e TypeScript check passed.
- Next.js production build completed with `/admin`, `/dashboard`, and `/search` routes.
- Cypress browser journey: 2 tests passed. It verified customer ticket counts and admin-access denial, then created an admin category at a 390px viewport.

## Backend checks

- Release unit tests: 50 passed.
- PostgreSQL-backed integration suite is running with Elasticsearch-only tests filtered because local registry access to the Elasticsearch 8.19.4 image is unavailable. Record the exact integration count after the command completes.
- GitHub CI is the validation environment for the two Elasticsearch integration tests.

## Local application smoke test

The Docker Compose `app` profile built and started successfully using temporary local port overrides. PostgreSQL migration and Hangfire job-storage bootstrap completed; API, worker, storage, Mailpit, and web services became healthy. The web health endpoint returned HTTP 200. Search was disabled for this smoke test.

## Contracts and migrations

With the Development API running on port 5080:

```powershell
pnpm api:generate
dotnet ef migrations add YourChange --project apps/backend/src/TicketFlow.Infrastructure --startup-project apps/backend/src/TicketFlow.Api --output-dir Persistence/Migrations
```

Commit generated contracts and lockfiles. Apply migrations using `--migrate`. Normal hosts never modify the schema. `--seed` requires `--migrate`, Development, and `Seed__Enabled=true`.

## Architecture and rules

- `apps/web`: feature-oriented Next.js frontend.
- `apps/backend`: Domain, Application, Infrastructure, API, Worker, and tests.
- `packages/api-client`: generated OpenAPI types.
- `infrastructure/docker`: container recipes.
- `AGENTS.md` files: repository and scoped Codex rules.

Read [architecture](architecture/overview.md), [milestone plan](milestone-1.md), and [troubleshooting](runbooks/local-development.md).

## Data and operations

`docker compose down` preserves volumes. Removing volumes deletes local data. Local credentials and loopback bindings are unsuitable for public hosting.

See [the jobs runbook](runbooks/milestone-4-jobs.md) for Hangfire bootstrap, reminder/escalation defaults, retries, and failure replay. See [the Milestone 5 case study](milestone-5.md) for dashboard/admin scope and design decisions.
