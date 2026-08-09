# Code Quality & Review Standards

## Design principles to check against
- **SOLID** — Single Responsibility (does this class/function do one job?), Open/Closed (can behavior extend without modifying existing tested code?), Liskov Substitution (do subtypes actually honor the base type's contract?), Interface Segregation (are callers forced to depend on methods they don't use?), Dependency Inversion (does high-level logic depend on abstractions, not concrete low-level details?). Don't cite these as dogma — flag violations only where they cause a real maintainability or correctness problem, not for their own sake.
- **DRY vs. premature abstraction** — duplicated logic that will change together should be unified; superficially similar code that happens to look alike but represents different business rules should *not* be forced into one abstraction (a common junior-dev mistake in the opposite direction from not-DRY-enough).
- **Explicit over implicit** — magic numbers/strings, implicit type coercion, and hidden side effects (a "getter" that also writes to the DB) all make code harder to reason about and review; flag them.

## Code review practice (Google Engineering Practices-style, adapt to team)
- A change should have a clear, single purpose — a review that's actually two unrelated changes bundled together should be flagged for splitting, not reviewed as-is.
- Reviewers should distinguish **must-fix** (correctness, security, data integrity) from **nit/preference** (style, naming taste) explicitly in comments — mixing them trains authors to ignore all feedback equally.
- "Looks Good To Me" doesn't mean "perfect" — it means the change is a net improvement and any remaining nits can be follow-ups; don't block merges on cosmetic disagreement.
- Tests are part of the change, not optional — a review that only reads production code and skips whether the tests actually assert the right thing is an incomplete review.

## Testing standards
- **Testing pyramid** — favor many fast unit tests, fewer integration tests, and a small number of end-to-end tests; a suite inverted toward slow E2E tests is a maintenance and CI-speed liability.
- **Test what breaks, not just the happy path** — for each function under test, check: is there a test for the boundary condition, the error path, and the concurrent/idempotency case (where relevant)? A suite that's 100% happy-path is a false sense of safety.
- **Tests should fail for the right reason** — a test that mocks so much it can't actually catch a real regression is closer to a tautology than a test.

## Naming and readability
- Names should say what something *is* or *does* in business terms, not implementation shorthand (`activeSubscriptions`, not `list2` or `tmpData`).
- A reviewer (or a fresher six months from now) should be able to understand *why* a non-obvious piece of logic exists from a comment or commit message — code that requires tribal knowledge to safely modify is technical debt even if it currently works.

## When reviewing an intern/fresher's code specifically
- Check for over-engineering as often as under-engineering — junior devs sometimes over-apply a pattern they just learned (excessive abstraction, unnecessary design patterns) as much as they under-apply good practice.
- Distinguish "this is wrong" from "this works but there's a better pattern for next time" explicitly — conflating the two either discourages unnecessarily or fails to raise the bar.
