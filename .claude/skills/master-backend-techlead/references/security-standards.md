# Security Standards — OWASP Top 10:2025 (grounded; re-verify before citing)

Released November 2025 — the first update since 2021, and the 8th edition overall. Two new categories, one consolidation. **Re-search owasp.org/Top10 before citing in any review**, since this may itself be superseded by the time you use it.

| Rank | Category | Key change from 2021 |
|---|---|---|
| A01 | Broken Access Control | Stays #1. SSRF has been folded into this category. |
| A02 | Security Misconfiguration | Jumped from #5 to #2 — the biggest mover, reflecting how much app behavior is now config-driven. |
| A03 | Software Supply Chain Failures | Expanded from "Vulnerable and Outdated Components" — now covers dependency, build-system, and distribution-infrastructure compromise broadly, not just outdated libraries. |
| A04 | Cryptographic Failures | Falls from #2 to #4. |
| A05 | Injection | Falls from #3 to #5. Still covers everything from XSS to SQLi. |
| A06 | Insecure Design | Falls from #4 to #6 — industry has improved here via threat modeling. |
| A07 | Authentication Failures | Renamed from "Identification and Authentication Failures," holds #7. |
| A08 | Software or Data Integrity Failures | Holds #8 — trust-boundary/integrity verification below the supply-chain level. |
| A09 | Security Logging & Alerting Failures | Renamed to emphasize *alerting*, not just logging — logs nobody acts on are close to worthless. Holds #9. |
| A10 | Mishandling of Exceptional Conditions | **New category.** Improper error handling, logical errors, failing open on error, and related abnormal-condition bugs. |

## How to use this in a review

- Don't just name the category — map the specific finding to it and explain the concrete exploit path a junior dev can picture (e.g., "this endpoint checks the JWT signature but not the `tenant_id` claim against the resource being fetched — that's A01 Broken Access Control: any authenticated user can read another tenant's data by changing the ID in the URL").
- **A10 (new) deserves special attention in backend review** — check specifically for: errors that are caught and silently swallowed, code that "fails open" (allows the action) instead of "fails closed" (denies it) when a check errors out, and logical errors mistaken for successful paths (e.g., a payment marked "success" because no exception was thrown, without checking the actual response status).
- **A02 rising to #2 matters for backend specifically** — review environment/config handling explicitly: default credentials, verbose stack traces returned to clients in production, permissive CORS, unnecessary exposed admin endpoints/ports, secrets in code/config committed to version control.
- **A03 (supply chain)** — check dependency pinning, whether CI verifies package integrity/provenance, and whether the project has any process for tracking known-vulnerable dependencies (not just "npm audit ran once").
- Treat the Top 10 as a baseline, not a full standard — for anything requiring measurable/testable rigor, point toward OWASP ASVS (Application Security Verification Standard) and note that it too should be checked for current version.

## Backend-specific checks that map to these categories
- Authorization checked at the resource level, not just "is authenticated" (A01).
- Least-privilege service accounts / DB credentials, no default configs shipped to prod (A02).
- Lockfiles committed and CI enforces them; dependency updates reviewed, not blindly auto-merged without any scan (A03).
- Passwords/secrets hashed or encrypted with current recommended algorithms — verify current recommendation, don't assume what was correct years ago still is (A04).
- All external input parameterized/escaped at every boundary it crosses, including internal service-to-service calls, not just the public API edge (A05).
- Security requirements captured alongside functional requirements at design time, not bolted on after (A06).
- Auth uses a standard, current library/framework rather than hand-rolled logic (A07).
- CI/CD pipeline verifies artifact integrity before deploy; no unsigned/unverified build steps (A08).
- Security-relevant events (auth failures, permission denials, admin actions) are both logged AND wired to alerting, not just written to a file nobody reads (A09).
- Every external call (DB, downstream API, cache) has explicit error handling that fails closed and doesn't silently continue with bad/default data (A10).
