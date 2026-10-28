# Local development

Run commands from the repository root. Native and container apps cannot share ports 3000/5080.

## Native environment

Set these in each backend PowerShell terminal (defaults match .env.example):

```powershell
Set-Item Env:ConnectionStrings__Database 'Host=localhost;Port=5432;Database=ticketflow;Username=ticketflow;Password=ticketflow_local_only'
Set-Item Env:DOTNET_ENVIRONMENT Development
Set-Item Env:ASPNETCORE_ENVIRONMENT Development
Set-Item Env:Seed__Enabled true
```

.env is read by Compose, not automatically by dotnet run.

## Troubleshooting

- If dockerDesktopLinuxEngine is unavailable, start Docker Desktop and verify Linux containers and WSL. Do not reset Docker or delete data to fix a connection failure.
- /health/ready returns 503 when PostgreSQL is unavailable or migrations are pending; /health/live remains independent.
- Inspect docker compose logs migrate api worker and docker compose ps.
- Editing .env does not update credentials in an existing PostgreSQL volume.
- In restricted shells, place NUGET_PACKAGES, NUGET_HTTP_CACHE_PATH, NUGET_PLUGINS_CACHE_PATH, NUGET_SCRATCH, and DOTNET_CLI_HOME in a writable project-local cache.
- MSBuildEnableWorkloadResolver=false and single-node builds (-m:1 -nr:false) avoid Windows service probing in restricted shells. No optional .NET workloads are required.
- pnpm --store-dir .cache/pnpm-store --cache-dir .cache/pnpm-cache install keeps package metadata inside the workspace.
- Elasticsearch is optional through --profile search and requires extra RAM.
- SeaweedFS stores private attachments. Its bucket must exist before uploading; Compose mini creates `ticketflow-attachments`. Mailpit captures password-reset mail.

## Attachment storage

Compose supplies the API/worker `Storage__Endpoint`, `Storage__Bucket`, `Storage__Region`, `Storage__AccessKey`, and `Storage__SecretKey` from the local environment. For native API development, set `Storage__Endpoint=http://localhost:8333`, `Storage__Bucket=ticketflow-attachments`, `Storage__Region=us-east-1`, and copy the local S3 access and secret values from `.env` into `Storage__AccessKey` and `Storage__SecretKey`. Dotnet does not load `.env` automatically. With no explicit credentials, the AWS SDK uses its standard credential chain.

Apply the attachment migration with the explicit `--migrate` bootstrap command before use. Keep the bucket private; clients download through authorized API endpoints. Storage outages return 503 and do not affect non-storage ticket operations. An object-cleanup failure logs an operational error and requires reconciliation of unreferenced private objects.

Attachment integration tests default to PostgreSQL and SeaweedFS Testcontainers. Existing native services may be selected with `TICKETFLOW_TEST_DATABASE` (admin connection; tests create/drop unique databases) and `TICKETFLOW_TEST_S3_ENDPOINT`. Optional `TICKETFLOW_TEST_S3_ACCESS_KEY` and `TICKETFLOW_TEST_S3_SECRET_KEY` override the local sample credentials. S3 tests create/delete only their own random object keys in the existing `ticketflow-attachments` bucket; they never delete that bucket.

## Public ticket search

Search is disabled by default. To enable it in Compose, set `SEARCH_ENABLED=true` in `.env`, then run `docker compose --profile app --profile search up -d --build --wait`. The profile supplies Elasticsearch 8.19.4 and Compose configures both API and worker to use `http://elasticsearch:9200`. The API has no Elasticsearch readiness dependency: ticket operations stay available while search initializes or retries.

For native API and worker terminals, set these in **both** terminals and run the worker alongside the API:

```powershell
Set-Item Env:Search__Enabled true
Set-Item Env:Search__Endpoint http://localhost:9200
Set-Item Env:Search__Index ticketflow-public-v1
Set-Item Env:Search__IntervalSeconds 15
```

The interval accepts 1–3600 seconds. The configured index name must be lowercase letters/digits with optional hyphens/underscores, at most 100 characters. Endpoint URLs accept HTTP/HTTPS without embedded credentials. This local Elasticsearch service has security disabled and must stay loopback-bound.

The worker immediately reconciles the public projection, then waits the configured interval after each cycle. Each replay captures the latest ticket number and reads batches of at most 100 tickets through that bound, replacing documents by ticket ID. It bootstraps a missing index with explicit strict mappings. Repeated replay repairs missed updates and index loss; a failed cycle logs its failure type and retries from the beginning after the interval. Host cancellation stops database and HTTP work. Neither host performs migrations. Durable jobs/outbox remain a later milestone.

`GET /api/v1/search/tickets` requires authentication. Optional query parameters: `q` (max 160 characters), `page` (default 1), `pageSize` (default 20; 1–100), `status`, `priority`, `categoryId`, `agentId`, `from`, `to`, `sort` (`relevance` default or `newest`). Search and ticket-list enum filters accept the generated camel-case values; invalid or duplicate enum filters return 400. Dates use ISO `YYYY-MM-DD` and include the complete UTC creation day; `from` must not exceed `to`. A page's starting offset must be below 1000. Blank queries list candidates subject to authorization and filters. Both `TKT-1001` and `1001` match a ticket number.

The response is `TicketSearchPage`: `items` contain `ticket` (the existing ticket list DTO) and `snippet` segments `{ text, highlighted }`; `total`, `page`, `pageSize`, and `candidateLimit` accompany the page. Render each segment as React text, using a `mark` element for highlighted text. No indexed source, HTML, or raw Elasticsearch total reaches the browser. Snippets use current PostgreSQL public content and are bounded to 240 characters plus optional ellipses.

**Fixed candidate window:** Elasticsearch returns at most 1000 IDs in relevance/newest order, scoped to the requesting customer when applicable and prefiltered by status, priority, category, agent, and creation dates. PostgreSQL rechecks current ownership, all filters, and public content before totals, snippets, or pagination. `total` counts only authorized hydrated candidates within that window; it is not a global exact match count. Stale ownership/status can reduce a page's results without leaking foreign counts; recently edited/new tickets and reclaimed ownership can remain absent until replay. Relevance can reflect the last replay until indexing catches up. All roles search public replies only. Internal notes, attachment names/bytes/keys, account secrets, and audit data are excluded.

The worker installs/reinstalls `ticketflow-search-{Search:Index}` before index bootstrap and each nonempty bulk write. Its only index pattern is the exact configured index, with priority 500 and the same strict mappings/settings as explicit creation. This protects auto-creation if the index disappears after a check or between batches; replay then restores its documents. No cluster-wide auto-create setting is modified. A security-enabled Elasticsearch principal requires `manage_index_templates` in addition to the existing index read/write/create/refresh privileges; a template permission/acknowledgement failure stops writes and is retried next cycle. Avoid higher-priority templates conflicting with this exact index. Templates affect future creation only: for an already incorrectly mapped index, configure a fresh index name in both API and worker and allow replay to rebuild it. TicketFlow never automatically deletes an existing index.

Disabled, missing-index, failed/partial Elasticsearch responses, and timeouts return generic `503` Problem Details. Browse `/api/v1/tickets` while search is recovering. Check `docker compose logs worker` for reconciliation failures. If a search index is deliberately removed, the next complete worker cycle recreates it; PostgreSQL is authoritative. Do not delete persistent volumes to fix search availability.

Real search integration tests default to a disposable official Elasticsearch 8.19.4 Testcontainer and unique test-owned indexes. Set `TICKETFLOW_TEST_ELASTICSEARCH=http://localhost:9200` to use an existing local Elasticsearch instance; the tests delete only their own random indexes and their corresponding `ticketflow-search-{index}` templates. `TICKETFLOW_TEST_DATABASE` selects the native PostgreSQL admin connection. Run `dotnet test apps/backend/TicketFlow.sln --filter Category=Search` for these tests. If Elasticsearch/Docker is unavailable, `--filter "Category!=Search"` runs the remaining suite using native PostgreSQL/S3 overrides; this does not verify Elasticsearch.
