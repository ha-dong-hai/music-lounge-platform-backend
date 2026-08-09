# Mentoring Guide — Delivering Review Findings to Interns/Freshers

The goal of a review that includes a junior developer isn't just a fixed codebase — it's a developer who catches the same class of issue unprompted next time. Every finding should teach.

## Structure each finding as: Observation → Principle → Concrete scenario → Better pattern
1. **Observation** — what the code currently does, factually, no judgment language.
2. **Principle** — the underlying rule this touches (e.g., "operations a client might retry need to be idempotent").
3. **Concrete scenario** — walk through what actually happens in a specific real situation ("if the network times out after the charge succeeds but before the response reaches the client, the client retries, and the customer is charged twice"). Abstract pattern names ("race condition," "N+1 query") don't build intuition on their own — the scenario does.
4. **Better pattern** — what to reach for instead, and *why* that fixes it, not just "do this."

## Calibrate tone to seniority
- With an intern: assume they don't yet have the pattern-recognition that comes from having been burned by this failure before — spend more time on the scenario, less on jargon. It's fine to over-explain.
- With a fresher who's shipped a few things: they likely know the pattern name but not yet when it applies — connect the dots between "I know what a race condition is" and "I didn't see one here."
- Never let tone read as gatekeeping ("any senior dev would know this") — that teaches avoidance of review, not learning from it.

## Praise real judgment, not just correctness
When a junior developer's code shows they *did* think about an edge case, name it explicitly — reinforcing good instincts is as valuable as correcting bad ones, and it's easy for a review focused on catching problems to only ever produce negative signal.

## Distinguish severity out loud
Use the same must-fix / should-fix / nice-to-have framing from the main skill consistently, and say *why* something is must-fix in business terms ("this one loses the company money if it ships") vs. should-fix in engineering terms ("this one will make the next feature harder to add"). Junior developers build judgment about prioritization faster when they see the reasoning, not just the label.

## Turn recurring findings into a teaching moment, not a repeated correction
If the same category of issue shows up multiple times across someone's code, say so explicitly and point to the underlying principle once clearly, rather than repeating the same fix five times without naming the pattern — the goal is for them to generalize the lesson, not memorize five individual fixes.

## Leave room for their reasoning
Before assuming a finding is a mistake, ask what the intent was if it's not obvious — sometimes what looks wrong is a deliberate tradeoff the reviewer doesn't have context for, and treating it as an unquestioned error either embarrasses someone who had a reason, or, worse, teaches them to stop explaining their reasoning.
