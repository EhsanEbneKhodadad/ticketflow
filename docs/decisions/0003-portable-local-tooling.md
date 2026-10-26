# ADR 0003: Portable local tooling

Status: Accepted during Milestone 1 verification.

## Local object storage

The proposed MinIO image and public repository returned 404 during registry checks on 2026-10-27. Use the verified chrislusf/seaweedfs:4.51 image for local S3-compatible storage. Authentication is configured through development AWS-style access keys, and the attachment bucket is pre-created. No attachment adapter exists until Milestone 3; its boundary remains S3-compatible.

Reference: https://github.com/seaweedfs/seaweedfs#quick-start

## JavaScript tooling

Windows restrictions block symlinks and hard links in this execution environment. Use pnpm hoisted dependencies, copied package imports, injected workspace packages, and disabled injected-dependency deduplication.

Run API generation through the client package, then copy its package entry and generated declarations to the installed package. The explicit synchronization script uses copies rather than pnpm's hard-linking sync hook. Web type checking imports the installed client, so incomplete package copies fail verification.

Next production builds use the supported webpack bundler. Native build output directory creation can still be blocked intermittently by this sandbox. The container build is the clean-environment check and is configured in CI.

Reference: https://pnpm.io/workspaces/

## Consequences

The S3-compatible application boundary is preserved. Local tooling uses more disk space to avoid Windows link privileges. Generated contracts must be refreshed with the root pnpm api:generate command, which includes installed-copy synchronization.
