---
name: role-performance-load-review
description: Designs and runs load tests against realistic MusicLounge traffic shapes — ticket on-sale spikes, concurrent livestream+chat viewers — measuring rate/error/duration per the RED method rather than only average latency, and specifically stress-tests the concurrency-controlled resources (ticket holds, zone capacity) that are this system's most fragile point under real load. Covers role 09 (Performance Engineer) from the MusicLounge SDLC role charter. Use when asked to load test, check performance, find N+1 queries, or explicitly invoke the Performance Engineer role.
license: Internal project tooling — MusicLounge repository, no separate license.
---

# Performance Engineer — Load & Concurrency Review

Mandate: *"Đảm bảo hệ thống chịu đúng kịch bản tải thật — giờ mở bán vé đồng loạt, đêm diễn nhiều người xem livestream cùng lúc — không phải tải trung bình dễ chịu."*

Work in order: **(1) Identify realistic load shapes → (2) Concurrency race test → (3) RED metrics → (4) N+1/index check → (5) Propose SLOs.**

## 1. Identify realistic load shapes for this domain

Average steady-state traffic is not this system's risk. The risk is concentrated in two spike shapes specific to a ticketing + livestream platform:
- **On-sale spike**: many buyers hitting `POST /tickets/holds` for the same show within seconds of tickets going live.
- **Livestream concurrency**: many viewers connecting to `/hubs/livestream` and posting chat simultaneously during a show.

Design load scripts around these two shapes specifically, not a generic ramp.

## 2. Concurrency race test — the highest-value check this skill runs

This system already has proven-effective concurrency defenses (`IShowBookingLock`, filtered unique indexes) and a proven-effective way to verify them dynamically: fire genuinely parallel requests against a shared, finite, small-capacity resource and confirm the total granted never exceeds capacity.

Concretely: create a `SeatingZone` with a small capacity (e.g. 5), create two `TicketTier`s both pointing at it, then fire concurrent `POST /tickets/holds` requests from multiple simulated users summing to more than capacity. Confirm the system grants exactly up to capacity and rejects the rest — including at the exact boundary (requests summing to exactly capacity must succeed; capacity+1 must fail). A sequential loop of requests does not exercise the real race window; the requests must actually overlap in time.

## 3. Measure the RED metrics, not just average latency

For each load shape: **Rate** (requests/sec sustained), **Error rate** (% non-2xx, broken out by status code — a spike in 409/422 under load may be correct backpressure, not a bug, so classify before alarming), **Duration** at p50/p95/p99 (not average — a system with a fine average and a terrible p99 still produces a bad experience for a meaningful fraction of real buyers at the exact moment that matters most, on-sale).

## 4. Check for N+1 queries and missing indexes under realistic data volume

Enable EF Core query logging, run a representative flow (browse shows → view detail → hold ticket → checkout) against a database seeded with realistic volume (hundreds of shows/tickets, not a handful), and count queries issued per logical request. A query count that scales with result-set size instead of staying constant is an N+1. Cross-reference any slow query against `role-db-integrity-review`'s index-coverage findings.

## 5. Propose SLOs

Turn the measured p95/p99 into a proposed SLO (e.g., "p95 ticket-hold latency < Xms under N concurrent on-sale requests") that Ops can monitor against going forward — a load test that produces a number nobody uses afterward for alerting has limited lasting value.

## Report

State the load shape tested, the concrete script/parameters used (so it's re-runnable), the RED numbers observed, and any concurrency-boundary result explicitly (granted count vs. capacity, at and around the boundary).

## Quick reference

| Need | Go to |
|---|---|
| The exact 4-tier quota check this system relies on (price → tier → zone → show) | `src/MusicLounge.Application/Tickets/Commands/HoldTicket/HoldTicketCommandHandler.cs` — `ValidateQuotaAsync` |
| Locking primitives already in place | `IShowBookingLock`, `IAsyncKeyedLock` in `src/MusicLounge.Infrastructure/Services/` |
| Account/environment setup for dynamic load scripts | `role-enduser-uat-dynamic` skill's `references/dynamic-testing-playbook.md` |
