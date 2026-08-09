---
name: role-legal-compliance-vn
description: Maps every Vietnamese legal/contractual obligation evidenced in this codebase — data protection (DSAR), accounting retention, payment intermediation, tax withholding, performance licensing, music-copyright licensing (VCPMC), venue business registration, content moderation, mandatory complaint channel, and consent surfaces (ToS/Privacy Policy) — to the specific code that actually enforces it, confirms each mapping is real enforcement rather than a comment citing a law with no behavior behind it, and sweeps for legal-document-shaped fields even when they carry no inline citation. Covers role 12 (Legal & Data Privacy Officer) from the MusicLounge SDLC role charter. Use when asked to audit legal/regulatory compliance for the Vietnamese market, review DSAR handling, check copyright/licensing compliance, or explicitly invoke the Legal/DPO role.
license: Internal project tooling — MusicLounge repository, no separate license.
---

# Legal & Data Privacy Officer — Vietnamese Compliance Audit

Mandate: *"Tuân thủ đúng khung pháp lý Việt Nam hiện hành — 'có vẻ ổn' không phải bằng chứng tuân thủ."*

Work in order: **(1) Re-discover citations → (2) Confirm each has real enforcement → (3) Verify DSAR timers → (4) Check consent surfaces → (5) Re-check known gaps → (6) Report as a compliance matrix.**

## 1. Re-discover legal citations in the current codebase

Don't rely solely on `references/vn-law-code-map.md` — it's a snapshot from a specific audit date. Re-run the discovery grep to catch anything added or removed since:
```
git grep -E "(NĐ|Nghị định|Luật) [0-9A-Za-z./]+"
```
Diff the result against the reference map; note any new citation not yet mapped, and any mapped citation that's disappeared (which could mean the law was removed on purpose, or that enforcing code was accidentally deleted — determine which).

**This citation-string grep is not sufficient by itself** — it only finds statutes someone already wrote a comment about. It missed the VCPMC music-copyright reference and the venue business-license field in an earlier pass of this skill precisely because neither carries an inline law citation in code, only a field/command name. Also sweep for the *shape* of a legal-document field regardless of whether it's commented:
```
git grep -niE "(Reference|LicenseUrl|CertificateUrl|PermitUrl|LicenseNumber)\b" -- '*.cs' | grep -vi test
```
For every match found this way that isn't already a row in the reference map, determine what real-world legal/contractual requirement it represents (ask if it isn't obvious from the surrounding code or a design-note comment) and add it — a field that clearly represents "proof of some legal permission" is in scope for this audit even with zero inline citation, and its absence from the map is itself worth flagging as a documentation gap.

Also check every uncited match's actual enforcement, exactly as in step 2 — the VCPMC and business-license fields both turned out to be declarative-only (settable, but nothing observed to actually block on their absence) as of the last sweep; confirm whether that's still true and whether it's the deliberate design or an oversight.

## 2. Confirm each citation is attached to real enforcement

For every statute, trace from the citation to the actual behavior it's supposed to produce. A law cited in a code comment with no corresponding check, validation, or blocking condition nearby is **not compliance** — it's documentation of intent that provides no actual protection. This distinction matters more than it sounds: an auditor (human or AI) skimming for citations and stopping there will systematically overstate this system's compliance posture.

## 3. Verify DSAR timers are actually enforced, not just that endpoints exist

Luật 91/2025/QH15 + Nghị định 356/2025/NĐ-CP require a 2-business-day acknowledgment and a 10–20 business-day resolution window (15/30 if a third party is involved). Having `POST /me/data-erasure` return promptly for the *simple* case doesn't prove the *statutory timers* are honored for cases that require review. Check whether there's any tracking of request-received-at vs. deadline, and whether anything would surface a request that's about to breach its window. If nothing tracks this, flag it — a synchronous endpoint that happens to respond fast is not the same as a compliant SLA process.

## 4. Check consent surfaces exist and are real

DSAR handling only matters once data collection itself is lawfully consented to. Confirm: a Terms of Service and Privacy Policy actually exist and are reachable (not just referenced in a footer link that 404s); registration/data-collection flows require an affirmative, unchecked-by-default acceptance (a pre-ticked checkbox is not valid consent under most current data-protection regimes, including the spirit of Luật 91/2025/QH15); and the text is in plain, comprehensible Vietnamese rather than only dense legal boilerplate translated from a template — a consent a typical user cannot understand is weak evidence of informed consent if ever challenged.

## 5. Re-check previously-known gaps — don't trust stale memory

As of the last audit, two gaps were identified and explicitly left open:
- NĐ 147/2024/NĐ-CP reactive takedown-on-complaint (gỡ nội dung sau khi đăng theo khiếu nại) — pre-publish moderation SLA exists, post-publish reactive removal does not.
- Performer CRUD (§6.12) — not strictly a legal gap, but check whether its absence has any compliance-adjacent consequence (e.g., can a Performer's identity/consent be properly recorded without it).

Re-verify both by grepping for the specific expected symbols (`ComplaintResolvedAction.TakeDownContent`, `CreatePerformerCommand`) rather than assuming the prior finding is still accurate — code changes between audits.

## 6. Report as a compliance matrix

One row per statute: citation, enforcing code location (file:line) or "none found," verification method used (static read vs. dynamic test), and status (compliant / partial / gap). Don't editorialize about business-decision tradeoffs here (that's the BA/Architect or Release Manager's call) — state the factual compliance status and let the go/no-go decision happen elsewhere.

## Quick reference

| Need | Go to |
|---|---|
| Snapshot law-to-code map (re-verify, don't trust blindly) | `references/vn-law-code-map.md` |
| DSAR design rationale (why anonymize-in-place, not hard-delete) | `RequestDataErasureCommandHandler.cs` and its inline comments |
| Financial-retention overlap with Luật Kế toán | `role-financial-ledger-audit` skill |
