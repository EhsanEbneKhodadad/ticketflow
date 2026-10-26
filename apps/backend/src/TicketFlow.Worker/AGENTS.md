# Worker rules

- Inherit repository and backend rules.
- The foundation worker is a host shell; do not claim notifications or jobs are implemented.
- Execute application use cases through DI scopes; never call API endpoints to run a job.
- Propagate host cancellation and log lifecycle events without secrets.
- Before adding jobs, define idempotency keys, bounded retries, failure visibility, and replay behavior.
- Use PostgreSQL-backed Hangfire storage in the automation milestone; add no broker by default.
- Never perform migrations or development seeding during worker startup.
