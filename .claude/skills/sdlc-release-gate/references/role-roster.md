# 17-role SDLC charter → skill coverage map

This repo is **`MusicLounge-Backend-ForFE`** — backend only. The frontend is a separate,
consuming repository not present here. That single fact determines which of the charter's
17 roles this skill set can actually execute against this codebase.

| # | Role | Skill that covers it | Coverage |
|---|---|---|---|
| 01 | Business Analyst / Product Owner | `role-ba-architecture-review` | Full — requirement traceability is a code+repo concern |
| 02 | UX/UI Designer | *(none)* | **Out of scope here** — no frontend/design assets in this repo. Run from the frontend repo. |
| 03 | Solution Architect | `role-ba-architecture-review` | Full |
| 04 | Backend Engineer | `master-backend-techlead` (existing skill) | Already covered — general code/architecture review methodology |
| 05 | Frontend Engineer | *(none)* | **Out of scope here** — no frontend code in this repo. |
| 06 | Database Engineer / DBA | `role-db-integrity-review` | Full |
| 07 | Functional QA | `role-qa-test-design` | Full |
| 08 | Security Tester | `role-security-asvs-audit` | Full |
| 09 | Performance Engineer | `role-performance-load-review` | Full |
| 10 | Accessibility & Inclusive-Design Auditor | *(none directly)* | **Partial at best** — real accessibility auditing (contrast, screen-reader DOM, keyboard nav) needs rendered UI, which lives in the frontend repo. What *is* checkable from here: whether API error messages/data contracts give a frontend enough to build an accessible experience (plain-language errors, locale-correct formatting fields) — fold this spot-check into `role-technical-writer-audit` or `role-ba-architecture-review` rather than treating it as fully covered. |
| 11 | Financial / Payment Correctness Auditor | `role-financial-ledger-audit` | Full |
| 12 | Legal & Data Privacy Officer | `role-legal-compliance-vn` | Full |
| 13 | DevOps / SRE / Release Engineer | `role-devops-release-readiness` | Full |
| 14 | Technical Writer / Documentation | `role-technical-writer-audit` | Full |
| 15 | Customer Support Readiness | *(none directly)* | **Partial** — the checkable half (error codes being lookup-able, complaint-channel SLA) is covered incidentally by `role-technical-writer-audit` and `role-legal-compliance-vn`; the FAQ/support-content half needs a human or a support-specific skill not yet built. |
| 16 | End-User UAT | `role-enduser-uat-dynamic` | Full for account-based personas (Audience/Owner/Staff/Admin/Performer) and the anonymous persona's *API-level* experience. Does **not** cover visual/UX quality of the anonymous experience — that's rendered by the frontend repo. |
| 17 | Release / Product Manager (Go/No-Go) | `sdlc-release-gate` (this skill) | Full — this IS that role |

## What "full coverage" means in practice

Even where marked "Full," these skills check what's *verifiable from this repo* against
each role's mandate — they don't replace a human's business judgment on roles that are
inherently about human trade-off decisions (BA prioritization calls, Release Manager's
final risk-acceptance sign-off). They produce the evidence a human decision-maker needs;
they don't remove the human from roles where removing them would be the wrong call.

## Existing skills already in this repo, reused rather than duplicated

- `master-backend-techlead` — general four-lens code/architecture review (functional,
  business-logic, tech/infra, operational/security), mentoring delivery style. Satisfies
  role 04 and contributes to role 03.
- `production-hardening-audit` — the "vibe coding" failure-pattern hunt (stale snapshots,
  missing locks, inconsistent authorization, half-wired features) plus the fix-and-verify
  workflow. Complements `role-security-asvs-audit` and `role-db-integrity-review`.
