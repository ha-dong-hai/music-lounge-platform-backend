# Stitch Design Brief — MusicLounge, Staff Mobile

> Distilled for Google Stitch from [View-Design-Spec.md](View-Design-Spec.md) §2 (view catalog) and §3 (Staff flow diagrams), scoped to the **Staff mobile surface** — see [platform-architecture.md](platform-architecture.md) for why this is a native operational tool, separate from the Audience site and the Owner web dashboard.
> Rewritten for a design tool, not a dev handoff: no raw field/endpoint names, no BE-only validation trivia — only what actually shapes a screen.

---

## 1. App overview

**MusicLounge Staff** is the on-the-floor operational tool for venue staff working a show: sell walk-in tickets at the counter, scan tickets at the door, and manage food/drink orders from the kitchen or bar side. A staff member is assigned to exactly one venue at a time by that venue's owner — there's no self-signup here. This app is used standing up, moving around a physical space, often in low light and with one hand occupied — every screen should assume that.

**Platform**: Native mobile app (or tablet), used at the venue during show hours. Large tap targets, minimal typing, fast task completion over visual polish.

**Out of scope here**: creating/editing shows, venues, or performer profiles (Owner web dashboard); anything platform-wide (Admin console). A staff member's job is executing what the owner already set up, not configuring it.

## 2. Suggested visual direction *(a starting point — adjust freely)*

Utilitarian and fast, closer to a point-of-sale or event-check-in app than to the atmospheric Audience site. High contrast for readability in a dim venue, clear large status colors (a green "Confirmed" vs. a red "Already used" needs to read instantly, not require careful reading). A light touch of the MusicLounge palette keeps it on-brand, but clarity wins over mood here every time.

## 3. Navigation

Simple bottom tab bar, since a staff member switches between a small number of modes during a shift: **Sell** (walk-in tickets), **Check-In** (door scanning), **F&B** (kitchen/counter orders). Account access (profile, notifications) can live behind a smaller icon rather than taking a full tab slot — it's used rarely mid-shift.

---

## 4. Screens by flow

### Flow A — Getting Started

**Log In**
- Purpose: authenticate — same login screen as the Audience/Owner sites (email/password or Google), no separate signup flow exists for Staff.
- Note: a staff member only gets this access after being assigned by a venue owner (outside this app entirely) — there's nothing to design for "becoming staff," only for using the access once granted. If access is ever revoked by the owner, the next login (or next API call) simply stops working for operational actions — design a clear "you no longer have access to this venue" state rather than assuming access, once granted, is permanent.

### Flow B — Selling Tickets at the Counter

**Walk-In Sale**
- Purpose: sell a ticket directly to someone at the counter, no app or account needed on their side.
- Content: available ticket tiers for the current/upcoming show, with remaining count — **only in-person tiers show here**, online-only tiers aren't sellable this way.
- Actions: pick tier + quantity, "Sell Now."
- States: sold out tiers shown disabled with a reason, not hidden. This is a cash transaction, not a card payment flow — no external payment redirect, the ticket confirms immediately.
- Note: this doesn't earn platform commission by default — don't show commission figures on the receipt, that would be misleading for this sale type.

**Sale Confirmation / QR**
- Purpose: hand the buyer their ticket immediately.
- Content: large QR code, ready to be shown to the buyer's phone camera or printed.
- Actions: "New Sale" (reset for the next customer).
- Note: this buyer has no account in the system at all — there's no "email the ticket" step, the QR is the only artifact, so make sure it's easy to screenshot/save from the buyer's side.

### Flow C — Door Check-In

**Check-In Scanner**
- Purpose: scan a ticket's QR code to admit someone at the door.
- Type: camera-based scanner as the primary interaction.
- Content: live camera view; on a successful scan, a preview step shows ticket details (holder, tier) *before* committing the check-in — these are two separate steps, don't auto-confirm on scan alone.
- Actions: "Confirm Check-In" after preview.
- States: **each rejection reason needs its own clear message**, not a generic "invalid ticket" — already checked in before (most common, needs to read as "already used," not an error); wrong show/time; online-only ticket presented at a physical door (shouldn't need door check-in at all); ticket mid-transfer ("frozen," ask them to wait). After a successful check-in, briefly show a confirmation (name/tier) for 1-2 seconds, then automatically reset for the next scan — don't require a manual "done" tap between every single guest.
- Note: **there is no offline fallback** — if the connection drops mid-scan, that's a known, accepted limitation. Show connection status honestly; never let a scan appear to succeed when it didn't actually reach the server.

### Flow D — F&B Order Management

**Order Board**
- Purpose: see and progress every food/drink order for the venue right now.
- Type: list or kanban-style board grouped by status.
- Content: orders with items, table/zone note, current status.
- Actions: "Take Order for Table" (staff placing an order on a customer's behalf, same flow whether the customer ordered themselves via their own device or not); advance an order's status — **the sequence is fixed** (Pending → Preparing → Served → Paid), so show exactly one "next step" button per order rather than a free-choice dropdown; "Cancel" is available as a separate exit at any point before Paid.
- States: empty state per status column (e.g. nothing "Preparing" right now). Marking an order "Paid" is just a status flag here — it doesn't verify money actually changed hands, so don't design any payment-confirmation ceremony around it, it's a simple tap.
- Note: the customer tracking their own order (in the separate Audience F&B app) doesn't get a live push when staff update status here — no real-time indicator needed on this screen for "customer has seen this," there isn't one.

---

## 5. Cross-screen behaviors worth designing consistently

- **Speed over confirmation dialogs**: this app is used under time pressure with a line of people waiting — minimize taps-per-transaction everywhere it's safe to (e.g. check-in auto-resets, no "are you sure" on routine status advances).
- **Single-venue context**: a staff member's session is scoped to exactly one venue for their whole shift — no venue switcher needed anywhere in this app, unlike the Owner dashboard.
- **Connection-state honesty**: check-in and walk-in sales are the two screens where a false "success" is genuinely costly (double-admits, unrecorded cash sales) — always make network/sync state visible rather than optimistically assuming success.

---

*Source: [View-Design-Spec.md](View-Design-Spec.md) §2 (Staff-relevant views across group S, plus the shared Livestream Operations view with Owner, the shared account-management views from A/B/I) and §3.3 (Staff flow diagrams). Livestream operations (creating/starting/ending a stream) are shared 1:1 with the Owner dashboard's own screen — see [stitch-brief-owner-web.md](stitch-brief-owner-web.md) Flow E for that spec; not repeated here since Staff would use the exact same interaction, just from this app's visual shell if it's ever needed on mobile. See [platform-architecture.md](platform-architecture.md) for the platform-split rationale.*
