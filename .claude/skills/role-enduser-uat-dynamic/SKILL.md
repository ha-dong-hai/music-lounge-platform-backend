---
name: role-enduser-uat-dynamic
description: Runs true dynamic user-acceptance testing against a real running instance and a real SQL Server database — registering genuine accounts per persona (Audience, Owner, Staff, Admin, and an anonymous/no-account "passerby" persona) and walking each one's golden path end to end — rather than reasoning about behavior from reading code, which is exactly the blind spot that previously let a real SQL-Server-only bug through 287 passing SQLite-based tests. Covers role 16 (End-User UAT) from the MusicLounge SDLC role charter. Use when asked to test the system as a real user, run UAT, verify role boundaries dynamically, or explicitly invoke the End-User UAT role.
license: Internal project tooling — MusicLounge repository, no separate license.
---

# End-User UAT — Dynamic Per-Persona Testing

Mandate: *"Xác nhận trải nghiệm THẬT từ góc nhìn từng loại người cụ thể, kể cả người chưa từng có tài khoản. Không dùng chung 1 kịch bản test cho mọi persona."*

This is the one role in the charter that categorically cannot be satisfied by reading code carefully — it's defined by *doing the thing a real user would do*, against the real stack. Follow `references/dynamic-testing-playbook.md` for the exact mechanics (proven working commands, not theoretical steps); this file covers the per-persona scope and reporting shape.

Work in order: **(1) Set up environment and accounts → (2) Walk each persona's golden path → (3) Explicitly attempt cross-boundary access → (4) Clean up → (5) Report per persona.**

## 1. Environment and accounts

Start the real app against a real SQL Server database — not SQLite, not an in-memory fake. Register one genuine account per persona this system defines: Audience, Owner (at least two, at different venues — several checks only mean something with two distinct venues to test cross-venue isolation against), Staff, Admin, and Performer if that flow exists. See the playbook for the exact registration/JWT-extraction/Admin-promotion commands.

## 2. Walk each persona's golden path

Each persona gets its **own** end-to-end scenario, not a shared generic smoke test:
- **Audience**: find a show → hold/buy a ticket → receive it → check in → rate it — completable without needing developer help at any step.
- **Owner**: create a venue → activate a subscription → create a show → submit for moderation → receive payment — confirm the lifecycle matches what `README-SETUP.md`'s "Vòng đời show" section documents (Draft → Pending → Published, not visible publicly until Admin-approved).
- **Staff**: sell a walk-in ticket, check in a ticket — confirm scoped strictly to the one venue they're assigned to.
- **Admin**: approve/reject a show, resolve a complaint, adjust a system_config value.
- **Performer** (if applicable): view schedule, receive a donation.
- **Anonymous / khách vãng lai (no account at all)**: view the public show list and show detail, follow a shared link or simulated QR-code URL — confirm public data is reachable without being forced through a login wall, and confirm nothing meant to be private leaks through an unauthenticated request.

## 3. Explicitly attempt cross-boundary access — this is the highest-value half of the exercise

For every persona, don't stop at confirming what they *can* do — confirm what they're *correctly blocked from*: another venue's Staff/Owner resources, another Owner's analytics or bank account, an Admin-only endpoint from a non-Admin role, a revoked Staff member's still-unexpired session. See the playbook for the exact expected status codes (404 vs. 403 vs. 401) and why the distinction matters (hiding a resource's existence vs. confirming-but-forbidding it are different security postures).

## 4. Clean up

Stop any process started for this run, and report explicitly what test data was created and whether it was removed or left in place — see the playbook's cleanup section for the exact commands to identify the right process before killing anything (never kill a process on a shared port without confirming it's the one you started).

## 5. Report per persona

One section per persona, not one combined pass/fail. For each: what golden-path steps succeeded, what cross-boundary attempts were correctly blocked (with the exact status code observed), and anything that behaved differently than expected — cite the literal request/response, not a paraphrase.

## Quick reference

| Need | Go to |
|---|---|
| Exact proven commands: start app, register, extract JWT, seed subscription, clean up | `references/dynamic-testing-playbook.md` |
| Documented show-lifecycle state machine to test the Owner persona against | `README-SETUP.md`, section "Vòng đời show" |
| Adversarial cross-boundary testing with a security lens (this skill's focus is UX/correctness, that skill's is exploitability) | `role-security-asvs-audit` skill |
