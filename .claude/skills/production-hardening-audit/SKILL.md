---
name: production-hardening-audit
description: Audits a codebase for the specific failure patterns that let AI-assisted ("vibe coded") software work in development but break in production — stale snapshots, missing concurrency locks, inconsistent authorization, half-wired features, dead code with no auth check, wrong idempotency signals, single-path external integrations, missing audit trails on sensitive actions, plus the broader OWASP-class security gaps AI-generated code statistically ships with. Then fixes findings in severity order with continuous build/test verification before every commit. Use whenever the user asks to audit, harden, or review a codebase for production-readiness, references "vibe coding" risks, asks "còn lỗi gì không" after a build-fast phase, or wants a systematic pre-launch pass.
license: See LICENSE.txt in this package, if present; otherwise treat as internal project tooling.
---

# Production Hardening Audit

You are auditing software that was likely built fast and iteratively — correct on the happy path, because that's the path that got tested. The job is finding where it breaks under real concurrency, real data drift over time, real attackers, and real external services failing in ways the demo never exercised — then fixing it without breaking what already works.

Work in this order: **(1) Establish context and scope → (2) Hunt using the failure-pattern checklist → (3) Verify every finding before trusting it → (4) Fix in severity order with continuous build/test → (5) Flag what needs a human decision instead of guessing → (6) Ask before committing.**

## 1. Establish context and scope

Before hunting for anything, understand: what does this system do for its end user, what's already been audited (don't silently re-walk ground already covered — ask or check prior commit history/memory first), what's the risk tolerance of the area under review (a payments/PII path earns more scrutiny than an internal admin toggle), and how large a slice to take in one pass. A single pass over an entire large codebase produces shallow findings; splitting by domain (money flow, one bounded context, the API/auth layer, a specific data-sensitivity class) and going deep produces real ones. If the codebase is large, say so and propose a slice order rather than attempting everything at once.

## 2. Hunt using the failure-pattern checklist

Don't review "for bugs" generically — walk deliberately through `references/vibe-coding-failure-checklist.md`, which groups patterns into three families: **logic/state patterns** (the ones that survive because only the happy path got tested), **security patterns** (the ones AI-generated code statistically ships with because the prompt never asked for security), and **process patterns** (workflow habits that let the other two ship unnoticed). For each area under review, walk the checklist item by item rather than free-form — free-form review reliably misses categories the reviewer isn't already primed to think about.

If money, PII, or authorization is anywhere in the area under review, treat it as the default priority regardless of what else is present — these are the categories where a missed finding has the highest real-world cost, and where this checklist was originally built from live findings.

## 3. Verify every finding before trusting it

Findings from subagents, prior audit notes, or your own first pass are hypotheses, not facts. Before including a finding in a report or fixing it:
- Read the actual file:line cited. Confirm the code does what the finding claims.
- Trace the concrete failure scenario end to end (what specific interleaving, input, or actor produces what specific wrong outcome) — don't accept a pattern-name label ("race condition," "IDOR") without the trace.
- If a finding turns out to be wrong or already mitigated elsewhere (e.g., a downstream check already catches what looks like a missing upstream check), downgrade or drop it and say so — a report padded with false positives trains the reader to stop trusting it.
- If you can empirically test a claim (run the code, run the test suite, start the app) rather than reason about it, do that instead of trusting static analysis alone.

## 4. Fix in severity order with continuous build/test

Group findings into severe → medium → light (or your project's equivalent tiers) and fix in that order — don't let a large finding count turn into 40 equally-weighted diffs nobody can review. See `references/fix-and-ship-workflow.md` for the concrete mechanics: the snapshot-at-commitment-point pattern (capture a value once, at the moment it's agreed/paid/decided, instead of re-reading "current" state later), the per-resource lock-key convention for concurrent-actor races, safe migration practices, and why build+test should run after every logical batch of related fixes rather than after every single file edit or only at the very end.

Never fix a finding you're not sure is real — re-verify (step 3) before spending an edit on it.

## 5. Flag what needs a human decision instead of guessing

Some findings aren't code bugs at all — they're product, business, or operational decisions wearing a bug's clothes: a fee model, which of two documented behaviors is the intended one, whether to register a webhook URL with a third party, whether historical data needs a backfill, who owns a key-backup procedure. Do not silently pick an answer and implement it. State the finding, the options, and their tradeoffs, and ask — the same way you'd ask before an irreversible action. Implement the answer once given; don't implement your guess "for now" and mention the open question in a comment.

## 6. Ask before committing

Once a batch of fixes is verified (build clean, tests passing, and where relevant, the app boots), summarize what changed and why, then ask whether to commit — don't commit unrequested. This mirrors the general rule that committing is a visible, hard-to-fully-reverse action, not a step to take on autopilot just because the code is ready.

## Quick reference

| Need | Go to |
|---|---|
| The full pattern checklist to hunt with (logic, security, process) | `references/vibe-coding-failure-checklist.md` |
| The concrete fix mechanics: snapshotting, locking, safe migrations, batch verification | `references/fix-and-ship-workflow.md` |
| General four-lens code/architecture review methodology (functional, business-logic, tech/infra, operational/security) and mentoring delivery style | Use alongside `master-backend-techlead`, if present in this project — this skill is the specialized hunting checklist and fix discipline; that one is the general review posture and how to explain findings to a less senior reader. |
