# Milestone 4 verification

## Local checks

- `dotnet restore apps/backend/TicketFlow.sln --locked-mode`: passed with no vulnerability warnings after pinning Newtonsoft.Json 13.0.4 for Hangfire's serializer dependency.
- Release solution build: passed, zero warnings and errors.
- `dotnet format apps/backend/TicketFlow.sln --verify-no-changes --no-restore`: passed.
- Unit tests: 46 passed.
- PostgreSQL/S3/API integration tests excluding the two Elasticsearch container tests: 54 passed.
- Hangfire storage bootstrap and worker processing were verified against a disposable PostgreSQL database. The manual smoke polling script had a quoting error; a separate query confirmed the outbox row completed before cleanup.

The two Elasticsearch tests could not pull `docker.elastic.co/elasticsearch/elasticsearch:8.19.4` because the local Docker engine could not connect to docker.elastic.co. Their source was unchanged. GitHub Actions runs the complete container workflow and provides the remaining integration evidence.

## Operational limits

Email delivery is at least once; an SMTP acceptance followed by a worker crash can lead to a duplicate. Reminder and escalation thresholds default to 24 and 48 hours for demonstration and are configurable. Hangfire storage must be explicitly prepared after the application migration. See the [jobs runbook](runbooks/milestone-4-jobs.md).
