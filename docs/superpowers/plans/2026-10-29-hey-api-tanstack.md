# Hey API and TanStack Query Integration Plan

> **For agentic workers:** Implement this plan inline. Steps use checkbox syntax for tracking.

**Goal:** Generate API types and typed TanStack Query operations from the backend OpenAPI contract and use them throughout the interactive frontend.

**Architecture:** `packages/api-client` owns Hey API output and re-exports the generated types, SDK operations, and query/mutation helpers. The web app configures the generated client to use the existing cookie/CSRF/session-aware transport. Feature modules supply account-aware query inputs and own cache invalidation.

**Tech Stack:** ASP.NET OpenAPI, `@hey-api/openapi-ts`, Hey API Fetch client, TanStack Query v5, Next.js 16, TypeScript.

**Spec:** `docs/decisions/0009-hey-api-tanstack-query.md`

## Global Constraints

- OpenAPI is the source of truth for browser DTOs.
- Do not hand-edit generated files.
- Preserve same-origin cookies, CSRF, refresh behavior, and expected-principal checks.
- Do not generate mutation keys because mutation bodies may contain credentials or private values.
- Keep private query caches separated by authenticated principal.

## Review Focus

- Account switches during pending requests must not display or cache another user's data.
- Multipart uploads must preserve their content type and existing attachment behavior.
- Mutations that return no content must retain their existing success behavior.
- Search and list filters must remain represented in cache keys.
- Auth endpoints must not put credentials into generated mutation keys.

---

### Task 1: Configure generation and transport

**Files:** `packages/api-client/openapi-ts.config.ts`, `packages/api-client/src/index.ts`, `packages/api-client/package.json`, `scripts/sync-api-client.mjs`, `apps/web/src/lib/api.ts`, `apps/web/src/lib/api-client-config.ts`, `apps/web/src/components/providers.tsx`, `.cursor/rules/frontend-api-data.mdc`, `apps/web/AGENTS.md`.

- [ ] Replace `openapi-typescript` with pinned Hey API generation and TanStack Query plugins.
- [ ] Synchronize the complete generated source directory into pnpm's injected workspace package on Windows.
- [ ] Configure generated Fetch requests through the shared TicketFlow transport.
- [ ] Regenerate from the local OpenAPI endpoint and export generated types, operations, and query artifacts.

### Task 2: Adopt generated client and TanStack options

**Files:** feature API modules and interactive query/mutation components under `apps/web/src/features`, plus `apps/web/src/features/auth/auth-provider.tsx`.

- [ ] Replace manually declared DTO aliases with generated endpoint and schema types.
- [ ] Replace handwritten query keys/options with generated query options/keys.
- [ ] Replace handwritten mutation transport functions with generated mutation options while keeping feature-specific invalidation and UI feedback.
- [ ] Keep page routes thin and existing loading/error/permission UX intact.

### Task 3: Verify and document

**Files:** `docs/decisions/0009-hey-api-tanstack-query.md`, `docs/architecture/overview.md`, package and frontend rules.

- [ ] Regenerate the client and confirm generated artifacts are stable.
- [ ] Run formatting, lint, typecheck, and production build.
- [ ] Review the diff for handwritten generated-file edits, unsafe cache keys, and lost security transport behavior.
