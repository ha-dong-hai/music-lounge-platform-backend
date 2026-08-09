# Go/No-Go report template

## 1. Scope statement (always first, always explicit)

State which roles from the 17-role charter ran in this pass, which were skipped and why
(see `role-roster.md` — some are structurally out of scope for a backend-only repo), and
the exact commit/branch audited.

## 2. Findings, grouped by role, each tagged with a tier

- **BLOCKER** — must be fixed before this release ships. Reserve for: data loss, money
  miscalculated, a security exposure with a real exploit path, or silent wrong answers
  reaching real users.
- **SHOULD-FIX-SOON** — real defect, doesn't block this release, must be tracked (not
  forgotten in a chat transcript).
- **ACCEPTED-RISK** — a known, named gap the team is choosing to ship with. Requires an
  explicit owner and a one-line reason it's acceptable *for this release* — "we didn't
  get to it" is not a reason, it's an omission wearing a decision's clothes.

Every finding traces to the role/skill that produced it and the concrete evidence
(file:line, or the literal request/response from a dynamic test) — no finding survives
into this report as an unsourced claim.

## 3. Known gaps carried forward (not new findings — surfaced for visibility)

Pull from `role-legal-compliance-vn` and prior audit memory: gaps that were already known
before this pass (e.g., NĐ 147/2024 reactive takedown, Performer CRUD) belong here, re-
confirmed as still open, not re-discovered as if new.

## 4. Go/No-Go recommendation

State it plainly: **Go**, **Go with accepted risks** (list them), or **No-Go** (list the
blockers). This is a recommendation for a human to ratify, not an autonomous decision —
this skill never commits, deploys, or otherwise acts on its own conclusion.

## 5. Post-release monitoring plan

What to watch in the first 24–48 hours (the highest-risk window), and confirmation the
rollback plan (from `role-devops-release-readiness`) is ready before go-time.
