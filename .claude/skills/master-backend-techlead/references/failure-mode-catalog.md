# Failure Mode Catalog

Concrete, real-world failure patterns organized by the four lenses. Use as a checklist while reviewing — don't just look for "bugs" in the abstract.

## Functional failure modes
- **Boundary values ignored** — off-by-one on pagination, empty-list vs. null handled differently than a populated list, zero/negative quantities not validated.
- **Idempotency missing** — an operation (payment, order creation, email send) that isn't safe to retry produces duplicates when the client retries after a timeout (which clients *will* do).
- **Partial failure mid-operation** — a multi-step process (charge card → create order → send confirmation) has no compensation/rollback if step 2 fails after step 1 succeeded; check for the "half-done" state explicitly.
- **Concurrency races** — two requests modifying the same resource at once (classic: two "last item in stock" purchases both succeed) without a lock, version check, or atomic operation.
- **Time-zone and locale bugs** — dates stored/compared without explicit timezone handling; assumes server locale matches user locale for formatting/parsing.
- **Silent truncation/precision loss** — money handled as float instead of a decimal/integer-minor-units type; string truncated at a DB column limit without validation or error.

## Business-logic failure modes
- **Ticket-literal implementation** — code does exactly what the ticket said in shorthand, but the ticket didn't capture an exception case that always exists in the real process (e.g., "cancel order = refund" ignores that some real businesses forfeit deposits on late cancellation).
- **State machine gaps** — the business has more real states than the code models (e.g., only "confirmed" and "cancelled" exist in code, but real operations need "pending confirmation," "no-show," "partially fulfilled").
- **Wrong source of truth** — a value that the business actually derives from one authoritative process is instead recalculated or duplicated in another place, and the two drift apart over time (classic: inventory count maintained in two systems that desync).
- **Missing real-world exception handling** — discounts, promotions, or special-case pricing that real staff apply manually aren't representable in the system, so staff work around it in ways that don't get recorded (breaks reporting/reconciliation later).
- **Reporting assumes 100% digital capture** — financial/operational reporting logic assumes all activity flows through the system, when in reality some of it (cash, manual overrides, phone orders) doesn't — this silently corrupts any report or AI model trained on "complete" data that isn't.

## Technology/infrastructure failure modes
- **N+1 queries** — a loop that issues one DB query per item instead of one batched query; invisible at small scale, catastrophic at production scale.
- **Missing timeouts** — a call to a downstream service/DB has no timeout, so one slow dependency hangs the whole request chain (and can exhaust the connection pool, taking down unrelated requests).
- **No circuit breaker / retry storm** — a failing downstream service gets hammered by naive retries from every caller simultaneously, turning a partial outage into a total one.
- **Connection pool exhaustion** — connections aren't released on error paths (only on the happy path), so errors slowly starve the pool until the service falls over.
- **Unbounded resource usage** — no pagination limit, no request size limit, no rate limit — a single bad actor or bad client bug can exhaust memory/CPU/bandwidth.
- **Cache invalidation gaps** — cache is written on read but never explicitly invalidated on write, so stale data silently persists past its real freshness window.
- **Migration/schema-change risk** — a schema change or deploy isn't backward-compatible with the previous version running during a rolling deploy, causing errors during the deploy window itself.
- **Config/secret drift between environments** — logic that works in dev fails in prod because of an environment-specific assumption (file path, service URL, feature flag default) that isn't parameterized.

## Operational / observability failure modes
- **No structured logging / correlation ID** — an error in production can't be traced back to the specific request/user/transaction that caused it.
- **Logs without alerting** — errors are logged but nothing pages anyone; the team finds out from a customer complaint instead of monitoring.
- **No health/readiness distinction** — a service reports "healthy" even when a critical dependency (DB, queue) is down, so the load balancer keeps sending it traffic it can't serve.
- **Metrics that don't reflect user experience** — CPU/memory dashboards exist but nothing measures actual request latency/error-rate as experienced by the end user (the thing that actually matters for "best experience for end user").
- **No rollback plan** — a deploy has no fast, tested path back to the previous version if something goes wrong in production.
- **Silent degraded mode** — when a non-critical dependency fails, the system should degrade gracefully (e.g., show cached recommendations instead of none) — check whether it does, or whether it just breaks the whole request.

## How to use this catalog in review
For each area of code under review, walk down the relevant sub-list and explicitly check it off (mentally or in the written report) rather than reviewing free-form — free-form review reliably misses categories the reviewer isn't already primed to think about, which is exactly how experienced engineers still get surprised in production.
