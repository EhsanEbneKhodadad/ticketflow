# Milestone 5: dashboards and administration

## Scope

Add truthful ticket dashboards, role-protected workspace administration, accessible interfaces, and a portfolio case study. This milestone builds on the completed notification and job milestone. No screenshot deliverable is required.

## Dashboard

`GET /api/v1/dashboard` returns live counts grouped by ticket status, unassigned, and high/urgent priority. Customers see only their own tickets; support staff see the whole workspace. Counts include every creation date and closed tickets. The API aggregates directly from PostgreSQL and exposes no customer details or message content.

## Administration

`/admin` and `/api/v1/admin/*` are restricted to administrators. Admins can list and create/edit categories and change user roles. Categories are not deleted, preserving ticket references. Role updates cannot self-demote or remove the final administrator; active tickets must be reassigned before a staff member becomes a customer. A PostgreSQL advisory transaction lock serializes administrator-set changes, and the security stamp is rotated to invalidate sessions after a role change.

## Quality

Frontend routes remain thin and feature components handle dashboard and admin behavior. API shapes come from generated OpenAPI types. Interfaces expose loading, error and empty states, responsive layouts, semantic labels, and keyboard-operable controls. The Cypress journey exercises customer data scope and an administrator category update. The case study describes the local/CI system and its operational limits without claiming a production deployment.

## Verification

See [Milestone 5 verification](milestone-5-verification.md) for exact commands and results after the final integration run.
