# Dashboard and administration access

Status: accepted for Milestone 5.

Dashboard counts are derived from authoritative ticket records at request time. Customers see only their own tickets; support staff see all. Counts include all statuses, including closed tickets. High priority includes both High and Urgent. No customer identifiers or internal messages are exposed in these aggregates.

Only administrators can list or edit categories or change account roles. Categories have stable IDs and are not deleted by these endpoints, so existing ticket foreign keys remain valid. Slugs are normalized by validation, with a unique database constraint and conflict response for duplicates.

Role changes serialize on a PostgreSQL transaction advisory lock, protect the last administrator, prohibit self-demotion, and reject conversion of a staff member to customer when they still own an active assignment. Successful role changes rotate the Identity security stamp inside the transaction, invalidating prior access and refresh sessions on their next request. This does not cancel requests already in progress. Operators should reassign active tickets before demoting an agent.
