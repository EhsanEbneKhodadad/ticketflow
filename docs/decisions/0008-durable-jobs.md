# Durable ticket outbox and worker

Status: accepted for Milestone 4.

Each ticket audit entry creates an outbox record through the EF context while the ticket is saved, so both changes commit or roll back together. The outbox contains no message bodies or attachment names. The worker polls through Hangfire recurring jobs, claims with PostgreSQL row locking and expiring leases, retries bounded failures, and permits explicit operator replay. SMTP delivery remains at least once because the email server and database do not share a transaction. The existing periodic search reconciliation remains necessary for rebuilding an empty index.

Reminder/escalation events use a unique ticket/version/kind key. The current ticket status and version are rechecked at delivery to avoid acting on stale schedules. Hangfire schema initialization is an explicit operator step rather than a normal host startup side effect.
