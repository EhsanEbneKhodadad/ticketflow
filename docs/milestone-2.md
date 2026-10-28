# Milestone 2: Identity and complete ticket workflow

## Approved intent

Implement the previously agreed Milestone 2, then test it, provide screenshots, and create separate backend and frontend GitHub pull requests.

## Design

Keep the existing Clean Architecture modular monolith. ASP.NET Identity stores accounts in PostgreSQL. HttpOnly JWT access cookies expire in 15 minutes; hashed refresh sessions rotate with reuse detection, expire after seven days, and revoke immediately on logout/reset. Durable session-family revocation and Identity security-stamp checks prevent concurrent refresh from escaping logout or password reset. All mutating API routes validate an antiforgery token. Registration creates Customers only; staff roles are provisioned through explicitly enabled Development seeding.

Customers can list/read/edit their own active tickets and post public replies. Agents/Admins manage the queue, assignment, priorities and resolution. Internal notes and their audit events are excluded from customer responses. Closed tickets must be reopened before posting. Use the approved status graph and integer optimistic concurrency versions. PostgreSQL generates TKT-1001-style display numbers.

Password reset sends a short-lived single-use link to local SMTP/Mailpit. Public reset requests never reveal account existence. No attachment integration, Elasticsearch, notification jobs, admin-management screens, dashboard analytics, or cloud deployment is added.

## API

Versioned auth routes: csrf, register, login, me, refresh, logout, forgot-password, reset-password.
Authenticated tickets: paginated GET/POST /tickets; GET/PUT /tickets/{id}; POST /tickets/{id}/messages; PATCH /tickets/{id}/status, /priority, /assignment. Mutations include expectedVersion.
GET /categories and staff-only /agents provide form lookups. OpenAPI generates the client contract.

## Frontend

Preserve the foundation page. Add login/register/reset routes and an authenticated workspace with customer tickets, staff queue/assignment filters, ticket creation/editing, conversation, internal-note composer, management controls, and audit history. Use TanStack Query, React Hook Form/Zod, semantic accessible primitives, explicit loading/empty/error/conflict states, and the generated API types. Render user content as plain text.

## Pull requests

Backend branch feat/m2-backend targets main and owns backend changes, migration/config, contract generation and backend tests. Frontend branch feat/m2-frontend is stacked on backend and targets it, owning frontend, Cypress, screenshots and frontend docs. Do not merge either PR automatically.

## Acceptance

A real customer registers/logs in, creates a ticket, and can follow an agent assignment/reply/resolution. Cross-customer reads and writes are denied; customer-facing data never contains staff notes. Refresh/logout/reset, validation, status transitions and concurrency have automated coverage. Run both app builds/tests, real PostgreSQL integration tests and Cypress; capture real UI screenshots and link them in the PRs. Report unavailable checks accurately.
