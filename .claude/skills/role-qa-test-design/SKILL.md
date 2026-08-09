---
name: role-qa-test-design
description: Reviews test coverage using ISTQB-standard test design techniques (equivalence partitioning, boundary value analysis, decision tables) rather than ad hoc coverage, builds a traceability matrix from acceptance criteria to actual test cases, checks that concurrent/negative paths are tested — not just the happy path a suite tends to accumulate under time pressure — and requires the suite itself be run against real SQL Server before a major release, not only the SQLite-backed CI default. Covers role 07 (Functional QA Engineer) from the MusicLounge SDLC role charter. Use when asked to review test coverage, design test cases, audit whether the test suite is thorough enough before release, or explicitly invoke the QA role.
license: Internal project tooling — MusicLounge repository, no separate license.
---

# Functional QA — Test Design Review

Mandate: *"Xác nhận hệ thống làm đúng đặc tả và bắt được trường hợp biên trước khi tới tay người dùng thật — không chỉ chạy lại happy-path."*

Grounded in ISTQB Certified Tester Foundation Level Syllabus v4.0.1 (04/2023) — re-verify this is still current before citing a version number in any report, since syllabi are periodically revised.

Work in order: **(1) Inventory existing coverage → (2) Boundary-value audit → (3) Concurrency audit → (4) Negative-path audit → (5) Traceability → (6) Report.**

## 1. Inventory existing coverage using the project's own taxonomy

This repo already organizes integration tests by business flow, not by layer — `tests/MusicLounge.Tests.Integration/CF1` through `CF7`, plus `Compliance/`, `Security/`, `E2E/`, `Auth/`, `Uploads/`, `Users/`. Use this structure as the map: for the flow under review, read the existing test file(s) first so new findings are additive, not duplicate rediscovery.

## 2. Boundary-value audit

For every business rule with a numeric threshold — ticket quota (price/tier/zone/show, 4 nested levels), subscription `MaxTicketsPerEventSnapshot`, SLA hour windows, publish-lead-time (≥7 business days), donation hold days — confirm tests exist at the boundary itself and one unit on each side (boundary−1 passes, boundary passes/fails per the rule's actual `>` vs `>=`, boundary+1 fails). A rule tested only with a comfortably-inside value and a wildly-over value has not actually verified where the line is. Use **equivalence partitioning** to also confirm each valid/invalid input class has at least one representative, not just the boundaries.

## 3. Concurrency audit — this system's most fragile point

Ticketing systems fail at the boundary between "read available capacity" and "commit the reservation," not in sequential logic. For every operation that reserves a finite shared resource (ticket hold, zone-capacity check, `LoungeStaff` unique-active-assignment, `OwnerSubscription` unique-active-per-owner), confirm a test exists that fires genuinely concurrent/parallel requests against the *same* resource and asserts no overselling — not a test that calls the handler twice sequentially and calls that "concurrency." Sequential double-calls do not exercise the actual race window a `IShowBookingLock`/`IAsyncKeyedLock` or filtered unique index is defending against.

## 4. Negative-path audit

Sample handlers across at least 3 different modules. For each, confirm tests exist for: wrong role calling it, wrong state (e.g., action attempted on a show/donation/subscription in a status that shouldn't allow it), malformed/missing required input, and — where relevant — the exact HTTP status/error message contract, not just "it returns an error."

## 5. Build the traceability matrix

Cross-reference against the Acceptance Criteria produced by the BA/Architect review (`role-ba-architecture-review` skill, if it has run) or against whatever spec exists. Every AC should map to at least one test case; every test case should map back to a reason it exists. List any AC with zero covering test as a gap, and any test with no traceable purpose as a candidate for removal or documentation.

## 6. Regression discipline

Confirm CI actually runs the full suite on every change, not a subset — a partial suite that's green tells you less than it appears to. Beyond CI's default SQLite-backed run: before any major release, confirm the full suite (or at minimum every test touching schema, cascade behavior, or enum-typed columns) has been run at least once against a real SQL Server instance, not only the SQLite integration-test harness. This is distinct from `role-db-integrity-review`'s migration-apply check — that skill verifies the *schema* migrates cleanly; this step verifies the *test suite's assertions* still hold against real-engine behavior (query translation, constraint enforcement, and locking semantics all differ from SQLite in ways that have produced real, otherwise-invisible bugs in this project before).

## Report

For each gap: name the specific rule/flow, the missing test-design technique (boundary value / equivalence class / decision table / concurrency), and — where feasible — draft the missing test case rather than only describing it.

## Quick reference

| Need | Go to |
|---|---|
| Existing business-flow test taxonomy | `tests/MusicLounge.Tests.Integration/CF1..CF7`, `Compliance/`, `Security/`, `E2E/`, `Auth/`, `Users/` |
| Shared fixtures (careful: shared across the whole suite) | `tests/MusicLounge.Tests.Integration/Helpers/SeedHelper.cs` — destructive tests must use freshly-created accounts, not shared seed data |
| Real dynamic concurrency/boundary verification against SQL Server (this skill only reviews test *design*, not runtime behavior) | `role-enduser-uat-dynamic` and `role-performance-load-review` skills |
