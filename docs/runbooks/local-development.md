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
