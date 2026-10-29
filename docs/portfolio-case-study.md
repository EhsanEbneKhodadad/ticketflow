# TicketFlow: support-workflow portfolio case study

TicketFlow is a modular support workspace for customers and support teams. Customers submit requests and exchange public replies; staff manage ownership, priorities, status and internal notes. Administrators manage categories and user roles, while dashboards show counts from the current database. The project is a resume demonstration, not a claim of a deployed commercial service.

## Architecture

A pnpm monorepo hosts a Next.js App Router interface and an ASP.NET Core modular monolith, with a separate background worker. Domain rules and application workflows remain independent of database and transport implementations. PostgreSQL is authoritative. A generated OpenAPI contract supplies frontend TypeScript types. An S3-compatible store holds private attachments, and Elasticsearch provides a derived index of public ticket content.

Authentication uses HttpOnly cookies, session revocation and antiforgery validation. The API checks ticket ownership and roles for every relevant operation. File downloads require authorization; internal notes and private files are excluded from public search. The frontend also sends the initiating-account precondition for protected requests to handle shared-cookie account switches.

## Engineering decisions

Database migrations and development seed data run explicitly, keeping normal startup free from schema changes. Search can lag writes and is unavailable when Elasticsearch is disabled; browsing tickets remains available. Local infrastructure uses Docker Compose, PostgreSQL, SeaweedFS, Mailpit and optional Elasticsearch. The worker and notification guarantees are described in the milestone 4 operational documentation.

## Quality and evidence

The repository contains unit and integration tests, frontend behavior tests, browser journeys, type checking and CI. See milestone verification documents for exact results and environment limitations. Dashboards report real database counts rather than demonstration numbers. Public registration creates customer accounts; administrator capabilities are enforced by the API, not merely hidden in the interface.

## Scope and next steps

This is local-development and CI software. Cloud hosting, production credentials, monitoring and backups belong to the deployment milestone. Do not claim exactly-once email delivery or security certifications; evaluate production requirements separately.
