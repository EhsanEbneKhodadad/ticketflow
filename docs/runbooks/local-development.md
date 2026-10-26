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
- Mailpit, SeaweedFS, and Elasticsearch are infrastructure only until their feature milestones.
