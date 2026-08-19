# Stitch Design Brief — MusicLounge, Owner Web Dashboard

> Distilled for Google Stitch from [View-Design-Spec.md](View-Design-Spec.md) §2 (view catalog) and §3 (Owner flow diagrams), scoped to the **Owner web surface** — see [platform-architecture.md](platform-architecture.md) for why Owner is a desktop-first business dashboard, separate from the Audience site and the Staff mobile tool.
> Rewritten for a design tool, not a dev handoff: no raw field/endpoint names, no BE-only validation trivia — only what actually shapes a screen.

---

## 1. App overview

**MusicLounge for Owners** is the business dashboard for people who run a live-music venue ("lounge") on MusicLounge — set up your venue, create shows, sell tickets, run livestreams, handle tips sent to your performers, and track your earnings. This is a B2B operator tool, not the consumer app: no browsing-for-fun here, every screen exists to help an owner run their business.

**Platform**: Website, desktop-primary. This involves form-heavy setup flows, a drag-and-drop seating editor, and financial dashboards — genuinely desk work, unlike the Audience site which also needs to work well on a phone. Still keep it reasonably responsive (an owner might check today's earnings from a phone), but don't compromise the desktop layout for it.

**Out of scope here** (separate surfaces, don't design into this app): walking around the venue to scan tickets or sell at the door (that's the Staff mobile tool — an Owner personally working the counter just logs into that same tool, no separate desktop version needed), and anything an Admin does to moderate/approve content platform-wide.

## 2. Suggested visual direction *(a starting point — adjust freely, this isn't a fixed brand)*

Should read as a serious, trustworthy business tool — this is where real money and legal paperwork flow through. Keep the same warm, live-music-adjacent identity as the Audience site (so it's recognizably "MusicLounge" and not a generic admin template), but dial back the atmosphere in favor of clarity: more neutral surface area, data given room to breathe, financial figures set with careful hierarchy (a settlement dashboard is not the place for cramped tables). Light mode as the default here reads more "business tool" than the Audience site's dark-mode-friendly suggestion — but this is a suggestion, not a rule.

## 3. Top-level navigation

Sidebar navigation (standard dashboard pattern), sections: **Venues**, **Shows**, **Donations**, **Finance**, **Performers**, **Staff**, **Subscription**, **Penalties**, plus a top-bar account menu (Notifications, Profile, Bank Accounts, Privacy & Data). If the owner has multiple venues, a venue switcher near the top of the sidebar scopes Shows/Finance/Staff to the selected venue.

---

## 4. Screens by flow

### Flow A — Onboarding

**Sign Up / Log In / Password Recovery**
- Same account flows as the Audience site (register choosing the "Owner" role, email OTP verification, login, forgot/reset password) — see [stitch-brief-audience-web.md](stitch-brief-audience-web.md) Flow A for full detail, same content and rules apply, just skinned to this dashboard's visual direction instead.

**Bank Accounts**
- Purpose: register where venue earnings (and performer payouts, on their behalf) get paid out.
- Content: list of accounts (venue's own + one per performer the owner manages), each with a verification-status badge.
- Actions: "Add Bank Account" (bank name, account number, account holder, owner type: venue or a specific performer, set-as-default); "Edit".
- States: unverified accounts show a "Pending admin verification" badge — **this is a real blocker**, not decoration: settlements and donation payouts stay stuck until verified. Surface this clearly, since it's the #1 reason an owner would see "why hasn't my money arrived." Empty state should be treated as urgent — this step blocks almost everything else, so a brand-new owner should see a prominent nudge to complete it before doing anything else.

### Flow B — Venue Setup

**My Venues**
- Purpose: overview of every venue this owner runs.
- Content: cards/rows per venue — name, photo, status badge (Pending review / Approved / Rejected), quick stats.
- Actions: "Add New Venue".
- States: `Pending` venues show "Waiting for admin approval," all management actions disabled until approved. `Rejected` shows the rejection reason — the only path forward is creating a brand-new venue, there's no "resubmit this one" flow. Empty state for a first-time owner should point straight at "Add New Venue."

**Create / Edit Venue**
- Purpose: core venue profile — name, description, address, photos, business license.
- Content: form fields for name/description/atmosphere tag/address (with map picker ideally); image uploader for the venue photo; document uploader for the business license; optional 3D model upload (a `.glb` file — different from the 360° tour feature below, don't conflate the two).
- Actions: "Save" / "Publish for Review" (creates at `Pending` status).
- States: right after creating, show a clear banner: "Your venue is pending approval — you can't create shows yet." Large file uploads (license doc, 3D model) need visible progress.

**Seating Zone Editor**
- Purpose: define the venue's seating areas and their real physical capacity — this number becomes a hard limit on ticket sales later, not just a sales-copy number.
- Type: visual drag-and-drop editor — place zones on a 2D floor plan (or 3D markers), size/rotate/color them.
- Content: list of zones (name, capacity) plus the visual layout canvas; an optional background floor-plan image to place zones over.
- Actions: add/edit/delete a zone; drag to reposition; set capacity per zone.
- States: works fine with no background image yet (free-floating coordinates), just less visually anchored. Make the capacity field's real-world weight obvious in the UI copy — this genuinely caps how many tickets can ever sell for that zone.

**360° Virtual Tour (management)**
- Purpose: build the panorama tour visitors can explore on the public site.
- Content: list of existing scenes (thumbnail + name).
- Actions: "Add Scene" (upload a single pre-made panorama image) or "Auto-Stitch" (upload several rotating photos of a room, the system stitches them into one panorama — this runs as a background job, takes 15-30+ seconds); once scenes exist, place them on a floor-plan-style mini-map and add clickable hotspots linking between scenes or showing an info popup.
- Actions: delete a scene/hotspot.
- States: scene count is capped by the owner's subscription tier — show "X of Y scenes used," disable "Add Scene" at the cap. The auto-stitch flow **must** show a processing state (this is not instant) with a clear success/failure outcome and a retry option on failure.

**Venue Extras (Gallery & Custom Criteria)**
- Purpose: two lighter-weight venue enrichment features bundled on one settings page — a photo gallery (showcase images, unrelated to the 360° tour) and custom recommendation criteria (venue-defined tags that audience members can express interest in, feeding personalized recommendations).
- Content: gallery grid with add/remove; a list of custom criteria (name, type of value it collects) with add/edit.
- Actions: add/remove gallery photo; add a new criterion (name, key, data type, options); edit a criterion's display name/options (but not its key or data type — those lock in at creation).
- States: gallery has no subscription-tier cap (unlike the 360° tour) — no quota UI needed here. Empty states independently for each of the two sections.

**Staff Management**
- Purpose: promote an existing Audience account to Staff for one venue.
- Content: list of current staff for the selected venue.
- Actions: "Look Up User" by email first (to confirm who you're about to assign) before "Assign as Staff"; "Remove Staff."
- States: lookup can come back blocked — the user is already an Owner/Admin, or already Staff at a different venue — show the specific reason, don't just disable the assign button silently.

### Flow C — Subscription

**Subscription Plans**
- Purpose: compare available plans before subscribing (also publicly viewable, even to a logged-out visitor, as marketing content — but the action buttons here assume an Owner is looking).
- Content: plan cards — price, billing cycle, ticket-per-event cap, whether AI poster generation is included (and its monthly cap), virtual-tour scene cap.
- Actions: "Choose Plan" → starts checkout.
- States: don't show plans the platform has deactivated.

**My Subscription**
- Purpose: current plan status, subscribe/renew/cancel.
- Content: active plan details — note these are a **snapshot** from when the owner subscribed, so they don't silently change if the platform later edits that plan's terms.
- Actions: "Subscribe" (blocked if already have an active plan — must cancel first); "Renew" (still requires a real one-time payment step, doesn't auto-charge); "Cancel."
- States: no subscription yet → empty state pointing at Subscription Plans.

**Payment Result**
- Same generic payment-result template used across the platform (Processing / Success / Failed three-state pattern) — see [stitch-brief-audience-web.md](stitch-brief-audience-web.md) Flow D for the full spec. On success here, land back on My Subscription.

### Flow D — Show Creation & Management

**My Shows**
- Purpose: overview of every show this owner has created, at any stage.
- Content: list with status (Draft / Pending review / Published / Ongoing / Ended / Cancelled).
- Actions: "Create New Show."
- States: `Draft` links back into editing; `Pending` is read-only while awaiting admin review. Empty state points at "Create New Show."

**Create / Edit Show**
- Purpose: core show details plus the performer lineup.
- Content: name, description, format (in-person / livestream / hybrid), schedule, category/genre tags, ticket quota split (in-person vs online), and a lineup builder — add performers with their role/set-time/whether they can receive tips for this show.
- Actions: search-or-create for each lineup performer (typing a name that doesn't match anything in the catalog silently creates a new performer profile on submit — the UI should make this obvious, e.g. "No match — a new performer profile will be created for '{name}'" rather than a hidden side effect); "Save Draft" / "Continue."
- States: block submission (with a clear banner, not a late error) if the owner doesn't currently have an active subscription. No hard limit on the number of performers in a lineup — don't impose an artificial cap in the UI.

**Show Control Center**
- Purpose: the hub for one show's whole lifecycle, from draft to ended — most owners will spend the most time here.
- Content: show status, ticket sales summary (orders list), and a menu of lifecycle actions.
- Actions: "Submit for Review" (needs the venue approved and at least one ticket tier); "Reschedule" (only while `Published`, not yet ongoing); "Change Format" (in-person→online is one-way, **automatically refunds 100% of confirmed in-person tickets** — needs a strong confirmation, not a casual one-click); "Change Playback Mode" (2D/3D, only relevant for online/hybrid); "Cancel Show" (refunds 100% of confirmed tickets, blocked while a livestream is actively live — must terminate that first); "Start"/"End" (in-person shows only — these buttons disappear entirely if the show has an attached livestream, which uses its own start/end pair instead, in the Livestream Operations screen).
- Actions (links out): to Ticket Tiers, Poster, Legal & Royalty Declaration, Livestream Operations (if online/hybrid).
- States: a show rejected by admin review shows the reviewer's note clearly, not just a status label — the owner needs to know *what* to fix.

**Ticket Tiers**
- Purpose: define ticket tiers and pricing for a show.
- Content: list of tiers (name, access type, zone if in-person, capacity, price).
- Actions: add/edit/delete a tier — only while the show is still a Draft.
- States: total capacity across tiers is capped by the subscription plan's per-event ticket limit — show a running "X of Y tickets used" indicator. At least one tier is required before the show can be submitted for review; nudge for this here rather than letting the owner discover it later at submission.

**Poster**
- Purpose: generate or upload a show poster.
- Content: current poster preview, history of past AI-generation attempts.
- Actions: "Generate with AI" (optional style hint) or "Upload Your Own"; a separate cover-image upload.
- States: "Generate with AI" only appears/enables if the subscription plan includes it *and* the owner hasn't hit their monthly or per-show generation cap — generation is not instant, show clear progress and a failure state with a reason if available.

**Legal & Royalty Declaration**
- Purpose: declare the performance permit and music-royalty reference required by law before a livestream can go live.
- Content: two reference-number fields with their current saved values.
- Actions: save each.
- States: if the royalty reference is missing on a show that's online/hybrid, show a warning here — it will hard-block starting the livestream later, better to catch it at the source.

### Flow E — Livestream Operations

**Livestream Operations**
- Purpose: set up streaming credentials, start/end the broadcast, and monitor viewers/chat while live.
- Content: RTMP URL + Stream Key (**never shown to the audience** — treat as sensitive, maybe behind a "reveal" click), live chat feed, viewer count.
- Actions: "Create Livestream" for a show; "Start Broadcasting" (blocked until an independent admin content review clears *this livestream specifically* — separate from the show's own review — **and** the legal royalty reference is filled in; show both conditions distinctly so the owner knows exactly what's still missing); "End Broadcasting."
- States: viewer count does not update live on this screen — it's a manual-refresh number, don't imply a live counter animation. If an admin forcibly terminates the stream for a violation mid-broadcast, that needs to read clearly differently from a normal "End" (show the reason given).

### Flow F — Donation Handling

**Pending Acknowledgment**
- Purpose: confirm that a tip sent to a performer at this venue has actually arrived via the payment provider.
- Content: list — performer, show, amount, a countdown to a 24-hour deadline.
- Actions: "Acknowledge Receipt."
- States: if the owner doesn't act within 24 hours, the system auto-confirms on their behalf — the history should visibly distinguish "you confirmed" from "auto-confirmed after timeout," so an owner isn't confused later about what they did or didn't do.

**Awaiting Payout**
- Purpose: record that the owner has manually wired a performer's share after receiving it.
- Content: list — same shape as above, filtered to the next stage.
- Actions: "Mark as Paid" (attach a payment reference and a photo of the transfer confirmation).
- States: this fails if the performer has no default bank account registered — better to detect that and disable the action with a link straight to setting one up, rather than let the owner submit and hit an error.

### Flow G — Finance

**Earnings Overview**
- Purpose: total income across every venue the owner runs.
- Content: summary combining settlements, ticket payments, and donations.
- Actions: none (read-only), with a link out to Bank Accounts if something looks blocked.
- States: money held up because a bank account isn't verified should show as "on hold, action needed," never silently absent — an owner should never have to wonder "did I lose this money."

**Venue Analytics**
- Purpose: detailed stats for one specific venue (distinct from the cross-venue Earnings Overview).
- Content: venue-scoped performance metrics; a venue picker if the owner runs more than one.
- States: empty state for a brand-new venue with no shows yet.

### Flow H — Penalties & Appeals

**My Penalties & Appeals**
- Purpose: view penalty history and file an appeal.
- Content: list of penalties (warning/suspension/ban) with status.
- Actions: "Appeal" (only on an `Active` penalty; a written reason).
- States: an overturned penalty doesn't automatically lift other overlapping active penalties — show the venue's real combined restriction state, not just "this one penalty" in isolation. Distinguish an admin-reviewed overturn from an automatic one (the system auto-overturns if admin review misses its SLA) — different enough that an owner might reasonably ask why in one case but not the other.

### Flow I — Performer Catalog

**Performer Profiles**
- Purpose: create, find, and edit performer profiles in the platform-wide shared catalog.
- Content: searchable list — avatar, name, bio snippet, genre tags.
- Actions: "Add Performer" (name, avatar, bio, type, genres); "Edit"/"Delete" — but **only visible at all** for profiles this owner created, or for admins (hide the buttons entirely for everyone else's profiles, don't show-then-reject); social media links (upsert per platform); a "Delete" that's disabled with a tooltip if the performer has ever been booked into any show, even a past one.
- States: empty search results should suggest "Create a new profile for '{search term}'" right there.

---

## 5. Cross-screen behaviors worth designing consistently

- **Financial confirmations matter**: several actions here move real money or make large refund commitments (change show format, cancel a show, mark a donation payout as paid) — these deserve a genuinely deliberate confirmation step, not a throwaway "Are you sure?" modal. This is an open design question tracked in [View-Design-Spec.md §5.3 item 10](View-Design-Spec.md#53-quyết-định-ux-cần-đội-thiết-kế-xác-nhận-be-không-quyết-định-được-đây-là-lựa-chọn-sản-phẩm) — pick a level of friction and apply it consistently across all of them.
- **Blocking prerequisites should be visible before the moment they bite**: bank account verification, subscription status, venue approval, legal declarations — all of these silently block a later action if incomplete. Wherever possible, surface the blocker at the point the owner would naturally discover the *cause*, not just at the point where the *action* fails.
- **This is a multi-venue tool from day one for some owners**: don't design single-venue assumptions into the navigation — the venue switcher should feel natural even for an owner who currently only has one.

---

*Source: [View-Design-Spec.md](View-Design-Spec.md) §2 (Owner-relevant views across groups J, K, L, M, N, O, P, Q, R, AA, plus the shared account-management views from A/B/I) and §3.2 (Owner flow diagrams). See [platform-architecture.md](platform-architecture.md) for the web/mobile split rationale — walk-in ticket sales and F&B counter management are deliberately excluded here even though an Owner is technically permitted to use them; that job belongs to [stitch-brief-staff-mobile.md](stitch-brief-staff-mobile.md) instead, which an Owner can log into directly for the same effect. Open UX decisions this brief already made a default call on are tracked in [View-Design-Spec.md §5.3](View-Design-Spec.md#53-quyết-định-ux-cần-đội-thiết-kế-xác-nhận-be-không-quyết-định-được-đây-là-lựa-chọn-sản-phẩm) — confirm before treating this brief as final.*
