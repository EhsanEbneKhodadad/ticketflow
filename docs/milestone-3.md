# Milestone 3: attachments and search

## Scope

Add private ticket/reply attachments and Elasticsearch-backed full-text search on the complete Milestone 2 workflow. Preserve scoped frontend/backend AI rules and Clean Architecture. No Milestone 2 PR is merged by this work.

Attachments use the S3-compatible adapter and PostgreSQL metadata. Files attach to the original request or an existing reply; internal replies produce staff-only attachments. Accepted formats are PDF, PNG, JPEG, and UTF-8 text, up to 10 MiB each and 10 files per ticket. The API validates bytes, enforces authorization and current ticket version, records audit events, and forces authorized downloads as attachments. Object keys and internal metadata never become public URLs. Format validation is not malware scanning.

Search covers ticket number, title, description, tags, and public replies. Internal notes and private filenames are excluded for every role. Filters include status, priority, category, agent, creation-date range and relevance/newest ordering. Elasticsearch is a derived index; PostgreSQL rechecks current ownership and filters and supplies current display text. The worker periodically reconciles the index in batches and can rebuild an empty index. Search may lag writes; tickets remain available when search is disabled or unavailable.

## Review order

1. Milestone 2 backend PR #1 -> main.
2. Milestone 2 frontend PR #2 -> feat/m2-backend.
3. Milestone 3 backend branch feat/m3-backend -> feat/m2-frontend.
4. Milestone 3 frontend branch feat/m3-frontend -> feat/m3-backend.

After each preceding PR merges, retarget its dependent PR to main and verify the diff and CI. The app-specific stacked PRs avoid repeating prerequisite changes. Leave every PR unmerged until the user reviews it.

## Deferred work

Transactional outbox, Hangfire notifications/reminders, file-content extraction, attachment previews/deletion, internal-note search, dashboards, admin-management screens and cloud hosting remain outside Milestone 3. Reconciliation is a recoverable derived projection, not a claim of durable background-job delivery.

## Implementation sources

- [Design](superpowers/specs/2026-10-28-milestone-3-design.md)
- [Implementation plan](superpowers/plans/2026-10-28-milestone-3.md)
- [Private attachment architecture](decisions/0005-private-attachments.md)

See [verification and screenshots](milestone-3-verification.md): 89 backend tests, 32 frontend tests and 7 production-stack browser journeys pass. The separate [backend PR #3](https://github.com/EhsanEbneKhodadad/ticketflow/pull/3) and [frontend PR #4](https://github.com/EhsanEbneKhodadad/ticketflow/pull/4) remain unmerged.
