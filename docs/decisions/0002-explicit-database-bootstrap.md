# ADR 0002: Explicit database bootstrap

Status: Accepted for Milestone 1.

## Decision

Normal host startup never migrates or seeds. The API executable accepts --migrate, and --seed only together with --migrate, Development environment, and Seed\_\_Enabled=true.

The initial schema contains categories. The development seeder upserts General and Billing using stable IDs and unique slugs. Identity accounts arrive with authentication in Milestone 2.

## Consequences

Compose uses a one-shot migration service before starting API and worker. Operators control schema changes; multiple host replicas do not race to migrate. A failed migration prevents dependent app startup.
