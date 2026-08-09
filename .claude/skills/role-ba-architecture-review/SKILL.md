---
name: role-ba-architecture-review
description: Audits whether implemented business rules and system architecture actually match documented/intended requirements — traces every business-relevant number in the code back to a real decision source (not a guess), checks Clean Architecture layering isn't violated, verifies resilience patterns (timeout/retry/circuit-breaker) exist for external dependencies, and confirms architecture decisions are recorded rather than living in one person's head. Covers role 01 (Business Analyst / Product Owner) and role 03 (Solution Architect) from the MusicLounge SDLC role charter. Use when asked to check requirement traceability, audit whether code matches business intent, review architecture conformance, or explicitly invoke the BA or Architect role.
license: Internal project tooling — MusicLounge repository, no separate license.
---

# Business Analyst & Solution Architect Review

Two roles bundled into one skill because they ask the same underlying question from two angles: **does the system actually encode what was decided, and is that decision traceable?** BA checks whether business rules are real decisions, not guesses. Architect checks whether the structure holding those rules is sound. Run both in one pass — they read the same code.

Mandate: *"Đảm bảo mọi tính năng phản ánh đúng cách một phòng trà ca nhạc thật vận hành tại Việt Nam — không dịch sai nghiệp vụ thành logic kỹ thuật, không tự suy đoán số liệu; kiến trúc đúng quy mô thật, không quá tay cũng không thiếu."*

Work in order: **(1) Trace business numbers → (2) Check layering → (3) Audit resilience patterns → (4) Check decision records → (5) Report.**

## 1. Trace every business-relevant number to a real source

Grep the Application layer for numeric literals that look like business decisions — percentages, day counts, hour counts, currency amounts, quotas — appearing directly in a handler rather than read from `ISystemConfigService`. For each one found:
- Is it a genuinely fixed technical constant (retry count, buffer size) — fine as-is.
- Is it a business-tunable number hardcoded where `system_config` was the established pattern for exactly this kind of value? Flag it — this repo's own precedent (`donation_performer_share_rate`, `platform_commission_rate`, etc., 11 keys total) means a new hardcoded business number is a regression, not a stylistic choice.
- For every business rule enforced in code (subscription caps, SLA windows, publish-lead-time requirements), confirm there's a traceable source: a code comment citing a law/decree, a commit message citing a benchmark, or a memory/doc citing a stakeholder decision. A rule with no traceable source is a finding — not because it's necessarily wrong, but because nobody can defend it under audit.

## 2. Check Clean Architecture layering

Verify the dependency direction actually holds, not just "looks right" from folder names:
- `MusicLounge.Domain` has no project reference to Application/Infrastructure/Api, and no `using` pointing outward — check the `.csproj` files, not just intuition.
- `MusicLounge.Api` controllers orchestrate (bind request → send command/query → shape response) and don't contain business logic — spot-check a sample of controllers for `if` statements that encode a business rule rather than pure request wiring.
- Infrastructure implements Application-defined interfaces; Application never references Infrastructure types directly.

## 3. Audit resilience patterns on every external dependency

This system depends on VNPay, Mux, Cloudflare Stream, SMS gateway, SMTP, Firebase Cloud Messaging, and ML.NET. For each:
- Is there an explicit timeout, or does a hung external call block a request indefinitely?
- Is there retry-with-backoff, and only on the calls that are actually idempotent (a retried non-idempotent call is its own bug)?
- Is there a defined degraded-mode behavior when the dependency is down (e.g., what happens to show creation if Mux is unreachable — fail closed with a clear error, per the 503 behavior already observed in this system, or silently proceed)?
- Flag any dependency with no documented failure mode at all.

## 4. Check that architecture decisions are recorded

Look for any form of Architecture Decision Record (a doc, a detailed commit message, a memory entry) behind non-obvious structural choices already present in the code (e.g., why donations use a 4-tier quota check instead of one; why erasure anonymizes in place instead of hard-deleting). If a clearly deliberate structural choice has zero trace of *why*, flag it as a process gap — future changes risk silently undoing a decision nobody remembers making.

## 5. Report

For each finding: cite the exact file:line, state whether it's a BA-lens finding (business rule fidelity) or Architect-lens finding (structural), and state the concrete risk if left unaddressed. Don't guess at the "right" fix for a missing traceability source — that's a question for whoever owns the business decision, not something to invent.

## Quick reference

| Need | Go to |
|---|---|
| The system_config pattern and its 11 existing keys | `system_config` table via `ISystemConfigService` — grep `ConfigKeys` constants in `src/MusicLounge.Application/Common/Interfaces/ISystemConfigService.cs` |
| Full role definitions this skill implements | The MusicLounge SDLC role-charter artifact (roles 01, 03) |
| Deeper code-quality/architecture review beyond BA/Architect scope | `master-backend-techlead` skill, if present in this project |
