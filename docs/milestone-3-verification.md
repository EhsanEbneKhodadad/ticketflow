# Milestone 3 verification

## Environment and isolation

Milestone 3 builds on the unmerged Milestone 2 frontend branch. Existing Milestone 2 PRs and its development database remain unchanged. The new local development database is ticketflow_m3 on the existing isolated PostgreSQL 17 cluster.

Docker Desktop is unavailable on this Windows host. Real S3 tests use official SeaweedFS 4.51 bound to loopback port 18333 with private bucket access. Elastic's official Windows package endpoints return 403 from this host, so actual Elasticsearch integration and search-browser checks run in GitHub Actions with the official Elasticsearch 8.19.4 image. No fake search service substitutes for these acceptance checks.

## Backend attachment evidence

The attachment slice passed 48 backend tests, including 20 PostgreSQL/HTTP/S3 integration tests and 28 unit tests. Two additional real-Kestrel URL-variant regressions were subsequently added. Independent review found and verified the route-size correction.

Coverage includes byte/name/size validation; owner/foreign/staff access; staff-only reply files; forced download headers and original bytes; missing CSRF; actual uploads larger than 64 KiB; oversized-body early rejection; optimistic concurrency and the tenth-file race; database rollback and S3 cleanup; private object access and sanitized outage errors. Migration and API contract checks use generated artifacts.

## Complete milestone checks

The combined backend passes 77 local tests with real PostgreSQL and S3, excluding only two real Elasticsearch tests that require the CI Docker runner. Release build, locked restore, C# formatting, generated frontend API types and TypeScript compatibility checks pass. The new search suite covers metadata/date filters, current-DB authorization/snippets/counts, bounded replay and index failures, and the advertised camel-case query enum contract. The same query compatibility defect on the existing ticket list was reproduced and corrected.

Two compiled Elasticsearch integration scenarios remain for GitHub CI: full DB/index/API projection and recovery, and the bounded candidate window with metadata filtering. Frontend/browser counts, CI links and selected screenshots are recorded after their verification. Neither Milestone 2 nor Milestone 3 PRs are merged automatically.
