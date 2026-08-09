# OWASP ASVS 5.0.0 (05/2025) — 17 categories mapped to this codebase

Verified against search results at time of writing (08/2026): ASVS 5.0.0 restructured from 4.0.3's
14 categories to 17, ~350 requirements total. **Re-confirm the category list at asvs.dev before
citing a specific requirement ID in a report** — this file records category *names and scope*, not
exact requirement numbers, since those are too granular to hand-maintain here without drifting from
the live standard.

| # | Category | Applies to this repo via | Notes |
|---|---|---|---|
| V1 | Encoding and Sanitization | Any endpoint returning user-supplied text (chat messages, ratings, complaint text, show descriptions) | Check output encoding at the API boundary; XSS itself is a frontend concern (V3) but the backend must not be the source of unescaped payloads |
| V2 | Validation and Business Logic | FluentValidation validators + Domain invariants | Check validators exist for every command, and that business-logic checks (quota, state-machine transitions) can't be bypassed by calling handlers out of the UI-assumed order |
| V3 | Web Frontend Security | **Not applicable to this repo** — no frontend code here | Flag for the frontend repo instead; don't fabricate coverage |
| V4 | API and Web Service | Every controller in `src/MusicLounge.Api/Controllers/` | Check consistent error contract, versioning, rate limits are enforced not just documented |
| V5 | File Handling | `UploadsController`, image/model upload validation | Check file-type allowlist (not blocklist), size limits enforced server-side not just client-side, no path traversal in stored filenames |
| V6 | Authentication | `JwtTokenService`, `AuthController`, Google OAuth, `AuthAttemptTracker` | Check lockout (5 attempts / 15 min) is enforced server-side, password hashing uses a current algorithm, no timing side-channel on login |
| V7 | Session Management | JWT expiry, `SecurityStamp`/`sec_stamp` rotation, `ActiveUserBehavior` | Check revocation is real-time (mid-session), not only enforced at next token issuance |
| V8 | Authorization | 5 `Policies` + `VenueOperatorAccess`, resource-level checks in every handler | The highest-value category for this system — check resource-level, not just role-level, authorization; test cross-venue/cross-owner access explicitly |
| V9 | Self-Contained Tokens | JWT claim design | Check claims carry only what's needed (userId, role, sec_stamp, loungeId for Staff), no sensitive PII embedded in the token itself |
| V10 | OAuth and OIDC | Google OAuth login path | Check state/nonce validation, redirect URI allowlist |
| V11 | Cryptography | `PiiEncryptionService`, `DataProtectionSecretProtector`, password hashing | Check current-recommended algorithms are used, not whatever was correct when the code was first written |
| V12 | Secure Communication | HTTPS/HSTS enforcement, VNPay/Mux/SMS integration transport | Check `UseHsts()` is active in production, no plaintext fallback for external API calls |
| V13 | Configuration | `appsettings.*.json`, secrets, CORS, environment separation | Check no secrets committed to git, CORS origin allowlist is intentional not wildcard, production doesn't leak stack traces |
| V14 | Data Protection | PII fields (CCCD, phone, email), DSAR erasure, `LedgerEntry` retention | Check classification is consistent — what's encrypted, what's anonymized-on-erasure, what's retained for the 10-year accounting requirement |
| V15 | Secure Coding and Architecture | Dependency pinning, concurrency locks (`IShowBookingLock`, `IAsyncKeyedLock`), SBOM/dependency scanning | Check lockfiles are committed and CI verifies them; check concurrent-actor code paths (ticket holds, staff assignment) use the locking pattern consistently |
| V16 | Security Logging and Error Handling | Serilog config, `GlobalExceptionHandler` | Check security-relevant events (auth failures, permission denials, admin actions) are logged with enough context to investigate, and errors don't leak internals to the client |
| V17 | WebRTC | Livestream (Mux / Cloudflare Stream) | Likely not applicable if livestream uses HLS/RTMP via the provider rather than raw WebRTC — confirm the actual transport before marking in/out of scope |
