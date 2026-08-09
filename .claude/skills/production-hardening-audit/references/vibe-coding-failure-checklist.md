# Vibe-Coding Failure Pattern Checklist

Three families of patterns. Logic/state patterns are bugs that pass every test because only the happy path got tested. Security patterns are what AI-generated code statistically ships with because the prompt asked for a feature, not for safety. Process patterns are the workflow habits that let the other two reach production unnoticed. Walk each family deliberately for the area under review — don't rely on spotting these free-form.

## A. Logic / state patterns

1. **Missing state-machine branches.** The code was written imagining one flow through a process; a real state the domain actually has (cancelled-while-in-progress, already-processed, partially-refunded, retried-after-timeout) was never modeled. Concretely: read every status enum touching the area under review, and for each state ask "what handler runs when the entity is in THIS state and someone calls THAT action" — if the answer is "nothing checks for this," that's a finding.

2. **Duplicated logic that can drift.** The same calculation, validation, or business rule is implemented independently in two or more places instead of one being the source of truth the other calls. Grep for the distinctive constants/formula shape in a second location. Today's two copies agreeing is not evidence they'll agree after either is edited alone.

3. **Missing concurrency protection on shared mutable state.** Two concurrent callers (two users, two admins, a user vs. a background job) can both read a precondition as true before either commits its write, and both proceed — producing a duplicate row, a double-transition, or corrupted aggregate state. Look specifically at: check-then-insert without a unique constraint backing it, check-then-update on a status field without a lock, and any handler whose sibling handlers use a lock but it doesn't.

4. **Stale data read instead of a value snapshotted at commitment time.** A price, entitlement, quota, or rate is captured once "when it mattered" in one code path, but a related/downstream path re-reads the current live value instead of the captured one — so an edit made in between silently changes what the other party actually gets, after they already committed to the original value.

5. **Inconsistent application of a correct pattern.** One authorization check, locking convention, or validation rule exists and is used correctly in most places — but one or two call sites (often added later, or added by a different session/prompt) implement a looser version instead of reusing the shared one. Explicitly diff every call site that does "the same kind of check" against each other, not just against an abstract standard.

6. **Half-wired features that silently no-op.** An option, field, or code path exists and looks complete (accepted by validation, stored, shown in a DTO) but nothing downstream actually acts on it — so choosing it behaves identically to not choosing it, with no error to reveal the gap.

7. **Unauthorized dead code.** A handler, endpoint, or function is fully implemented — including a real security-sensitive data return — but isn't wired to any route or caller yet, and because it's "not live" it never got the authorization check its siblings have. It's not exploitable today, but it will be the moment someone wires it up without re-auditing it. Prefer fixing the check over deleting, unless you're certain the code has no intended future use.

8. **Idempotency inferred from the wrong signal.** "Has this already happened" is answered by checking a side effect's current state (e.g., "is the target already in the state this action would produce") instead of a marker on the action itself. This breaks the moment two different actions could legitimately produce the same side-effect state — the second action's own effects get silently skipped because the check can't tell them apart.

9. **Single-path reliance on an unreliable external signal.** A critical confirmation (payment succeeded, a webhook fired, a client acknowledged) is trusted from exactly one delivery path with no independent/redundant channel — and that one path has a plausible real-world failure mode (browser closed, client crashed, network dropped) that silently loses the signal forever instead of retrying or reconciling.

10. **No audit trail on consequential actions.** State transitions that matter for money, access, or a legal/compliance record (ban a user, view sensitive PII, issue a penalty, approve a payout) produce no structured log distinguishable from routine traffic — so a dispute or incident has no way to reconstruct who did what, when. Check this specifically wherever elevated-privilege actions exist; look for whether *sibling* handlers in the same domain already log and this one doesn't — inconsistency here is a strong signal it was simply missed, not a deliberate choice.

## B. Security patterns (what AI-generated code statistically ships with)

11. **Auth checked in the wrong layer.** A check exists in the UI/frontend but the corresponding API route has no server-side enforcement — trivially bypassed by calling the API directly.

12. **Secrets reachable from the client.** An API key, credential, or internal token is embedded in frontend code, a public config, or a response payload instead of staying server-side only.

13. **Auth silently weakened across iterative edits.** A later prompt/session, focused on a new feature, edits a shared auth/middleware path and narrows or removes a check that an earlier session had correctly added — because the reviewer's attention was on the new feature, not the file's full diff.

14. **Injection surfaces.** Anywhere user input reaches a query, shell command, HTML render, or file path without the standard safe-by-default mechanism for that sink (parameterized queries, output encoding, path canonicalization) — don't assume an ORM or templating engine's defaults are actually in effect; verify the specific call site uses them.

15. **Unvalidated dependency additions.** A new package was added to satisfy some requested functionality without any check on its maintenance status, known vulnerabilities, or whether its capabilities exceed what's actually needed.

16. **Missing resource/rate limits.** Any endpoint accepting a size, count, or repeat-call parameter with no upper bound — file uploads, pagination limits, search-result counts, retry/resend endpoints — that a single bad actor or bad client bug could use to exhaust memory, CPU, or a downstream quota.

17. **No guardrail on destructive automated actions.** A background job, migration, or agent-executed action can delete/overwrite data with no dry-run, confirmation, backup, or scope limit — relying entirely on the code being correct rather than on a safety net if it isn't.

## C. Process patterns (how the other two reach production)

18. **Skipped review of AI-authored diffs.** Generated code is accepted once it runs, without a deliberate second pass treating it as an untrusted first draft — this is what lets items A and B above survive.

19. **Prototype scope mistaken for production scope.** A build that proved an idea works gets shipped as-is to real users/data/traffic without the additional work "the idea works" never had to cover: concurrency, adversarial input, partial failure, scale.

20. **QA skipped or shallow.** Test coverage that only exercises a single successful call, never a second concurrent caller, a retried call, or a state the happy path doesn't reach — which is exactly the coverage gap that hides items A1, A3, and A8 above.
