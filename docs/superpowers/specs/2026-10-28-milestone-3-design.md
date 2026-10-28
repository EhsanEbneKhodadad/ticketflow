# Milestone 3: attachments and authorized search

## Intent and scope

Implement roadmap Milestone 3 on the unmerged Milestone 2 implementation. Preserve the monorepo, Clean Architecture, same-origin cookie/CSRF security, and scoped AI rules. Deliver tested backend and frontend PRs with screenshots; do not merge Milestone 2 or 3.

## Approach and implementation decisions

Use the existing S3-compatible SeaweedFS development service through the official AWS S3 SDK, private object keys, and API-mediated authorized downloads. Direct browser presigned uploads would require an additional completion protocol and orphan lifecycle; local filesystem storage would not exercise the planned S3 boundary. API-mediated uploads are the selected implementation default.

Attachments belong to a ticket, optionally an existing reply. Reply visibility determines attachment visibility; internal-note attachments and metadata are staff-only. Ticket-level attachments are public to that ticket's authorized participants. Accept PDF, PNG, JPEG, and UTF-8 text only, validate bytes as well as extension, maximum 10 MiB/file and 10 files/ticket. Bound multipart bodies, reject empty/unsafe files, sanitize display names, randomize private keys, and force attachment download with nosniff. Uploads enforce current expectedVersion, reject closed tickets, and audit changes. Metadata and ticket version save atomically; delete uploaded objects if persistence fails. No user-facing deletion, previews, content extraction, public buckets, or malware-scanning claims.

Elasticsearch indexes ticket number/title/description/tags and public replies only. Internal notes, internal attachment names, credentials, and reset data never enter the index. PostgreSQL stays authoritative. The existing worker reconciles the derived index in bounded batches periodically; idempotent document IDs and repeated full reconciliation recover missed updates and index loss without introducing Milestone 4's transactional outbox/Hangfire. Reconciliation failures are visible in logs and retried next cycle; disabled or unavailable search returns a clear 503 and does not disable the ticket workflow. A durable outbox remains Milestone 4.

Search supports text, ticket number, status, priority, category, assigned agent, inclusive creation-date bounds, relevance/newest sort, bounded pagination, and text highlighting rendered safely as React text/mark nodes. Scope candidates by ownership in Elasticsearch and recheck current authorization and filters in PostgreSQL before constructing results. Never hydrate or highlight directly from untrusted indexed source. For this milestone all roles search public ticket content; internal-note search is excluded intentionally. Backend filters must handle stale indexed ownership/status safely without leaking inaccessible counts or snippets.

## Branches and verification

Retain the existing in-place workflow (Milestone 1 ruling: no extra worktree needed). Create feat/m3-backend from feat/m2-frontend, PR base feat/m2-frontend; create feat/m3-frontend from feat/m3-backend, PR base feat/m3-backend. Review chain: M2 backend -> M2 frontend -> M3 backend -> M3 frontend. No merges.

Behavior tests cover content/size validation, ownership, internal attachment privacy, download bytes and headers, CSRF, concurrency and upload cleanup; real PostgreSQL/S3/Elasticsearch integration checks cover adapter behavior, search updates/rebuild/filters and stale authorization. Add browser journeys for upload/download, public reply attachments, internal privacy and full-text filters/highlighting. Run existing suites, formats, builds, generated-contract verification and Compose CI. Capture genuine screenshots from successful browser flows. No cloud deployment or paid services.
