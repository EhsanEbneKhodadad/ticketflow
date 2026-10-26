# ADR 0001: Monorepo with Clean Architecture and feature slices

Status: Accepted for Milestone 1.

## Decision

Use pnpm workspaces for JavaScript and one .NET solution for backend projects. Keep one API deployment and a separate worker host. Enforce inward dependencies and organize product behavior by feature.

## Alternatives

A lean vertical-slice API uses fewer projects but makes dependency boundaries less explicit. Microservices add deployment and distributed consistency work without a current product need.

## Consequences

Frontend and API contracts evolve together. Backend project boundaries are visible and testable. CI checks both runtimes. More initial folders are accepted in exchange for clear ownership; do not add abstractions until a use case needs them.
