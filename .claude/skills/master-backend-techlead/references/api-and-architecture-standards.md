# API Design, Architecture & Resilience Standards

## API design
- **REST resource modeling** — URLs represent resources (nouns), HTTP verbs represent actions; check for verbs leaking into paths (`/getUserOrders` instead of `GET /users/{id}/orders`) as a smell, though pragmatic exceptions exist (search, bulk actions).
- **Consistent error contract** — errors should return a consistent shape (status code + machine-readable error code + human message) across the whole API, not ad-hoc per endpoint — inconsistency here is what makes client-side error handling fragile.
- **Versioning strategy exists and is explicit** — check the project has *a* strategy (URL version, header version, etc.) rather than breaking changes shipping silently into a "stable" endpoint.
- **Pagination, filtering, and rate limits are explicit contracts**, not accidental behavior of whatever the DB query happened to return.
- For current framework-specific idiom (e.g., what's currently recommended for a given language/framework's API layer), search rather than rely on memory — this shifts version to version.

## The Twelve-Factor App (still a reasonable baseline, verify nothing supersedes it for the stack in question)
Config in environment (not in code), explicit and isolated dependencies, backing services treated as attached resources (swappable without code change), strict separation of build/release/run stages, stateless processes (session state doesn't live in-process memory unless explicitly designed for it), port-binding for service exposure, concurrency via horizontal process scaling, fast startup/graceful shutdown ("disposability"), dev/prod parity, logs treated as event streams (not managed files the app writes and rotates itself), admin/one-off tasks run as one-off processes against the same codebase.

## Observability (check for all three, not just logs)
- **Logs** — structured (not free-text), with correlation/trace IDs threading through a request across services.
- **Metrics** — request rate, error rate, and latency (the "RED" method) at minimum, per service and ideally per critical endpoint.
- **Traces** — distributed tracing across service boundaries for anything with more than one hop, so a slow request can be attributed to the actual slow component instead of guessed at.
- OpenTelemetry is the current vendor-neutral standard for instrumenting logs/metrics/traces — verify current adoption/version guidance for the stack in question before recommending specific instrumentation.

## Resilience patterns
- **Timeouts** on every network call, tuned per-dependency (not one global timeout for everything).
- **Retries with backoff and jitter**, and only for operations that are actually safe to retry (idempotent) — retrying a non-idempotent operation blindly is itself a failure mode.
- **Circuit breaker** to stop calling a dependency that's clearly failing, instead of queuing up requests against it and making the outage worse.
- **Bulkheads** — isolate resource pools (threads, connections) per dependency so one slow/failing dependency can't starve resources needed for unrelated requests.
- **Graceful degradation** — define what the system does when a non-critical dependency is unavailable (serve stale/cached data, disable a feature) rather than failing the whole request.

## Database and data-layer standards
- Transactions used where multi-step consistency actually matters — but check that transaction scope isn't so wide it holds locks across slow external calls.
- Indexes exist for the query patterns actually used in production, not just the primary key — check for missing indexes on foreign keys and frequently-filtered columns.
- Migrations are backward-compatible with the currently-deployed code version during a rolling deploy (additive changes first, cleanup in a later deploy).
- Sensitive data (PII, credentials, payment info) encrypted at rest and in transit per current standard for the data classification and jurisdiction involved — verify current requirement, don't assume.
