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

Start the real app against a real SQL Server database — not SQLite, not an in-memory fake. Register one genuine account per persona this system defines: Audience, Owner (at least two, at different venues — several checks only mean something with two distinct venues to test cross-venue isolation against), Staff, and Admin. See the playbook for the exact registration/JWT-extraction/Admin-promotion commands.

**`Performer` is not an account-holding persona in this system** — verify this hasn't changed before assuming otherwise, but as of the last check the `Performer` entity has no `Email`/`PasswordHash`/`Account` link at all, only a `CreatedByUserId` recording which Owner/Staff created the profile. There is no login for a performer to "view their own schedule" — that experience, if it's meant to exist, is entirely unbuilt (consistent with the known Performer-CRUD gap tracked elsewhere). Don't fabricate a Performer login test; instead fold Performer-related verification into the **Owner** persona's golden path (an Owner manages Performer profiles and assigns them to a `Performance`) and flag the absent self-service experience as a product gap for the BA/Architect review to weigh, not a bug in this UAT pass.

## 2. Walk each persona's golden path

Each persona gets its **own** end-to-end scenario, not a shared generic smoke test:
- **Audience**: find a show → hold/buy a ticket → receive it → check in → rate it — completable without needing developer help at any step.
- **Owner**: create a venue → activate a subscription → create a show → submit for moderation → receive payment — confirm the lifecycle matches what `README-SETUP.md`'s "Vòng đời show" section documents (Draft → Pending → Published, not visible publicly until Admin-approved).
- **Staff**: sell a walk-in ticket, check in a ticket — confirm scoped strictly to the one venue they're assigned to.
- **Admin**: approve/reject a show, resolve a complaint, adjust a system_config value.
- **Performer (no login exists — see note in step 1)**: verify, via the Owner persona, that a Performer profile can be created/assigned to a show and that a donation credited to them flows correctly through `role-financial-ledger-audit`'s checks — this is Owner-mediated, not a Performer self-service scenario.
- **Anonymous / khách vãng lai (no account at all)**: view the public show list and show detail, follow a shared link or simulated QR-code URL — confirm public data is reachable without being forced through a login wall, and confirm nothing meant to be private leaks through an unauthenticated request.

Weave one network-constraint pass through at least the Audience and Anonymous golden paths rather than treating it as a separate persona: replay the same requests with an artificially slow/high-latency connection (`curl --limit-rate` or a proxy tool) and confirm the API still behaves correctly under a request that takes several seconds to arrive — no premature timeout that a fast connection wouldn't trigger, no partial-write left behind if a slow client disconnects mid-request. This is the backend-checkable half of "works for someone on a weak connection"; the rendering/UX half of that concern belongs to the frontend repo and is out of scope here.

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
