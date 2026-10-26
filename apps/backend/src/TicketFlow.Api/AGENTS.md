# API host rules

- Inherit repository and backend rules.
- Configure hosting and middleware here; place use cases outside the host.
- /health/live checks the process only; /health/ready checks required dependencies and applied migrations.
- Keep OpenAPI available only in Development/Testing until a production exposure policy is implemented.
- Use global Problem Details handling. Never expose stack traces or credentials in responses.
- Protect all ticket resources by authenticated role and ownership in Milestone 2.
- Keep migration/seeding commands separate from normal startup and fail on invalid flag combinations.
