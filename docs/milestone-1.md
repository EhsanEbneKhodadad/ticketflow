# Milestone 1: Foundation

## Intent

Create an executable foundation for a resume-ready support platform. The agreed architecture is a monorepo with a feature-oriented Next.js app, a .NET Clean Architecture modular monolith, and a separate worker. Local development and CI precede cloud hosting.

## Acceptance

- Documented native and Docker setup, dependency lockfiles, environment example, and health checks.
- Domain/Application/Infrastructure/API/Worker boundaries with scoped Codex instructions.
- Initial category migration and explicitly enabled, repeatable Development seeding.
- Generated TypeScript API contract from the running ASP.NET OpenAPI endpoint.
- Frontend checks, backend host/database tests, builds, and CI configuration.
- Responsive, accessible foundation page that does not pretend ticket features exist.

## Implementation sequence

1. Create configuration, project shells, and API/UI contract tests; observe missing behavior.
2. Implement health/info endpoints, persistence, migrations, bootstrap commands, and worker shell.
3. Add the foundation frontend, generated API contract, Compose, and container builds.
4. Document architecture and operations; verify native builds/tests, real PostgreSQL behavior, and Docker startup.
5. Review the complete change and record actual verification, including any environment limitations.

## Defaults and rulings

- New repository at D:/PersonalProjects/ticketflow, branch feat/milestone-1; no extra worktree needed.
- First migration stores categories only. Authentication tables and development personas belong to Milestone 2.
- Development categories: General and Billing; fixed IDs and unique slugs make repeated seeding safe.
- Worker has lifecycle logging only. Hangfire, email delivery, SeaweedFS integration, and search indexing arrive in later milestones.
- Search infrastructure is optional through a Compose profile to keep core local development lighter.
- Shadcn components, form/query libraries, and Cypress are added alongside actual product screens in Milestone 2.
- Initial publication creates a public GitHub repository. Application deployment, cloud resources, and production credentials remain outside this milestone.

## Next milestones

2. Authentication, roles, ticket workflow, replies, internal notes, and audit trail.
3. Attachments and Elasticsearch search with authorization-safe results.
4. Transactional outbox, Hangfire jobs, email, reminders, escalation, and failure replay.
5. Dashboards, admin screens, accessibility, screenshots, and portfolio case study.
6. Budgeted AWS deployment, backups, monitoring, and rollback.
