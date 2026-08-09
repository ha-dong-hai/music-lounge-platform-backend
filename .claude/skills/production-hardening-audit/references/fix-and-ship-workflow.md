# Fix-and-Ship Workflow

The mechanics for turning a verified finding into a safe, shipped fix. These are patterns, not rules to apply blindly — use judgment about which apply to a given finding.

## Order fixes by severity, not by discovery order

Group findings into severe / medium / light before touching any code. Fix severe first, medium second, light last, and treat each tier as a natural checkpoint to report progress and re-verify (build + test) before continuing — don't let a long finding list collapse into one giant undifferentiated diff. If the user asks to "fix everything," this ordering is still how you sequence the work, not a reason to skip it.

## The snapshot-at-commitment-point pattern

When two parties agree to a value (a price, an entitlement, a rate) at one point in time, but a *later* step in the same process needs that value again, capture it once at the moment of agreement and carry the captured value forward — never re-derive it from "the current record" at the later step. A record that's still mutable between the two steps (an admin can edit it, a config can change) is exactly the gap that lets the two steps silently disagree. Concretely: add the captured value as a field on the record created at commitment time (an order, a payment, a request), and change every downstream reader to consume that field instead of re-querying the source entity. If the downstream code already reads *one* related field this way (e.g., a total amount) but not others from the same source (e.g., entitlements bundled with that purchase), that inconsistency is itself a finding — fix all of them the same way, not just the one already caught.

## Concurrency locks: per-resource keys, verified against every sibling

When adding a lock to close a race:
- Key it to the specific resource instance the race is over (`"{action}:{resourceId}"`), not globally.
- Check whether a *different* handler that can race against this one (a background job doing the same transition, a related command touching the same resource) needs the *same* key — a lock only one side acquires doesn't prevent the race.
- If the racing handler processes a batch (a loop over many resources) with a single save at the end of the loop, holding a per-item lock across the whole loop doesn't actually protect any single item's commit — move the lock acquisition and the save to be per-item, so the lock's scope actually covers the write it's protecting.
- After acquiring a lock and re-reading state, re-check the precondition under the lock (state may have changed between an earlier unlocked read and lock acquisition) rather than trusting a pre-lock read.

## Idempotency markers: mark the action, not the outcome

When fixing pattern A8 (idempotency inferred from the wrong signal), add an explicit marker (a timestamp or flag) set only when *this specific action* has run, and check that marker — not a proxy signal that something *matching* this action's outcome happened. This usually means a small schema addition; see migration safety below.

## Migration safety checklist

- Prefer additive, nullable columns over anything that could destroy existing data.
- Read the generated migration's Up *and* Down before trusting it — confirm it does only what you intended, nothing implicit.
- After generating, rebuild and run the full test suite — a broken model/migration mismatch surfaces immediately as a test-infrastructure failure, not a subtle bug.
- If a test starts failing after a migration + logic change, check first whether the test encoded the *old* (now-intentionally-changed) behavior before assuming your fix is wrong — update the test's expectation if so, don't work around the test.
- Watch for a database-specific translation gap between the test database and the production database engine (e.g., an ORDER BY or combined-predicate query that one engine translates and another doesn't) — if a query pattern already has a documented workaround elsewhere in the codebase for this reason, apply the same workaround rather than reintroducing the untranslatable form.

## Build and test after every logical batch

Run a full build and the full test suite after each related group of fixes — not after every single file edit (too slow to be practical) and not only once at the very end (a failure becomes hard to attribute to a specific change). "Logical batch" usually means: everything needed for one finding, or a small set of tightly related findings fixed together. A green build+test run is what earns the right to move to the next tier of severity or to report progress.

## Flag-don't-guess triggers

Stop and ask instead of implementing a fix when:
- The correct behavior depends on a business/product decision not stated anywhere in the code or prior conversation (a rate, a split, a policy).
- The fix requires an action outside the codebase (registering a URL with a third party, a production data backfill, a key-backup procedure) — implement the code-side change if there is one, but say plainly that it has no effect until the outside action happens.
- Two plausible fixes exist with different tradeoffs and no clear precedent in the codebase for which one this project prefers.

## Ask before committing

Summarize what changed, the verification performed (build/test result, and live-boot verification if a migration or startup-path change was involved), and ask whether to commit — every batch, not just the first one. Commit messages should state the concrete failure scenario each fix closes (what input/interleaving caused what wrong outcome), not just name the pattern — this is what makes the commit log useful for the next person auditing the same codebase.
