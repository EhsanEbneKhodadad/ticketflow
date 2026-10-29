# ADR 0007: Bind guarded requests to the initiating account

## Context

Browser login cookies are shared across tabs, while React Query state belongs to each tab. A login in another tab can change the authenticated account while the first tab still displays its previous account. Checking only cached identity, or checking identity only after a 401 refresh, cannot stop an initially successful request executing under the new account.

## Decision

Guarded attachment/search/look-up requests carry the initiating UserView.id in X-TicketFlow-User. API middleware compares this optional precondition with the authenticated actor before CSRF/form parsing and endpoint execution. A malformed or repeated value returns 400; a different account returns 412 Problem Details with code account_changed and no private identity data.

The client does not replay the request. It clears this tab's stale private state and presents a sign-in notice, while preserving the other account's server session. Same-account cookie renewal and cross-tab refresh coordination remain supported.

## Consequences

The header is a client precondition, not authentication or a replacement for resource authorization/CSRF. Existing clients without it keep normal cookie-based behavior. The new guarded UI binds its initial requests as well as renewal retries. No token is exposed or stored in browser storage.

Actual Kestrel/PostgreSQL tests reproduce the previous wrong-account 201 and verify rejection without storage/version/audit changes. The real browser regression changes shared cookies without updating the old tab and confirms 412, no upload replay, cleared file state and unchanged ticket data. See [verification](../milestone-3-verification.md).
