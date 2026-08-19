# Stitch Design Brief — MusicLounge, Admin Console

> Distilled for Google Stitch from [View-Design-Spec.md](View-Design-Spec.md) §2 (view catalog) and §3 (Admin flow diagrams), scoped to the **Admin web surface** — see [platform-architecture.md](platform-architecture.md) for why this is a desktop back-office console, separate from every other surface.
> Rewritten for a design tool, not a dev handoff: no raw field/endpoint names, no BE-only validation trivia — only what actually shapes a screen.

---

## 1. App overview

**MusicLounge Admin** is the internal back-office console for the platform's own operators: approve new venues, moderate show/livestream content, handle venue penalties and appeals, process refunds, verify bank accounts, resolve customer complaints, manage user accounts, and configure platform-wide settings. There's no signup here — every admin account is created out-of-band by direct database access as a deliberate security posture, so this app's journeys all start from an already-provisioned login. This is queue-driven work: most screens are "here's a list of things waiting for a decision," not a browsing experience.

**Platform**: Website, desktop-first, no exceptions — dense data tables, multi-field review forms, cross-referencing several data sources at once. Never design a mobile-optimized version of this; that's not how this job gets done.

## 2. Suggested visual direction *(a starting point — adjust freely)*

A no-nonsense operations console — think airline ops center or a trust-and-safety review queue, not a consumer product. Favor information density and fast scanning over visual flourish: clear status pills, tabular data with real alignment, semantic color reserved strictly for state (pending/approved/rejected/flagged) rather than as a branding accent. A restrained neutral palette with one alert color for anything needing attention serves this better than the warm Audience-site identity — this is the one surface where diverging furthest from the consumer brand is the right call, since its audience is internal staff, not customers.

## 3. Top-level navigation

Sidebar navigation grouped by function: **Approvals** (venues, show/livestream moderation), **Penalties & Appeals**, **Finance** (refunds, donation reversals, bank verification, ledger integrity), **Complaints**, **Users**, **Platform Settings** (taxonomy, subscription plans), **Monitoring** (platform stats, background jobs), plus a notification bell that's especially important here — several queues have no natural entry point except a notification (see §5).

---

## 4. Screens by flow

### Flow A — Venue Approval

**Pending Venues**
- Purpose: review and approve/reject newly registered venues before they can operate.
- Content: queue of venues awaiting review with their submitted details.
- Actions: "Approve" or "Reject" (with a reason).
- States: empty state "No venues waiting." Every item in this queue is actionable immediately — no conditional logic gating entries here.

### Flow B — Content Moderation

**Moderation Queue**
- Purpose: review show and livestream submissions before they go public/live, informed by an AI-generated risk score as a suggestion (not a decision — the admin has full discretion to go against it).
- Content: tabbed or filterable queue (shows vs. livestreams), each item showing its risk score and submitted details.
- Actions: "Approve" or "Reject" — a written note is **required** when rejecting, optional when approving.
- States: separate empty state per tab. Approving a show makes it public to Audience immediately; approving a livestream unlocks the "Start Broadcasting" button on the Owner/Staff side.

### Flow C — Penalties & Appeals

**Issue Penalty**
- Purpose: record a penalty decision against a venue.
- Content: form — target venue, penalty level (Warning / Suspension / Ban), reason, evidence reference, and a suspension-length field that only appears when the level is Suspension.
- Actions: "Issue Penalty."

**Review Appeal**
- Purpose: uphold or overturn a venue's appeal against a penalty.
- Content: the penalty and the owner's written appeal reason.
- Actions: "Overturn" or "Uphold," with a review note.
- States: **known gap** — there's no list view of every currently-open appeal anywhere in the system; an admin only reaches this screen already knowing which penalty's id they're reviewing (typically via a notification). Don't design an "all open appeals" list screen assuming the data exists to power it — either accept notifications as the sole entry point, or flag this as a backend gap to close first (tracked in [View-Design-Spec.md §5.2](View-Design-Spec.md#52-endpoint-còn-thiếu-đã-xác-nhận-qua-đọc-code-không-phải-suy-đoán)). An overturned penalty doesn't automatically clear other overlapping active penalties on the same venue, and doesn't automatically reverse any financial side-effect (like a shortened subscription) — that still needs manual follow-up outside this screen today.

### Flow D — Finance Operations

**Refund Requests**
- Purpose: approve or reject ticket refund requests, which arrive from three different origins (a buyer's self-cancellation, an owner cancelling a whole show, or an admin resolving a complaint).
- Content: queue with the request's origin, amount requested, and context.
- Actions: "Reject"; "Approve" (full or partial amount — defaults to the full requested amount if left blank); a manual "Create Refund Request" escape hatch for edge cases that didn't generate one automatically, which then still flows through this same approval queue.
- States: make clear in the UI that approving here reverses internal ledger entries — it does **not** call a real payment-provider refund API in this environment (a sandbox/capstone limitation, not a bug) — don't phrase the confirmation as if money is guaranteed to land back in the buyer's bank account through this action alone.

**Reverse a Donation**
- Purpose: reverse a specific donation's ledger entries.
- Content: none pre-loaded — the admin needs to already know which donation's id they're reversing (typically arriving here from the Complaints flow, when a complaint's resolution involves a donation rather than a ticket).
- Actions: enter the donation id, provide a reason, "Reverse."
- States: only valid before the performer has actually been paid out — once that's happened, this should refuse with a clear explanation rather than silently failing. **No queue/list exists for this** — it's a standalone action screen, consider a prominent id-search field at the top since there's no other natural way in.

**Verify Bank Account**
- Purpose: manually confirm a bank account an owner registered, after reconciling it outside the system (there's no real bank API integration).
- Content: none pre-loaded, same gap as above — **this is the single screen in the whole admin console with no natural entry point at all**: no list of pending-verification accounts exists anywhere in the backend today. Flag this prominently as a backend gap worth closing (tracked in [View-Design-Spec.md §5.2](View-Design-Spec.md#52-endpoint-còn-thiếu-đã-xác-nhận-qua-đọc-code-không-phải-suy-đoán)) before investing much design effort into this screen's navigation — it may need a workflow change (e.g. verification requests arriving by a different channel entirely) more than a UI fix.
- Actions: enter the account id, "Verify."
- States: verifying immediately unblocks any settlement that was stuck waiting on it, on the owner's side — no confirmation UI needed here beyond a simple success toast.

**Ledger Integrity Check**
- Purpose: an internal auditing tool that scans for any accounting entries that don't balance.
- Content: a list of any discrepancies found, if any.
- States: **an empty result is the good outcome** — design it distinctly from a loading or error state, since "nothing found" reads ambiguously otherwise. This is a read-only diagnostic; any real fix happens outside this screen (direct database intervention), so don't design remediation actions here.

### Flow E — Complaints

**Resolve Complaints**
- Purpose: review and resolve every complaint filed on the platform, including from people with no account at all.
- Content: queue with target (ticket/show/venue/donation/penalty), category, description, evidence.
- Actions: "Resolve" — status, a resolution note, a resolved-action type, and (only relevant for Refund/Compensate) an amount.
- States: the automation behind a resolution varies by what's being resolved and what action is chosen — refund/compensate against a specific ticket auto-creates a refund request; against a donation, nothing automatic happens and the admin needs to separately use Reverse a Donation; take-down-content against a show cancels it and refunds every confirmed ticket; anything else is just a recorded decision with no further automation. Make the "what happens next" implication of each combination legible in the UI rather than uniform. A guest complainant (no account) shows their contact phone prominently in place of a name — they'll be notified by text message once resolved, not through any in-app channel.

### Flow F — User Management

**Manage Users**
- Purpose: search, inspect, and lock/unlock any user account.
- Content: searchable/filterable user list (by name/email, role, active status); a detail view per user including any submitted ID-verification photos.
- Actions: "Deactivate" or "Reactivate" — show exactly one of the two at a time, based on current status, never both.
- States: ID photos load through a protected endpoint, not a guessable public URL — treat them as sensitive throughout.

### Flow G — Platform Settings

**Taxonomy Management**
- Purpose: manage the platform-wide category/genre/mood/atmosphere tags used everywhere else in the system (show creation, recommendation preferences, etc.).
- Content: four simple lists, one per taxonomy type.
- Actions: "Add New" per type.
- States: **there is currently no edit or delete capability at all** — only creation. Don't design edit/delete controls for this screen; a typo today has no fix path except living with it or a direct database edit. If this is worth fixing, it's a backend gap to raise first (tracked in [View-Design-Spec.md §5.2](View-Design-Spec.md#52-endpoint-còn-thiếu-đã-xác-nhận-qua-đọc-code-không-phải-suy-đoán)), not something to route around in the UI. Before any taxonomy exists, every other screen across the whole platform that depends on it (show creation's genre picker, recommendation preferences) shows empty — worth a first-run banner here for a brand-new admin.

**Subscription Plan Management**
- Purpose: create and edit the subscription plans owners can buy.
- Content: list of plans, including inactive ones (unlike the public-facing plan comparison, which hides inactive plans).
- Actions: "Create Plan" / "Edit Plan" (including an active/inactive toggle).
- States: editing a live plan's terms does **not** retroactively change anything for owners who already subscribed under the old terms — make this explicit in the edit UI so an admin doesn't assume otherwise. Deactivating a plan blocks new subscriptions to it but doesn't cancel anyone's existing active subscription.

### Flow H — Livestream Enforcement

**Force-Stop a Livestream**
- Purpose: shut down a livestream that's actively broadcasting a violation.
- Type: this action lives on the live viewing screen itself (an admin can always open any livestream to watch it, same as an Audience viewer would, plus this one extra control) rather than as a separate screen — see [stitch-brief-audience-web.md](stitch-brief-audience-web.md) Flow E for the base viewing experience; add a "Force Stop" control visible only to admins, requiring a reason.
- States: every viewer gets disconnected the instant this fires — no confirmation delay expected on their end.

### Flow I — Platform Monitoring

**Platform Statistics**
- Purpose: system-wide analytics (distinct from a single venue's own analytics on the Owner side — a completely separate screen, not a permissions toggle on the same one).
- Content: platform-wide metrics dashboard.

**Background Jobs**
- Purpose: manually trigger a background maintenance job on demand (for troubleshooting), from a fixed known list of job names — there's no dynamic job registry to browse.
- Actions: select a job, "Run Now."
- States: this triggers one immediate run only — make clear it does **not** change that job's regular schedule, to avoid it being mistaken for a cron-configuration screen. No synchronous result comes back — show "run requested" feedback, then let the job's actual effect show up wherever that job's output naturally lives (e.g. running the ledger job means checking back on Ledger Integrity Check afterward).

---

## 5. Cross-screen behaviors worth designing consistently

- **Notifications are the primary entry point for several queues**: unlike a typical dashboard where every workflow starts from a list screen, a few of these (appeal review, bank verification, donation reversal) currently have **no list view at all** — the admin reaches them via a notification's deep link or by already knowing an id. Design the notification panel with this in mind: it's not just an inbox here, it's load-bearing navigation for real workflows.
- **Security alerts land in the same inbox as everything else**: background jobs watching for account-drift, login spikes, push-delivery failures, and payment-reconciliation mismatches all notify admins through the same Notifications channel Owners and Audience use — there's no separate "security alerts" surface. Make sure severity is visually distinguishable within one shared list.
- **"No real payment-provider integration" is a recurring caveat, not a one-off**: refund approval and the ledger tools all operate on internal accounting only in this environment — repeat the "internal reconciliation, not a live bank transfer" framing consistently rather than only on one screen, so it isn't easy to miss.

---

*Source: [View-Design-Spec.md](View-Design-Spec.md) §2 (Admin-relevant views across groups T, U, V, W, X, Y, Z, plus the Admin-specific edit/delete rights folded into the Owner-run Performer Profiles screen, the shared Livestream viewing screen, and the shared account-management views from A/B/I) and §3.4 (Admin flow diagrams). See [platform-architecture.md](platform-architecture.md) for the platform-split rationale. Several screens above explicitly flag missing list/edit endpoints as backend gaps rather than silently designing around them — cross-check [View-Design-Spec.md §5.2](View-Design-Spec.md#52-endpoint-còn-thiếu-đã-xác-nhận-qua-đọc-code-không-phải-suy-đoán) before starting on those specific screens.*
