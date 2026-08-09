---
name: role-technical-writer-audit
description: Diffs published documentation (Swagger/OpenAPI, docs/API-STANDARDS.md, README-SETUP.md) against the real running behavior of the API by actually calling it — not by re-reading the same source annotations the docs were generated from, which would just confirm the docs agree with themselves. Covers role 14 (Technical Writer / Documentation) from the MusicLounge SDLC role charter. Use when asked to review documentation accuracy, API doc completeness, or explicitly invoke the Technical Writer role.
license: Internal project tooling — MusicLounge repository, no separate license.
---

# Technical Writer — Documentation Accuracy Audit

Mandate: *"Tài liệu phải đúng những gì hệ thống THẬT SỰ làm — lệch pha với code là tài liệu có hại hơn không có tài liệu."*

The core discipline here: documentation drift is invisible to a reviewer who only re-reads the code the docs were written from — that just checks the docs against their own source, not against reality. The only real check is calling the live system and comparing.

Work in order: **(1) Call real endpoints → (2) Walk the setup guide literally → (3) Audit changelog currency → (4) Report drift.**

## 1. Call real endpoints and compare against documented contract

Start the app against a real database. For a representative sample of endpoints across several modules (not just the ones easiest to test), call them for real and compare the actual response shape, status codes, and error format against what `docs/API-STANDARDS.md` and the Swagger spec claim. Specifically check:
- The error contract (`{success, message, errors}`) is actually what every sampled endpoint returns on failure, not just the ones documented as examples.
- Status codes match documented expectations (a documented 404 that's actually a 400, or vice versa, breaks client error-handling silently).
- Any endpoint whose documented behavior no longer matches reality — this is the highest-value finding this skill produces, since it means client integrators (including FE devs consuming this backend) are working from a stale contract.

## 2. Walk the setup guide literally, start to finish

`README-SETUP.md` is this repo's strongest documentation asset — it's written for someone who knows nothing about the system. Its accuracy is worth protecting specifically: literally follow it step by step (or note precisely which step was last verified and when) rather than skimming it and assuming it still works. Any step that no longer matches current behavior (a changed endpoint path, a renamed field, a new required step not yet documented) should be corrected immediately — this document is often a new team member's or new AI agent's first real interaction with the system, and it silently teaches wrong mental models if stale.

## 3. Audit changelog / business-facing currency

For any recent change with business-visible impact (e.g., a commission or split ratio becoming config-driven instead of fixed, a new compliance-driven endpoint like DSAR erasure), confirm it's reflected somewhere a non-developer reader would find it — not only in a commit message or code comment that a business stakeholder would never read.

## 4. Report drift

For each documented claim found to be wrong: state the doc location, the actual observed behavior (with the real request/response used to verify it), and whether the fix is to correct the doc or to treat the drift itself as a product bug (i.e., the code changed unintentionally and the *old* documented behavior was the intended one — don't assume the code is always right just because it's newer).

## Quick reference

| Need | Go to |
|---|---|
| The documented API/response contract | `docs/API-STANDARDS.md` |
| The most FE-facing onboarding doc, treat as high-value to keep accurate | `README-SETUP.md` |
| Live Swagger UI for the running instance | `http://localhost:5289/swagger` once the app is running |
