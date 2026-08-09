---
name: sdlc-release-gate
description: Runs the full MusicLounge SDLC role-charter audit as a coordinated multi-agent sweep — dispatching the specialized role skills (DB integrity, QA test design, security ASVS, performance, financial ledger, legal compliance, DevOps readiness, technical writer, dynamic UAT, plus the existing backend/hardening skills) as parallel background agents, verifying their findings before trusting them, and producing one Go/No-Go report with blocker / should-fix / accepted-risk classification tied back to the role that owns each item. Covers role 17 (Release / Product Manager) from the MusicLounge SDLC role charter — the aggregation gate, not a replacement for any individual role. Use when asked for a full production-readiness review, a go/no-go assessment, or to run "the role charter" / "toàn bộ vai trò" / "tất cả các vai trò" audit.
license: Internal project tooling — MusicLounge repository, no separate license.
---

# Release Manager — Go/No-Go Aggregation Gate

Mandate: *"Tổng hợp kết quả của cả 16 vai trò khác thành 1 quyết định go-live có căn cứ — không phải cảm tính 'chắc ổn rồi'."*

This skill orchestrates; it doesn't re-derive each role's expertise itself. Its job is scoping, dispatch, verification discipline, and honest aggregation — not doing 9 skills' worth of specialist work inline.

Work in order: **(1) State scope → (2) Dispatch role skills in parallel → (3) Verify before trusting → (4) Aggregate with a tier per finding → (5) Report and stop.**

## 1. State scope before running anything

This repository is backend-only. Read `references/role-roster.md` first and say plainly, up front, which of the 17 charter roles this pass can and cannot cover from this repo alone (roles 02 UX/UI and 05 Frontend Engineer are structurally out of scope here; roles 10 and 15 are partially coverable). Silently skipping an out-of-scope role produces a report that looks complete but isn't — always name what wasn't checked and why, never just omit it.

## 2. Dispatch the applicable role skills in parallel

The role checks are independent of each other — they read different parts of the system and don't depend on each other's output. Launch them as parallel background Agent calls in a single message (not sequential one-at-a-time calls), each briefed with: which role skill to follow, the specific commit/scope to audit, and instruction to produce findings in the shared report shape from `references/report-template.md`. In scope for this repo:
`role-ba-architecture-review`, `role-db-integrity-review`, `role-qa-test-design`,
`role-security-asvs-audit`, `role-performance-load-review`, `role-financial-ledger-audit`,
`role-legal-compliance-vn`, `role-devops-release-readiness`, `role-technical-writer-audit`,
`role-enduser-uat-dynamic`, plus the existing `master-backend-techlead` and
`production-hardening-audit` skills for role 04's ground.

Don't fabricate or predict any agent's findings while they're running — the results arrive as completions, not as something to guess at in the meantime.

## 3. Verify before trusting — every finding, from every agent

Agent-reported findings, including this skill's own subagents' output, are hypotheses until checked — not facts to relay verbatim. For each finding that would land as a BLOCKER: read the actual file:line cited, and where the finding claims a runtime behavior (an endpoint returns X, a query does Y), spot-check by actually running it rather than trusting the agent's prose description of what it ran. A report padded with unverified or false-positive findings trains whoever reads it to stop trusting it — that failure mode is worse than a shorter, fully-verified report.

## 4. Aggregate into one report with an explicit tier per finding

Use `references/report-template.md`'s shape exactly: BLOCKER / SHOULD-FIX-SOON / ACCEPTED-RISK, each traced to the role and skill that produced it. Carry forward previously-known gaps (check `role-legal-compliance-vn`'s output and prior audit memory) as *named, re-confirmed* items, not as if newly discovered. Don't flatten severity — a report where 40 findings all read as equally urgent teaches nobody and gets ignored; rank by real production impact (money, data loss, security exposure, silent wrong answers) the way a Tech Lead would, not by how many were found in each category.

## 5. Report and stop — never act on the conclusion autonomously

Deliver the Go/No-Go recommendation and stop there. This skill never commits, deploys, force-pushes, or takes any other hard-to-reverse action on the strength of its own conclusion — that decision belongs to the human who asked for the audit, exactly as with every other irreversible action in this project's working agreement. If the recommendation is "Go with accepted risks," list them explicitly rather than letting them ride silently into production.

## Quick reference

| Need | Go to |
|---|---|
| Full 17-role charter and exactly which skill covers which role (and what's out of scope) | `references/role-roster.md` |
| Report structure every dispatched agent should write findings into | `references/report-template.md` |
| Any individual role's detailed methodology | That role's own `role-*` skill — this file intentionally doesn't duplicate their content |
