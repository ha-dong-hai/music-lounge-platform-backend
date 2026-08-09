---
name: role-security-asvs-audit
description: Runs a systematic security audit using OWASP ASVS 5.0.0 (~350 requirements across 17 categories) and OWASP Top 10:2025 as the checklist, going beyond static code reading into dynamic boundary testing — actually attempting cross-role and cross-venue access against a real running instance with real accounts — since authorization bugs are invisible to static review alone and this is the single highest-value category for a multi-tenant venue platform. Covers role 08 (Security Tester) from the MusicLounge SDLC role charter. Use when asked for a security audit, penetration test, OWASP-based review, or to explicitly invoke the Security Tester role.
license: Internal project tooling — MusicLounge repository, no separate license.
---

# Security Tester — OWASP ASVS 5.0 Audit

Mandate: *"Tìm lỗ hổng trước kẻ tấn công. Kiểm tra có hệ thống theo checklist đo lường được — không kiểm tra tuỳ hứng theo trực giác."*

Work in order: **(1) Re-verify standard versions → (2) Static pass by ASVS category → (3) Dynamic boundary testing → (4) Report with severity → (5) Clean up.**

## 1. Re-verify current standard versions before citing

Standards move. Before writing OWASP Top 10:2025 or ASVS 5.0.0 into a report, search to confirm these are still current (not superseded) — don't cite from memory or from this file's own text without a fresh check. If a newer edition exists, use it and note the update.

## 2. Static pass — walk `references/asvs-category-map.md` category by category

Don't review "for security bugs" free-form — that reliably misses categories the reviewer isn't already primed to think about. Walk all 17 ASVS categories in order using the reference map, which is pre-mapped to where each category actually lives in this codebase. Give **V8 (Authorization)** the most weight: this is a multi-tenant platform (many venues, each with its own Owner/Staff), and resource-level authorization bugs here mean one venue's data leaking to another — the highest-cost failure mode this system has.

Also apply OWASP Top 10:2025 as a cross-check, with special attention to:
- **A02 Security Misconfiguration** (risen from #5 to #2 in 2025) — review environment/config handling explicitly: default credentials, verbose stack traces in production, permissive CORS, secrets in version control.
- **A10 Mishandling of Exceptional Conditions** (new in 2025) — check for errors caught and silently swallowed, code that fails open instead of closed when a check errors, and logical errors mistaken for success (e.g., a payment treated as confirmed because no exception was thrown, without checking the actual response status).

## 3. Dynamic boundary testing — the part static review cannot substitute for

Authorization bugs are only provably absent by attempting the forbidden action, not by reading the code and reasoning it should fail. Stand up the real app against a real database and:
- Register one real account per role (Audience, Owner ×2 for different venues, Staff, Admin) and extract real JWTs.
- Attempt every cross-boundary access a real attacker would try: Staff of venue A reading venue B's draft show / livestream credentials, Owner A reading Owner B's analytics or bank account, a revoked Staff member's still-unexpired JWT continuing to work, a non-Admin hitting Admin-only endpoints.
- Attempt rate-limit and brute-force lockout in practice (not just reading `AuthAttemptTracker`'s constants) — fire the actual sequence of failed logins and confirm the lockout triggers and the timing.
- Test JWT tampering resistance and `sec_stamp` revocation timing (does a password change actually invalidate an already-issued token on the very next request, not just after expiry).

See `role-enduser-uat-dynamic`'s reference playbook for the exact account-setup and JWT-extraction mechanics — reuse it rather than reinventing the setup.

## 4. Report with severity

Use a CVSS-like severity tier for every finding (critical/high/medium/low), map each finding explicitly to its ASVS category and/or OWASP Top 10 category, and give the concrete exploit path in plain terms ("Staff JWT for venue A, called against `/livestreams/{id}/credentials` where `{id}` belongs to venue B, returns 200 with real RTMP credentials") — not just a category label.

## 5. Clean up

Any accounts, venues, or test data created for dynamic testing should be identified as such in the report. Stop any process started for testing (note the PID/port). Don't leave test artifacts indistinguishable from real production data without flagging them.

## Quick reference

| Need | Go to |
|---|---|
| ASVS 17-category map to this codebase | `references/asvs-category-map.md` |
| Account setup / JWT extraction mechanics for dynamic testing | `role-enduser-uat-dynamic` skill's `references/dynamic-testing-playbook.md` |
| General OWASP Top 10 grounding already in this project | `master-backend-techlead` skill's `references/security-standards.md`, if present |
