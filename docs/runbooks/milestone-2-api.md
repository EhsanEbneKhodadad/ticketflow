# Milestone 2 API

## Database and configuration

Apply migrations through the existing explicit --migrate command. Roles are migration data, so public registration always creates a Customer even without demo seeding.

Set Seed**Enabled=true, Development environment, and Seed**DemoPassword to a strong local password, then run --migrate --seed to create customer@ticketflow.local, agent@ticketflow.local and admin@ticketflow.local. Compose maps DEMO_PASSWORD from .env.example to the one-shot seed service. Existing accounts and passwords are not overwritten by repeated seed runs. Never enable demo seeding on a public deployment.

## Authentication

GET /api/v1/auth/csrf returns a request token and sets an HttpOnly antiforgery cookie. Include X-CSRF-TOKEN on every state-changing API request, including registration and login. Fetch a new token after identity changes. Access/refresh cookies are HttpOnly, host-only and Secure outside Development/Testing. No tokens are returned in public response bodies.

Access JWTs last 15 minutes. Refresh tokens are stored hashed, rotate after use, and expire after seven days. Reuse revokes the durable session family. Every authenticated request checks durable family state and the current Identity security stamp. Logout and reset therefore invalidate already-issued access tokens as well. Production requires a private Auth\_\_SigningKey of at least 64 characters. Persist ASP.NET data-protection keys before multi-instance deployment.

## Password reset

POST /auth/forgot-password always returns a generic accepted response. Local SMTP defaults to localhost:1025; Compose sets Mail**Host=mailpit. Mailpit UI is localhost:8025. App**PublicUrl controls the reset-link origin; links expire after one hour and are single-use. Mail delivery failures are logged without exposing the account in the HTTP response. Background notification jobs remain Milestone 4.

## Tickets

Customer queries and writes are ownership-scoped. Hidden tickets return 404. Staff can assign, prioritize and resolve; customers close resolved tickets or reopen their own. Mutations require expectedVersion; stale or concurrent updates return 409 without overwriting newer work. Closed tickets reject replies. Internal note bodies and audit events are removed from customer responses. Lists support bounded pagination, title/number matching, status, priority, assignment and unassigned filters. Elasticsearch and attachments are deferred.

## Tests

Default database tests use PostgreSQL Testcontainers. To use an existing disposable test server, set TICKETFLOW*TEST_DATABASE to an administrative PostgreSQL connection. Each test creates a generated ticketflow_test*<guid> database and drops only that database afterward. Do not point this at production. The session-race regression deliberately controls row locks so it reproduces the refresh/logout race deterministically.

## Verification

The backend suite passes 30 tests (17 unit and 13 PostgreSQL/HTTP integration tests). This includes the complete ticket lifecycle, cross-customer denial, internal-note filtering, CSRF rejection, privilege-safe registration, rotating refresh reuse, logout/reset revocation, deterministic refresh/logout concurrency, and migration from an already populated legacy session table. Tests ran on isolated PostgreSQL 17 databases; CI defaults to Testcontainers.

The durable-family migration explicitly revokes legacy refresh families before adding its foreign key. Upgrading an existing development database therefore requires signing in again, without discarding accounts or tickets.
