# Vietnamese statutes referenced in this codebase — law-to-enforcement map

Confirmed present as citation strings in code as of the 2026-08-09 production-hardening audit
(`git grep -E "(NĐ|Nghị định|Luật) [0-9A-Za-z./]+"`). **Re-run that grep at audit time** — this list
is a snapshot, not guaranteed current; new citations may have been added or removed since.

| Statute | What it requires | Expected enforcement point | Status at last audit |
|---|---|---|---|
| Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 + Nghị định 356/2025/NĐ-CP | DSAR: 2 business-day ACK, 10–20 day processing depending on request type (15/30 with a third party involved); Điều 19 refusal exceptions | `POST /me/data-erasure`, `GET /me/data-export`, `RequestDataErasureCommandHandler.cs` | Erasure + export implemented (anonymize-in-place design, not hard-delete). Verify the ACK/processing *timers* are actually enforced somewhere (job or handler), not just that the endpoints exist — a request that's never acted on within the statutory window is still non-compliant even if the endpoint technically works. |
| Luật Kế toán (Vietnamese Accounting Law) | Financial records retained 10 years | Ledger/Payment/Ticket/Donation rows must survive user erasure — check `RequestDataErasureCommandHandler` never deletes rows these tables FK to, only anonymizes the `User` row | Implemented via anonymize-in-place; re-verify no new code path introduces a hard-delete of `User` |
| NĐ 52/2024/NĐ-CP | Payment intermediary regulation | VNPay integration (`VnPayService`) | Check integration pattern against current requirement text — re-fetch if citing specifics, this file doesn't carry article-level detail |
| NĐ 117/2025 | Tax withholding | Settlement/payout calculation for Owners | Locate the actual withholding calculation in the settlement flow; if absent, this is a compliance gap, not just a missing feature |
| NĐ 144/2020/NĐ-CP Điều 10 | Performance license required before ticketed public shows; submission ≥7 business days before the event | `PublishLoungeShowCommandHandler.cs` — requires `LegalApprovalReference` and checks `BusinessDayCalculator.CountBusinessDaysBetween(...) >= 7` | Implemented and enforced at publish time |
| NĐ 147/2024/NĐ-CP | Content moderation | Pre-publish review: `EventModeration` entity + `SlaDeadline` (default 24h via `moderation_sla_hours` config) | Pre-publish SLA implemented. **Known gap as of last audit: no reactive takedown-on-complaint mechanism** (`ComplaintResolvedAction.TakeDownContent` + SLA tracking on `Complaint`) — re-verify this is still missing, don't assume the prior finding is still accurate without checking |
| NĐ 85/2021/NĐ-CP | Mandatory complaint channel | `Complaint` entity + `ComplaintsController` | Verify the channel is actually reachable (not just modeled in the schema) and has a real response SLA, not only a data model |

## How to use this map

For each row: grep the citation string in code, then trace forward from the cited location to confirm
there is an actual *enforcing* check (something that can reject/block/require) attached to it — not
just a comment referencing the law with no behavior behind it. A citation with no enforcement is worse
than no citation, because it reads as compliance without providing it.
