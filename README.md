# TicketFlow

A support ticket platform built as a Next.js + ASP.NET Core monorepo.

Source: https://github.com/EhsanEbneKhodadad/ticketflow

**Milestone 1: foundation.** Architecture boundaries, health endpoints, database bootstrap, generated API types, local infrastructure, and CI are included. Authentication and tickets begin in Milestone 2.

## Requirements

Node.js 24.21.0, pnpm 10.14.0, .NET SDK 10.0.203, Git, and Docker Desktop with Linux containers.

## Container setup

Run from the repository root in PowerShell:

```powershell
Copy-Item .env.example .env
docker compose --profile app up -d --build --wait
```

A one-shot migration service initializes PostgreSQL and explicitly seeds Development categories. Add --profile search for optional Elasticsearch.

- Web: http://localhost:3000
- API: http://localhost:5080/api/v1/system/info
- OpenAPI: http://localhost:5080/openapi/v1.json
- Mailpit: http://localhost:8025
- Local S3: http://localhost:8333 (credentials in .env; bucket ticketflow-attachments)

## Native development

```powershell
Copy-Item .env.example .env
pnpm install --frozen-lockfile
dotnet tool restore
dotnet restore apps/backend/TicketFlow.sln --locked-mode
pnpm infra:up
```

Set ConnectionStrings\_\_Database in each backend terminal to match the local PostgreSQL credentials. Also set DOTNET_ENVIRONMENT and ASPNETCORE_ENVIRONMENT to Development. See docs/runbooks/local-development.md for exact PowerShell commands.

Apply the schema and seed data with Seed\_\_Enabled=true and:

```powershell
dotnet run --project apps/backend/src/TicketFlow.Api --no-launch-profile -- --migrate --seed
dotnet run --project apps/backend/src/TicketFlow.Api --urls http://localhost:5080
```

Run the worker in another configured terminal, and the frontend in a third:

```powershell
dotnet run --project apps/backend/src/TicketFlow.Worker
pnpm dev
```

Next.js proxies /api/v1 to localhost:5080. API_INTERNAL_URL overrides the upstream at dev/build time. Standalone container rewrites are baked at build time.

## Verification

```powershell
pnpm lint
pnpm typecheck
pnpm test
pnpm build
pnpm format:check
dotnet build apps/backend/TicketFlow.sln --no-restore
dotnet test apps/backend/TicketFlow.sln --no-restore
dotnet format apps/backend/TicketFlow.sln --verify-no-changes --no-restore
docker compose --profile app --profile search config --quiet
```

Database tests use real PostgreSQL Testcontainers. Without Docker, filter with --filter "Category!=Docker"; this does not verify migrations.

## Contracts and migrations

With the Development API running on port 5080:

```powershell
pnpm api:generate
dotnet ef migrations add YourChange --project apps/backend/src/TicketFlow.Infrastructure --startup-project apps/backend/src/TicketFlow.Api --output-dir Persistence/Migrations
```

Commit generated contracts and lockfiles. Apply migrations using --migrate. Normal hosts never modify the schema. --seed requires --migrate, Development, and Seed\_\_Enabled=true.

## Architecture and rules

- apps/web: feature-oriented Next.js frontend.
- apps/backend: Domain, Application, Infrastructure, API, Worker, and tests.
- packages/api-client: generated OpenAPI types.
- infrastructure/docker: container recipes.
- AGENTS.md files: repository and scoped Codex rules.

Read [architecture](docs/architecture/overview.md), [milestone plan](docs/milestone-1.md), and [troubleshooting](docs/runbooks/local-development.md).

## Data and roadmap

docker compose down preserves volumes. Removing volumes deletes local data. Local credentials and loopback bindings are unsuitable for public hosting.

Next: identity and tickets; attachments and search; jobs and notifications; dashboards and portfolio presentation; budgeted AWS deployment.

## Verification status

See [verification notes](docs/verification.md) for passed checks and pending sandbox-dependent acceptance checks.

## Milestone 2 API

Authentication, role-scoped tickets, conversations, internal notes, optimistic concurrency, and audit history are implemented. See [API setup and verification](docs/runbooks/milestone-2-api.md). The frontend workflow is delivered in a separate dependent PR.
