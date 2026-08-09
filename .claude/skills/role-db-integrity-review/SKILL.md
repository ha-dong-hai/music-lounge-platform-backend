---
name: role-db-integrity-review
description: Reviews database migrations, schema integrity, and data-layer correctness against the REAL target engine (SQL Server) rather than trusting the SQLite integration-test harness alone — the two diverge on real constraints (multi-path cascade rules, enum-string columns) that only surface on the real engine. Checks index coverage for actual query patterns, confirms financial mutations flow only through the ledger, and verifies backup/restore has genuinely been exercised. Covers role 06 (Database Engineer / DBA) from the MusicLounge SDLC role charter. Use when asked to review migrations, schema, indexes, or data integrity before a release, or explicitly invoke the DBA role.
license: Internal project tooling — MusicLounge repository, no separate license.
---

# Database Engineer / DBA Review

Mandate: *"Dữ liệu phải đúng và nhất quán ở quy mô production thật — 'chạy được trên máy dev' không phải bằng chứng đủ."*

This project has already hit a real bug this class of review exists to catch: `PhysicalTicketDetail` had two `SetNull` foreign keys to `users` that SQL Server rejected outright with error 1785 ("multiple cascade paths"), because a third cascading path already reached `users` via `Ticket→BuyerId`. The entire 287-test SQLite-based suite passed anyway — SQLite's `EnsureCreatedAsync` doesn't enforce this constraint. **Never treat a green SQLite test run as proof a migration is safe on SQL Server.** That is the single most important thing this skill exists to prevent from recurring.

Work in order: **(1) Apply migrations on the real engine → (2) Audit enum-string columns → (3) Check index coverage → (4) Check financial mutation paths → (5) Confirm backup/restore → (6) Report.**

## 1. Apply every pending migration against a real SQL Server instance

`dotnet test` passing is not sufficient evidence. Run the actual migration command against a real (dev/staging) SQL Server:
```
dotnet ef database update --project src/MusicLounge.Infrastructure --startup-project src/MusicLounge.Api
```
If it fails, read the actual SQL Server error (not a guess at what it might be) — errors like 1785 (multiple cascade paths) are specific and tell you exactly which FK to change (`SetNull`→`Restrict` is usually the safe fix here, since this codebase's DSAR erasure design never hard-deletes `User` rows — verify that invariant still holds before assuming `Restrict` is safe for any given FK). If a migration must be regenerated because of a FK-behavior fix, prefer consolidating into one honestly-named migration over leaving broken intermediate ones in history.

## 2. Audit every `HasConversion<string>()` enum column

Grep all `*Configuration.cs` files under `src/MusicLounge.Infrastructure/Persistence/Configurations/` for `HasConversion<string>()`. For each column found, confirm that **every** place in the repo that writes to it via raw SQL (seed scripts, `README-SETUP.md`'s manual-SQL fallback steps, ops runbooks) uses the enum's string name (`'Active'`), not its ordinal (`0`). This is a silent-failure class: inserting an int into a string-converted column doesn't error — SQL Server coerces it to the wrong string, and every EF query filtering on that enum then returns zero rows with no exception anywhere. If you find any raw-SQL reference using a numeric literal for one of these columns, flag it as a concrete, reproducible bug, not a style nit.

## 3. Check index coverage against actual query patterns

For every foreign key column and every column that appears in a `.Where()`/`.OrderBy()` in the repository classes (`src/MusicLounge.Infrastructure/Persistence/Repositories/`), confirm an index exists covering it. Missing indexes on FKs are the most common gap — check every `HasOne(...).HasForeignKey(...)` in the `*Configuration.cs` files has a corresponding `HasIndex`.

## 4. Confirm financial mutations only flow through the ledger

Grep for any code path that mutates a balance/amount-bearing field directly (outside of the code that also writes a corresponding `LedgerEntry`). Any such path is a bookkeeping integrity risk — money should never move without a ledger record explaining why.

## 5. Confirm backup/restore has actually been exercised

This cannot be verified from code alone. Ask directly: when was the last restore-test performed, and against which backup? A backup schedule that has never been restore-tested is not a verified backup strategy — flag this explicitly as needing operational confirmation rather than silently assuming it's fine.

## 6. Report

Cite exact file:line for every schema/index/mutation-path finding. For the SQL-Server-apply step, report the literal command output (success or the exact error), not a paraphrase.

## Quick reference

| Need | Go to |
|---|---|
| Real precedent for the cascade-paths class of bug | `src/MusicLounge.Infrastructure/Persistence/Configurations/PhysicalTicketDetailConfiguration.cs` — its comment documents the exact incident |
| DSAR erasure design (relevant to whether `Restrict` is safe on a given FK) | `src/MusicLounge.Application/Users/Commands/RequestDataErasure/RequestDataErasureCommandHandler.cs` |
| Manual SQL seed examples that must use enum strings, not ints | `README-SETUP.md` Bước 6.7 Cách B |
