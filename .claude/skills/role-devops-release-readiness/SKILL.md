---
name: role-devops-release-readiness
description: Reviews CI/CD pipeline integrity, migration rollout safety, background-job observability across all 18 Hangfire jobs, and incident runbooks against DORA's five software-delivery-performance metrics and supply-chain/config-security guidance. Covers role 13 (DevOps / SRE / Release Engineer) from the MusicLounge SDLC role charter. Use when asked to review deployment readiness, CI/CD, monitoring, rollback plans, or explicitly invoke the DevOps/SRE role.
license: Internal project tooling — MusicLounge repository, no separate license.
---

# DevOps / SRE / Release Engineer — Operational Readiness Review

Mandate: *"Triển khai an toàn, quan sát được, phục hồi nhanh khi sự cố thật xảy ra — không phải khi mọi thứ suôn sẻ."*

Work in order: **(1) Measure DORA metrics → (2) Check deploy safety → (3) Audit background-job observability → (4) Check runbooks → (5) Check supply chain → (6) Report.**

## 1. Measure or estimate the DORA metrics

Google's DORA research tracks five metrics as of 2024–2025 (re-verify this hasn't shifted before citing a specific number): deployment frequency, lead time for changes, change failure rate, mean time to recovery (MTTR), and rework rate. Estimate what's derivable from git history and CI config (commit-to-deploy time, how often a deploy is followed by a revert/hotfix within a short window); state plainly what can't be derived from the repo alone (true production MTTR needs incident data this repo doesn't contain) rather than fabricating a number.

## 2. Check deployment safety

- CI/CD verifies build/artifact integrity before deploy — no unverified step in the path from commit to running service.
- Migrations follow the additive-first pattern for rolling deploys: new columns/tables added in one release, cleanup of anything no-longer-needed happens in a *later* release, never simultaneously with the code that stops using it — otherwise a mid-rollout mix of old and new code instances breaks against the same schema.
- A rollback path exists and has genuinely been exercised at least once (ask directly if this can't be confirmed from the repo; a rollback plan that's never been tried is unverified, not safe).

## 3. Audit background-job observability — all 18 jobs

This system runs 18 Hangfire background jobs (email/OTP delivery, ticket-hold/payment expiry, donation lifecycle, subscription expiry, settlement release, penalty application, moderation appeal auto-approval, event reminders, recommendation refresh, behaviour logging). For each: confirm there is a way to notice if it stops running, runs late, or throws — not just that it's scheduled and presumed to work. A job that silently stops (e.g., `SettlementReleaseJob` failing silently would mean Owners stop getting paid with nobody aware) is a severe operational blind spot specific to this domain — flag any job without failure/delay alerting explicitly, and prioritize the money-moving and legally-time-bound ones (settlement, moderation SLA, subscription expiry) above the rest.

## 4. Check incident runbooks

Confirm runbooks exist (or draft skeletons if missing) for at minimum: database unreachable, VNPay unreachable, Mux/Cloudflare Stream unreachable, SMS/email gateway unreachable. Each runbook should state: how an operator would notice, what the user-visible symptom is, and the immediate mitigation (not just "escalate").

## 5. Check supply chain basics

Lockfiles committed, dependency versions pinned rather than floating, and some process (even manual) for noticing known-vulnerable dependencies — not just "an audit tool ran once, a long time ago."

## 6. Report

State DORA metrics with explicit confidence/derivation method, list every job without alerting by name, and attach draft runbooks for any missing ones rather than only flagging the gap.

## Quick reference

| Need | Go to |
|---|---|
| Full list of background jobs | `src/MusicLounge.Infrastructure/Jobs/*.cs` + `src/MusicLounge.Application/*/Jobs/*.cs` |
| system_config keys that gate job behavior (SLA hours, hold minutes, reminder hours) | `ConfigKeys` in `ISystemConfigService.cs` |
| Migration safety specifics (real precedent for a rollout that broke on the real engine) | `role-db-integrity-review` skill |
