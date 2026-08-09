# Dynamic per-role testing playbook

Proven methodology from the 2026-08-09 dynamic role-based testing pass (committed as `d50dab3`).
This is the exact sequence that caught nothing new *because* every previously-fixed bug held up —
which is only meaningful evidence because the test ran against a **real SQL Server database**, not
the SQLite integration-test harness. Reasoning about behavior from reading code is exactly the blind
spot that let a real SQL-Server-only bug (migration error 1785, "multiple cascade paths") pass 287/287
SQLite-based tests earlier in the same session. Don't substitute static reading for this.

## 1. Start the real app against a real database

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/MusicLounge.Api --urls http://localhost:5289 > /tmp/musiclounge-api.log 2>&1 &
curl http://localhost:5289/api/v1/lounges   # confirm 200 before proceeding
```
Confirm it's pointed at a real SQL Server instance (check `appsettings.Development.Local.json`'s
`DefaultConnection`), not an in-memory/SQLite fallback.

## 2. Register one real account per persona

```bash
curl -s -X POST http://localhost:5289/api/v1/auth/register -H "Content-Type: application/json" \
  -d '{"Email":"role-audience@test.local","Password":"P@ssword123","FullName":"Role Test Audience","Phone":null}'
```
Repeat for Owner (×2, different venues — cross-venue tests need two distinct Owners), Staff, and
Admin. For Admin specifically: **do not** try to guess the password of a pre-existing account —
register a fresh account with a password you control, then promote it via direct SQL:
```sql
UPDATE users SET EmailVerifiedAt = SYSDATETIMEOFFSET(), Role = 'Admin' WHERE Email = 'role-admin@test.local';
```
Log in again after the SQL promotion — role is baked into the JWT at login time and does not
retroactively update on an already-issued token.

## 3. Extract real JWTs

```bash
curl -s -X POST http://localhost:5289/api/v1/auth/login -H "Content-Type: application/json" \
  -d '{"Email":"role-audience@test.local","Password":"P@ssword123"}' \
  | grep -o '"token":"[^"]*"' | sed 's/"token":"//; s/"$//' > /tmp/tok_aud.txt
```
(`grep -P` is unavailable in some Git-Bash locales — use `grep -o` + `sed` instead, not a Python
one-liner; a bare `python`/`python3` alias may resolve to a non-functional Windows Store stub.)

## 4. Seed anything the dynamic flow needs but that's gated behind an external payment provider

Some prerequisites (an Active `OwnerSubscription`, in this system) normally require a real VNPay
payment confirmation. Rather than fabricating a fake gateway callback, seed the row directly —
**but read the entity's EF configuration first** for any column using `HasConversion<string>()`.
Enum-typed columns configured this way store the enum's *name* (`'Active'`), not its ordinal (`0`) —
inserting the ordinal silently succeeds and stores the wrong string, and every EF query filtering on
that enum then returns zero rows with no error anywhere. This cost real debugging time once already;
check the configuration before writing the INSERT, not after the query mysteriously returns nothing.

## 5. Walk each persona's golden path, including explicit cross-boundary attempts

Don't just confirm each persona *can* do their own job — that only proves the happy path. The
higher-value check is confirming each persona is *blocked* from every other persona's resources:
- Staff of venue A reading venue B's Draft show → expect 404 (not 403 — a well-designed system hides
  the resource's existence entirely rather than confirming it exists but is forbidden).
- Staff of venue A reading venue B's livestream credentials → expect 403.
- A revoked Staff member's still-unexpired JWT used again → expect 401, and check this on more than
  one endpoint, including a generic one like `/me` — the revocation check should be global (in the
  request pipeline), not bolted onto individual handlers.
- Owner A reading Owner B's analytics/bank account → expect 403.
- Anonymous/no-account requests to public endpoints → expect the public data, not an auth wall.

For any endpoint requiring an unfamiliar query parameter or body shape, read the actual command/DTO
definition rather than guessing the shape — a guessed-wrong request produces a misleading error (e.g.
a missing required `?loungeId=` query param silently defaulting to `0` and returning a confusing 404)
that can be mistaken for a real bug if not double-checked against the actual handler signature.

## 6. Clean up

Identify the actual OS process bound to the port before killing anything:
```bash
powershell -NoProfile -Command "Get-NetTCPConnection -LocalPort 5289 | Select-Object -ExpandProperty OwningProcess"
powershell -NoProfile -Command "Get-Process -Id <PID> | Select-Object ProcessName, Path"   # confirm it's the test instance, not something else
powershell -NoProfile -Command "Stop-Process -Id <PID> -Force"
```
Report explicitly what was created (test accounts, seeded rows, venues) and whether it was cleaned up
or left in the database — don't leave test data silently indistinguishable from real records without
flagging it.
