---
name: role-ba-architecture-review
description: Audits whether implemented business rules and system architecture actually match documented/intended requirements — traces every business-relevant number in the code back to a real decision source (not a guess), checks specification quality (testable Given/When/Then acceptance criteria, a real Definition of Ready/Done, risk-based prioritization), verifies Clean Architecture layering isn't violated, audits observability instrumentation (logs/metrics/traces per the RED method), checks resilience patterns (timeout/retry/circuit-breaker) on every external dependency, and confirms architecture decisions are recorded rather than living in one person's head. Covers role 01 (Business Analyst / Product Owner) and role 03 (Solution Architect) from the MusicLounge SDLC role charter. Use when asked to check requirement traceability, review acceptance-criteria quality, audit whether code matches business intent, review architecture or observability conformance, or explicitly invoke the BA or Architect role.
license: Internal project tooling — MusicLounge repository, no separate license.
---

# Business Analyst & Solution Architect Review

Two roles bundled into one skill because they ask the same underlying question from two angles: **does the system actually encode what was decided, and is that decision traceable?** BA checks whether business rules are real decisions, not guesses. Architect checks whether the structure holding those rules is sound. Run both in one pass — they read the same code.

Mandate: *"Đảm bảo mọi tính năng phản ánh đúng cách một phòng trà ca nhạc thật vận hành tại Việt Nam — không dịch sai nghiệp vụ thành logic kỹ thuật, không tự suy đoán số liệu; kiến trúc đúng quy mô thật, không quá tay cũng không thiếu."*

Work in order: **(1) Trace business numbers → (2) Audit specification quality → (3) Check layering → (4) Audit observability → (5) Audit resilience patterns → (6) Check decision records → (7) Report.**

## 1. Trace every business-relevant number to a real source

Grep the Application layer for numeric literals that look like business decisions — percentages, day counts, hour counts, currency amounts, quotas — appearing directly in a handler rather than read from `ISystemConfigService`. For each one found:
- Is it a genuinely fixed technical constant (retry count, buffer size) — fine as-is.
- Is it a business-tunable number hardcoded where `system_config` was the established pattern for exactly this kind of value? Flag it — this repo's own precedent (`donation_performer_share_rate`, `platform_commission_rate`, etc., 11 keys total) means a new hardcoded business number is a regression, not a stylistic choice.
- For every business rule enforced in code (subscription caps, SLA windows, publish-lead-time requirements), confirm there's a traceable source: a code comment citing a law/decree, a commit message citing a benchmark, or a memory/doc citing a stakeholder decision. A rule with no traceable source is a finding — not because it's necessarily wrong, but because nobody can defend it under audit.

## 2. Audit specification quality

This is a distinct check from *number* traceability — it's about whether the requirements process itself produces defensible artifacts:
- Every user story/feature has Acceptance Criteria in an unambiguous, testable form (Given/When/Then or equivalent) — an AC a QA reviewer could disagree with about what "done" means is not a real AC.
- Definition of Ready (what must be true before dev starts) and Definition of Done (what must be true before it's considered shipped) are distinct and both exist — a project that conflates them tends to start work on under-specified requirements and call it done based on "it compiles."
- Roadmap/backlog prioritization has a stated rationale tied to risk or business value (MoSCoW or equivalent) rather than reflecting whichever stakeholder asked most recently or loudest — check for *any* documented prioritization rationale; its total absence is itself a finding.

## 3. Check Clean Architecture layering

Verify the dependency direction actually holds, not just "looks right" from folder names:
- `MusicLounge.Domain` has no project reference to Application/Infrastructure/Api, and no `using` pointing outward — check the `.csproj` files, not just intuition.
- `MusicLounge.Api` controllers orchestrate (bind request → send command/query → shape response) and don't contain business logic — spot-check a sample of controllers for `if` statements that encode a business rule rather than pure request wiring.
- Infrastructure implements Application-defined interfaces; Application never references Infrastructure types directly.

## 4. Audit observability instrumentation

A sound architecture is only verifiable in production if it's observable — check for all three pillars, not just whichever is easiest to add:
- **Logs** — structured (not free-text string concatenation), with a correlation/trace ID that threads through a single request end to end, so a failure can be attributed to one request's actual path through the system.
- **Metrics** — request rate, error rate, and latency (the RED method) captured at minimum per critical endpoint (ticket hold, payment webhook, publish), not only as an undifferentiated whole-app number.
- **Traces** — distributed tracing across any request that crosses more than one hop (e.g., a request that triggers a VNPay call and a ledger write), so a slow request can be attributed to the actual slow component. OpenTelemetry is the current vendor-neutral instrumentation standard — verify current adoption guidance for .NET before recommending a specific package, since this shifts over time.
- Flag any of the three pillars that's entirely absent, not just "could be better" — a system with only logs and no metrics/traces is flying blind on performance and cross-service failures even if it "looks observable" from the log volume alone.

## 5. Audit resilience patterns on every external dependency

This system depends on VNPay, Mux, Cloudflare Stream, SMS gateway, SMTP, Firebase Cloud Messaging, and ML.NET. For each:
- Is there an explicit timeout, or does a hung external call block a request indefinitely?
- Is there retry-with-backoff, and only on the calls that are actually idempotent (a retried non-idempotent call is its own bug)?
- Is there a defined degraded-mode behavior when the dependency is down (e.g., what happens to show creation if Mux is unreachable — fail closed with a clear error, per the 503 behavior already observed in this system, or silently proceed)?
- Flag any dependency with no documented failure mode at all.

## 6. Check that architecture decisions are recorded

Look for any form of Architecture Decision Record (a doc, a detailed commit message, a memory entry) behind non-obvious structural choices already present in the code (e.g., why donations use a 4-tier quota check instead of one; why erasure anonymizes in place instead of hard-deleting). If a clearly deliberate structural choice has zero trace of *why*, flag it as a process gap — future changes risk silently undoing a decision nobody remembers making.

## 7. Report

For each finding: cite the exact file:line, state whether it's a BA-lens finding (business rule fidelity) or Architect-lens finding (structural), and state the concrete risk if left unaddressed. Don't guess at the "right" fix for a missing traceability source — that's a question for whoever owns the business decision, not something to invent.

## Quick reference

| Need | Go to |
|---|---|
| The system_config pattern and its 11 existing keys | `system_config` table via `ISystemConfigService` — grep `ConfigKeys` constants in `src/MusicLounge.Application/Common/Interfaces/ISystemConfigService.cs` |
| Full role definitions this skill implements | The MusicLounge SDLC role-charter artifact (roles 01, 03) |
| Deeper code-quality/architecture review beyond BA/Architect scope | `master-backend-techlead` skill, if present in this project |
