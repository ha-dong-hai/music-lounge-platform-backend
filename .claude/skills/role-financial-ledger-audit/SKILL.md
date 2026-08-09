---
name: role-financial-ledger-audit
description: Audits money-flow correctness like a financial auditor, not a general QA — verifies every LedgerEntry set is double-entry balanced, every monetary field uses decimal precision (never float) with consistent rounding, payment webhooks are idempotent under replay, every commission/split percentage is system_config-driven with audit history rather than hardcoded, and determines the real PCI DSS scope based on how card entry is actually integrated (redirect-only vs. PAN-touching) instead of assuming either extreme. Covers role 11 (Financial / Payment Correctness Auditor) from the MusicLounge SDLC role charter. Use when asked to audit payments, ledger, commission, settlement, rounding/precision, or donation-split correctness, or explicitly invoke the Financial Auditor role.
license: Internal project tooling — MusicLounge repository, no separate license.
---

# Financial / Payment Correctness Auditor

Mandate: *"Sai 1 đồng trong luồng tiền là sự cố nghiêm trọng, không phải bug thường. Kiểm toán như kiểm toán tài chính thật."*

Work in order: **(1) Determine real PCI DSS scope → (2) Double-entry balance check → (3) Currency precision audit → (4) Idempotency check → (5) Config-driven rate audit → (6) Reconciliation cadence → (7) Report.**

## 1. Determine the real PCI DSS scope — don't assume either extreme

Read the actual `VnPayService` integration code end to end. If card entry is fully redirected to VNPay's own hosted payment page and the card PAN never transits this backend at any point (not even in a log line, not even transiently in memory before forwarding), the applicable scope is the lowest tier (SAQ-A under PCI DSS v4.0.1 — re-verify this is still the current version before citing it in a report). If any code path touches, logs, or stores raw card data, the scope is materially larger and this must be flagged as a serious finding, not a compliance detail — state explicitly which you found, with the file:line evidence, rather than asserting a scope without having read the integration.

## 2. Double-entry ledger balance check

For a representative sample of completed transactions (a ticket purchase, a donation payout, a subscription payment), pull every `LedgerEntry` row tied to that transaction and confirm the debits sum to exactly the credits — zero net. Do this for at least one transaction from each money-moving flow, not just one overall. Any transaction where the ledger doesn't balance to zero is a correctness bug regardless of whether the visible UI numbers "look right" — the ledger is the source of truth this system was designed around, not a display convenience.

## 3. Currency precision audit

Every money-bearing field must use `decimal` (or an integer minor-unit representation) with an explicit, consistent precision — never `float`/`double`. Grep entity and DTO definitions for any monetary field typed as `float`/`double`, and check rounding is applied consistently (same rounding mode, same decision of *where* in a calculation chain rounding happens) across every place a rate is applied — e.g. the donation split math (`PaymentFeeCalculator.SplitDonationPayout`) and any other percentage-of-amount calculation. Inconsistent rounding is a real-money bug class distinct from an outright wrong formula: individually tiny, it accumulates silently across thousands of transactions until an audit or a sharp-eyed Owner notices the ledger doesn't quite reconcile.

## 4. Idempotency under replay

Payment gateway webhooks (`vnpay-ipn`, `vnpay-return`) will be called more than once in the real world — network retries, gateway-side retries, a user refreshing a return page. Test this directly: replay the exact same IPN callback payload twice against a real running instance and confirm the second call does not create a second `LedgerEntry`/credit. If it does, this is a critical finding (double-crediting is a direct financial loss).

## 5. Config-driven rate audit

Every commission or split percentage this system applies (`platform_commission_rate`, `donation_performer_share_rate`, `settlement_partial_pct`, and any others in the current `system_config` table) must be read through `ISystemConfigService`, never duplicated as a hardcoded literal anywhere else in the codebase. Grep for the numeric values of the currently-configured rates as literals outside the config-read path — a hardcoded duplicate that drifts from the configured value is a silent bug (the UI and the ledger would then disagree about how much money moved).

## 6. Reconciliation cadence

Confirm there is a scheduled process comparing this system's internal `Payment`/`Settlement` records against VNPay's own transaction report — not just trusting internal state as ground truth. If no such reconciliation job exists, flag it: without it, a gateway-side failure or a bug in the IPN handler could silently diverge from reality for an extended period before anyone notices.

This skill owns *correctness of the money movement*; whether that movement complies with Vietnamese payment-intermediary and tax-withholding regulation (NĐ 52/2024/NĐ-CP, NĐ 117/2025) is `role-legal-compliance-vn`'s remit — cross-check its findings rather than re-deriving them here, but flag immediately if a correctness finding here (e.g., the settlement math itself) looks like it would also break that skill's compliance mapping.

## 7. Report

For every finding, state the concrete transaction/scenario, the exact ledger or config evidence, and the financial impact in plain terms (not just "inconsistency found").

## Quick reference

| Need | Go to |
|---|---|
| VNPay integration (return vs. IPN distinction) | `src/MusicLounge.Infrastructure/Services/VnPayService.cs`, `PaymentsController.VnPayIpn` |
| Ledger entity | `src/MusicLounge.Domain/Entities/LedgerEntry.cs` |
| Donation split math | `src/MusicLounge.Application/Common/PaymentFeeCalculator.cs` — `SplitDonationPayout` |
| Current system_config keys | `ConfigKeys` constants in `src/MusicLounge.Application/Common/Interfaces/ISystemConfigService.cs` |
| Vietnamese legal grounding for payment intermediation and tax withholding | `role-legal-compliance-vn` skill |
