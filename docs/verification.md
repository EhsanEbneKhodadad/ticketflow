# Milestone 1 verification

Local verification before the initial GitHub push: 2026-10-27. Some checks required a normal Windows process outside the restricted execution shell. Remote results are available in the repository Actions tab.

## Passed

- .NET Release solution build: zero warnings and zero errors.
- Eight backend unit tests: dependency boundaries and explicit development-seed policy.
- Three API host tests: database-independent liveness, unavailable-database readiness, and documented system info.
- Two frontend behavior tests: foundation status and accessible navigation.
- Frontend lint and strict TypeScript checks, including the installed generated client.
- pnpm frozen-lockfile offline installation with the configured hoisted/copy workspace layout.
- Live OpenAPI generation and matching source/installed client schema hashes.
- EF migration snapshot/model consistency.
- Full C# formatter verification and repository Prettier formatting.
- Complete frontend production build, including standalone output, outside the restricted shell.
- Docker Compose validation with all profiles enabled.
- Registry checks for configured PostgreSQL, Mailpit, SeaweedFS, and .NET build/runtime images.
- Native HTTP smoke checks: frontend page 200 with expected content, web health, and Next.js-to-ASP.NET system-info proxy.
- Read-only code review; client source-copy and regeneration-synchronization findings were fixed and rechecked.

## Pending environment-dependent checks

- The PostgreSQL Testcontainers test could not start locally: Docker was inaccessible in the restricted shell, and the Linux engine was not running when checked outside it. Eleven other backend tests pass. Real migration execution and repeatable seeding await Docker-backed verification.
- The full Compose stack has not been started or container-built here. SeaweedFS runtime behavior is therefore pending.
- Restricted-shell production builds intermittently failed with Windows EPERM directory errors. The complete production build subsequently passed outside that shell; use a normal terminal or the configured container CI job for reproducible packaging checks.
- GitHub Actions is configured to perform the Docker-backed checks on push. Check the repository Actions tab for the latest result. No cloud application deployment was created.

## Finish acceptance outside the restricted shell

Use a normal terminal with Docker Desktop's Linux engine running:

1. pnpm install --frozen-lockfile
2. pnpm build
3. dotnet test apps/backend/TicketFlow.sln
4. dotnet format apps/backend/TicketFlow.sln --verify-no-changes
5. docker compose --profile app up -d --build --wait
6. Check /health/ready on port 5080 and /api/v1/system/info through port 3000.

Implementation and native validation are complete. Milestone 1 acceptance still requires Docker-backed checks. No tests were silently disabled to obtain a green result.
