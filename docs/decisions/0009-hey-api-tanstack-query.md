# Decision 0009: Generate the browser API layer with Hey API

- Status: Accepted
- Date: 2026-10-29

## Context

The API client currently generates OpenAPI TypeScript declarations with `openapi-typescript`, while the web app manually builds request paths, query keys, query options, and mutation functions. This duplicates API contract details across the monorepo.

TicketFlow also has a shared browser transport that provides same-origin cookies, CSRF tokens, session refresh, account-change protection, and safe API errors. Replacing it with a default generated transport would weaken existing security behavior.

## Decision

Use `@hey-api/openapi-ts` to generate TypeScript API types, SDK operations, and TanStack Query v5 query options, query keys, and mutation options from the ASP.NET OpenAPI document. Generated artifacts live under `packages/api-client/src/generated` and are never edited by hand.

Route generated SDK requests through the existing browser transport so CSRF, cookie, refresh, and expected-user checks remain centralized. Include the authenticated principal in protected query inputs and therefore their generated keys. Do not generate mutation keys: mutation variables can contain credentials or other sensitive request bodies.

## Consequences

- OpenAPI remains the source of truth for browser request and response types.
- The API client package owns generation; the web app owns UI-specific query invalidation and mutations.
- Run `pnpm api:generate` with the local API available at `http://localhost:5080`.
- Query and mutation cache behavior stays explicit in feature modules while operation signatures and query keys come from generated code.
