# TicketFlow — repository rules

- Read this file and the nearest scoped AGENTS.md before editing.
- Read docs/architecture/overview.md and docs/milestone-1.md for boundaries and status.
- This is one organization, a modular monolith, and a separate worker; no microservices.
- Work only on the requested milestone. Do not silently add dependencies or product features.
- Keep secrets out of source control. Use .env locally; .env.example contains local-only defaults.
- Domain and Application must not depend on infrastructure or host projects.
- OpenAPI is the source of truth for browser DTOs; regenerate packages/api-client after API changes.
- Add meaningful behavior tests for business changes. Do not add tests that only mirror configuration.
- Run pnpm lint, pnpm typecheck, pnpm test, pnpm build, and pnpm format:check for web/shared changes.
- Run dotnet build apps/backend/TicketFlow.sln and dotnet test apps/backend/TicketFlow.sln for backend changes.
- Docker tests require a running Linux-container engine. Report unavailable checks explicitly.
- Run dotnet format apps/backend/TicketFlow.sln --verify-no-changes before handing over C# changes.
- Never edit generated API types or EF migration designer/snapshot files manually.
- Document architecture changes in docs/decisions; keep README setup commands accurate.
- Never push, publish, delete persistent volumes, or create production credentials without authorization.
