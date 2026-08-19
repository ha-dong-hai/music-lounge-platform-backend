# MusicLounge — Stitch Master Design Brief (All Surfaces)

> **1 file, 5 self-contained design briefs** — one per client surface/app, per [platform-architecture.md](platform-architecture.md). Each is written for Google Stitch, not for dev/BA (no raw field/endpoint names, no BE-only validation trivia). **Stitch works as 1 project = 1 app** — feed it **one surface section at a time** (copy that section's content into a new Stitch project), not the whole file at once; the sections describe 5 different products, not one. Source of truth for everything here: [View-Design-Spec.md](View-Design-Spec.md).
> **Cập nhật**: 2026-08-14.

## Mục lục

1. [Audience — Website](#surface-1-audience--website) (30 view)
2. [Audience — Mobile (F&B Ordering)](#surface-2-audience--mobile-fb-ordering) (2 view)
3. [Owner — Web Dashboard](#surface-3-owner--web-dashboard) (36 view)
4. [Staff — Mobile](#surface-4-staff--mobile) (11 view)
5. [Admin — Web Console](#surface-5-admin--web-console) (21 view)

Mỗi phần độc lập hoàn toàn — không cần đọc phần khác để hiểu 1 surface. Xem [platform-architecture.md](platform-architecture.md) cho lý do vì sao chia 5 surface này thay vì gộp theo actor suông.

---

# Design Execution Principles (applies to every surface, added 2026-08-17)

> Sourced from real research on luxury/premium digital product design (Nielsen Norman Group whitespace findings, luxury e-commerce case studies — Hermès/Chanel/The Row, 2026 glassmorphism best-practice writeups), not assumption. User confirmed 2026-08-17 this applies to **all 5 surfaces including Admin** — overriding Surface 5 §2's original "diverge from the warm identity" note below; keep reading that note as historical context for *why* density matters there, not as license to drop the design system.

1. **Whitespace is the loudest luxury signal, not empty space to fill.** NNG research ties generous whitespace directly to comprehension *and* perceived value — there's a real, measured correlation between how much breathing room a layout has and how expensive it reads. Every prompt should explicitly push for *more* padding/margin than a default AI-generated layout tends to produce; err toward a screen feeling slightly sparse over feeling efficient.

2. **One accent color per screen, used sparingly.** Primary (terracotta) should mark exactly the one most important next action — not be sprinkled across every badge, icon, and border on a screen. Competing accents read as busy/generic, the opposite of luxury. Semantic colors (error, tertiary) are not "extra accents" — they're reserved strictly for state, same as `DESIGN.md` already specifies.

3. **Restraint over density, even in list/queue/dashboard screens.** Surface only the 3–5 fields that actually matter in a scan; push everything else behind a detail view or expand/collapse rather than a wide table with every column visible at once. A queue can still feel curated, not spreadsheet-like.

4. **Glassmorphism: at most 2–3 surfaces per screen, and only where there's something visually rich behind it.** `backdrop-blur` over the flat warm-beige base surface renders as nothing — glass needs a photo, gradient, or busy scroll area behind it to actually read as glass. Reserve it for chrome (sticky nav, floating action bars, modals/overlays) — never put paragraph copy directly on a glass panel, since a moving/blurred background can't guarantee the contrast ratio body text needs.

5. **Photography carries more weight than iconography.** A single well-art-directed image reads more premium than a grid of generic icons (already how the Sign Up Polaroids and venue photos work) — where a screen has no real photo to show (e.g. Admin's Pending Venues queue has no image field), keep icon use strictly functional/minimal rather than reaching for decorative illustration to fill the gap.

6. **Typography is one coherent system, not per-screen improvisation.** Playfair Display stays reserved for headlines/titles/proper nouns, Inter for everything functional — same pairing on every surface, no third font, no ad hoc weight/size changes "for emphasis." Emphasis comes from spacing and color restraint, not font gymnastics.

7. **Motion is a small number of well-chosen moments, via [anime.js](https://animejs.com/) (`animejs` on npm, already installed in `fontend/Fontend_Final`), not decoration on every element.** User's explicit request 2026-08-17. Good candidates: a staggered entrance for list/queue rows, a state-change confirmation (e.g. an Approve/Reject action), an inline expand/collapse (like a reject-reason field revealing). Same restraint rule as color/glass above — a handful of purposeful, physics-feeling transitions reads as quiet confidence; animating everything reads as a demo reel, not a premium product. Reach for anime.js over a raw CSS `transition`/`@keyframes` specifically when the motion needs sequencing, stagger, or spring-like easing that CSS alone does the wrong way (e.g. a *bouncy or elastic* JS-space animation feel) — plain hover/focus color or shadow changes stay in Tailwind's own transition utilities as before, no need to route every micro-interaction through JS.

**Admin surface reconciliation**: Surface 5 keeps the *same* typography/spacing/shadow/radius tokens as every other surface (still Warm Luxury Lounge, not a separate "ops console" palette) — restraint and scannability come from neutral surface tones dominating each screen (surface-container-low/high, not primary/secondary color washes), primary reserved strictly for the one primary action per row, and editorial-style list rows (per `DESIGN.md`'s own "Lists: Clean, high-contrast rows with subtle dividers") instead of cramped tables — not from abandoning the design system.

8. **Actively avoid the specific, named tells of "AI slop" design, added 2026-08-17.** User flagged the shipped screens as reading "too AI-generated" despite following principles 1–7 — real 2026 design-industry research on why AI output converges on a generic look (the root cause is named "distributional convergence": a model reverts to the statistical average of its training data unless given something the average can't contain) points at specific, fixable tells:
   - **Icon set**: Material Symbols Outlined is *the* default in Stitch (and every other AI design tool) — instantly recognizable as machine-generated regardless of color/spacing polish. **Switched to [Phosphor Icons](https://github.com/phosphor-icons/react) (`@phosphor-icons/react`, thin/light weight)** across every screen 2026-08-17 — a deliberate, distinctive choice, not another popular default.
   - **Uniform border-radius on every single element** is itself a tell (everything rounded exactly 12px reads as templated). `DESIGN.md` already defines a 2-tier scale (`rounded-md` 12px standard, `rounded-xl` 24px featured) — enforce actually *using* both tiers with intent, not defaulting every element to the same one.
   - **Every content block wrapped in an identical white-rounded-card-with-shadow** reads as "AI dashboard template" the more it repeats unchanged. Vary the container treatment across a screen — not every section needs the same card chrome.
   - **AI-generated placeholder photography** (Stitch's own image-gen for the Polaroids/venue photos) has a "too smooth, too symmetric" quality under close inspection. Partial mitigation already in place and worth extending everywhere: the existing `sepia-[.2] contrast-125`-style filters on Polaroid images and the SVG noise-texture body background (`OwnerMyVenues` mobile export) both break up that plastic sheen — apply this kind of grain/tone treatment consistently, not ad hoc per screen. **Not fully solved**: the actual fix is real photography, which doesn't exist yet for this project (no real venues photographed) — flag this honestly rather than pretending a CSS filter fully closes the gap.
   - **A recurring signature motif specific to this brand** (not generic icons) helps signal "designed for MusicLounge," not "generated for any app" — worth developing (a sound-wave line, a groove/vinyl mark, something tied to live music) rather than leaning on Phosphor icons alone to carry visual interest.

---

# Surface 1: Audience — Website


> Distilled for Google Stitch from [View-Design-Spec.md](View-Design-Spec.md) §2 (view catalog) and §3 (Audience flow diagrams), scoped to the **web surface only** — see [platform-architecture.md](platform-architecture.md) for why Audience is web-first while a separate native app exists purely for venue F&B ordering ([stitch-brief-audience-mobile-fnb.md](stitch-brief-audience-mobile-fnb.md)).
> Rewritten for a design tool, not a dev handoff: no raw field/endpoint names, no BE-only validation trivia — only what actually shapes a screen.

---

### 1. App overview

**MusicLounge** is a website for discovering and attending live-music shows at small venues ("lounges") in Vietnam — buy tickets, watch livestreams of shows you can't attend in person, tip ("donate") performers in real time while watching, and manage your account. This is the primary consumer surface: no business tools, just browse → buy → attend/watch → engage.

**Platform**: Responsive website — desktop is the primary design target (most browsing/purchase decisions happen there), but every screen must also work cleanly in a phone's browser, since a ticket's QR code needs to be pulled up at the venue door without requiring an app install. Food/drink ordering while physically at a show is deliberately **out of scope here** — that's a separate, minimal native app (see linked brief above); don't design an in-venue ordering flow into this site.

### 2. Suggested visual direction *(a starting point — adjust freely, this isn't a fixed brand)*

Live-music, nightlife, intimate-venue energy — think small jazz/acoustic lounges, not stadium concerts. Suggest: dark-mode-friendly base (venues are evening/night experiences), one warm accent color (amber/coral range reads as "stage light" without being a cliché neon-club look), generous imagery (venue photos, performer avatars, show posters) since this is a highly visual, discovery-driven site. Avoid generic SaaS-dashboard styling — this should feel like a consumer entertainment site (closer to a ticketing/streaming service than a business tool).

### 3. Top-level navigation

Standard website header, not a mobile tab bar: logo, primary nav links (**Discover**, **My Tickets**, **Following**), a search bar, and an account menu (avatar dropdown → Notifications, My Donations, My F&B history — read-only here, see below —, My Complaints, Profile, Privacy & Data). Keep the header sticky/persistent since navigation happens across long browsing sessions.

---

### 4. Screens by flow

#### Flow A — Onboarding

**Sign Up**
- Purpose: create a new account.
- Content: email, password, full name, phone (optional).
- Actions: "Sign Up" button → moves to email verification. Link to "Log In" for existing users.
- States: field-level validation errors (not a generic banner); loading on submit.
- Note: after signup the account is *not yet logged in* — it goes straight to email verification, no "welcome, you're in" moment yet.

**Verify Email**
- Purpose: enter the OTP code just emailed to activate the account.
- Content: masked/partial email shown ("code sent to j***@gmail.com"), 6-digit code input.
- Actions: "Verify" button; "Resend code" link.
- States: wrong/expired code error; loading.

**Log In**
- Purpose: authenticate.
- Content: email + password fields, "Log in with Google" button.
- Actions: "Log In"; "Forgot password?" link; "Sign up" link.
- States: **deliberately vague error** for wrong email vs wrong password — one generic "Incorrect email or password" message, never reveal which one was wrong (this is intentional anti-enumeration, keep it that way).

**Forgot / Reset Password**
- Purpose: recover account access.
- Content: step 1 — email input only. Step 2 (opened from an emailed link, its own URL) — new password field.
- Actions: step 1 "Send reset link" (always shows a generic "check your email" confirmation, even if the email isn't registered — never say "email not found"); step 2 "Reset Password".

#### Flow B — Discover

**Home / Show Search**
- Purpose: browse and search live shows.
- Content: search bar, filter sidebar or chips (genre, mood, date, price, format), trending section, show cards (poster, venue name, date, price range) in a grid.
- Actions: click a show card → Show Detail; apply filters; type-ahead search suggestions.
- States: empty state for "no results" (distinct from a loading skeleton).

**Show Detail**
- Purpose: everything about one show before deciding to buy a ticket, watch live, or rate it.
- Content: poster/cover image, show name, date/time, venue name + link, format badge (in-person / livestream — a show is strictly one or the other, never both), performer lineup (avatar, name, bio snippet — click to see performer's public page), description, ticket price range.
- Actions (all *conditional* — this is the most important screen for conditional logic):
  - **"Buy Tickets"** — only if the show is open for sale and seats remain.
  - **"Watch Live"** — only if the show is currently live AND the user holds a ticket for it (if the button is visible but the user isn't a genuine ticket holder, they should see a clear "you need a ticket" message, not a silent failure).
  - **"Rate This Show"** — only if the show has ended, it's within the review window (about a week after), the user attended, and they haven't rated it yet.
  - **Follow venue** / **Add to wishlist** — secondary icon buttons, always available.
- States: cancelled show → hide the buy button, show a "This show was cancelled" banner instead of pretending it's still on.

**Venue Detail**
- Purpose: info about one venue.
- Content: photos, name, address/map, description, atmosphere tags, upcoming shows at this venue, seating zone overview.
- Actions: "Follow" toggle; "View 360° Tour" (only if the venue has one — hide the link entirely if not, don't show it disabled); click an upcoming show → Show Detail.

**360° Virtual Tour**
- Purpose: an immersive, drag-to-look-around panorama tour of the venue before buying tickets — think a museum virtual-tour experience.
- Type: interactive/immersive, not a standard page — full-bleed panorama viewer with clickable hotspots that jump between rooms/angles or show an info popup. Works with mouse-drag on desktop and touch-drag on a phone browser.
- States: empty state if the venue hasn't set up a tour yet.

**For You (Recommendations)**
- Purpose: personalized show suggestions.
- Content: a curated feed of show cards, similar to Home but personalized.
- States: if the user hasn't enabled personalization or is too new, this quietly becomes "Trending" instead — the screen should look intentional either way, not broken.

#### Flow C — Following

**Following / Wishlist**
- Purpose: manage venues followed and shows wishlisted.
- Content: two tabs — "Venues" and "Shows".
- Actions: unfollow / remove from wishlist.
- States: separate empty state per tab.

#### Flow D — Buying a Ticket

**Select Tickets**
- Purpose: pick a ticket tier + quantity and hold it before paying.
- Content: list of ticket tiers with price and remaining quantity, seating map (if the venue has assigned seating).
- Actions: "Hold" (starts a 15-minute countdown timer — **this timer must be visible and prominent**, it's a real deadline, not decorative); once held: "Continue to Payment" or "Release Hold".
- States: sold-out tiers shown disabled with the reason, not hidden; a running-out-of-time state as the 15 minutes wind down.

**Payment Result**
- Purpose: shown after returning from the external payment provider, before we know the final outcome for certain.
- Content: a status view — **needs 3 distinct states, not 2**: "Processing" (we're still confirming with the payment provider), "Success", "Failed". Don't design this as a simple success/fail binary; the in-between state is real and can last a few seconds.
- Actions: on success → "View My Ticket"; on failure → "Try Again".
- Note: this same screen template is reused for ticket purchases and donations here, and (on the separate Owner site) subscriptions — keep it generic/parameterizable rather than hard-coding "ticket" language everywhere.

**My Tickets**
- Purpose: full ticket history.
- Content: list of tickets across all statuses (upcoming, used, cancelled, refunded) — separate "upcoming" from "past" visually.
- Actions: click a ticket → Ticket Detail.
- States: empty state "You haven't bought any tickets yet."

**Ticket Detail**
- Purpose: the actual ticket — QR code for entry, plus transfer/cancel management.
- Content: QR code (large, front and center — **this is what gets scanned at the door on a phone browser**, so it must render clearly at small sizes too), show info, seat/tier info.
- Actions (all conditional):
  - **"Transfer Ticket"** — only if the ticket is confirmed, not checked in, never watched via livestream yet, and the show hasn't ended/been cancelled.
  - **"Cancel Ticket"** — for an unpaid/pending ticket, always available; for a confirmed ticket, only within the cancellation deadline and if the show allows cancellation.
- States: once used/cancelled/refunded, hide the QR code entirely and show a clear status label in its place instead (a dead QR code sitting on screen is confusing).

**My Refund Requests**
- Purpose: track refund status after cancelling a ticket.
- Content: list with status (pending / approved / rejected).
- States: empty state.

#### Flow E — Watching Live & Donating

**Live Viewing Room**
- Purpose: watch the livestream, chat, and donate — the most feature-dense screen on the site.
- Content: video player (large, desktop-primary layout — video and chat side-by-side rather than stacked, since desktop is the primary target here), performer lineup for tipping alongside.
- Actions: send chat message; click a performer → open donate flow (only shown for performers who have donations enabled for this set).
- States: if the connection is rejected (not a real ticket holder, or too many devices already watching on this ticket), show a clear explanation, not a blank/frozen player — and distinguish that from a plain network hiccup, which should just retry quietly.

**Public Donation Ticker** *(a widget embedded in Show Detail and the Live Viewing Room — not its own page)*
- Purpose: a live, Twitch/Streamlabs-style alert feed of donations for this show, visible to everyone watching — **including people with no ticket and no account**. This does not require livestream access; it's a separate, fully public real-time feed.
- Content: rolling list of alerts — donor name (or "Anonymous"), amount (may be hidden per-donor), message (may be hidden per-donor).
- States: nothing to show if there have been no donations yet — no empty-state box needed, it should just be absent.

**Donate**
- Purpose: send a tip to a specific performer while watching.
- Type: modal/overlay on top of the Live Viewing Room (keep the video visible/audible if possible — don't fully navigate away from the show).
- Content: performer name/avatar, amount input, optional message, privacy toggles ("Donate anonymously", "Hide amount", "Make message public").
- Actions: "Donate" → goes to payment → Payment Result.

**My Donations**
- Purpose: track tips sent, including their processing status.
- Content: list with a 3-step progress indicator (Sent → Venue confirmed receipt → Performer paid) — this can take a while and is largely out of the user's hands, the UI should communicate "still moving through the pipeline," not look stuck.
- States: empty state "You haven't donated to anyone yet."

**Public Donation Transparency Feed**
- Purpose: a public, browsable ledger of donations — for accountability, not just excitement (unlike the live ticker above).
- Two variants sharing this same purpose: (1) platform-wide feed with a full fee breakdown, (2) one performer's donation history, simpler, amount-only. Decide whether these are one flexible component or two separate layouts.
- Content: paginated list — donor (or "Anonymous"), performer, show, amount (+ breakdown in variant 1), date.
- Note: this intentionally only shows donations that are "settled," not ones still pending confirmation — a donation can appear in the live ticker before it appears here. Don't treat that as a bug to fix visually; if anything, a small "processing" note near very recent items in the ticker helps set expectations.

**Performer Public Page**
- Purpose: a performer's public profile.
- Content: avatar, bio, genre tags, list of their upcoming/past shows, link to their donation history (transparency feed, variant 2).

#### Flow F — Rating

**Rate Show**
- Type: modal on top of Show Detail.
- Content: star rating + comment text field, show name/poster for context.
- Actions: "Submit Rating".
- Note: only ever reachable once per show — after submitting, the "Rate" button on Show Detail disappears rather than the form showing an error on a second attempt.

#### Flow G — Complaints

**File a Complaint**
- Purpose: report a problem (bad ticket experience, unresolved donation, venue conduct, etc.) — reachable even by someone not logged in.
- Content: category picker, target (which show/ticket/venue — often pre-filled if opened from that screen's context), description, optional evidence/photos, phone number (required if not logged in).
- Actions: "Submit".
- States: on submit while logged out, **the confirmation screen must clearly display and let the user save the complaint's reference number** — it's their only way to check on it later (guest lookup uses this number + their phone).

**My Complaints**
- Purpose: track filed complaints (logged-in users only).
- Content: list with status; click for full detail including admin's resolution notes.
- States: empty state.

#### Flow H — Notifications & Profile

**Notifications**
- Purpose: central inbox for everything — ticket confirmations, donation updates, low-stock alerts on wishlisted shows, complaint/refund outcomes.
- Content: list (dropdown panel from the header, or a dedicated page), unread indicator, click-through to the relevant screen based on the notification type.
- States: empty state; unread count badge on the header icon.

**Profile**
- Purpose: hub for account settings.
- Content: name/avatar summary, links to: Edit Profile, ID Verification, AI Preferences, Privacy & Data, My Donations, My Complaints. *(F&B order history lives in the separate mobile app, not here.)*

**Edit Profile**
- Content: name, phone, avatar, date of birth; change-password section (hidden entirely if the account signed up via Google — show "Linked to Google" instead).

**ID Verification**
- Purpose: submit ID document photos for verification (optional, mainly relevant once a user needs payout-adjacent trust, but available to any Audience user).
- Content: front/back photo upload, submitted status.

**AI Preferences**
- Purpose: set music taste for personalized recommendations.
- Content: genre/mood/atmosphere tag selection, a toggle for "Enable personalized recommendations."
- Note: turning personalization off should visibly explain what changes (recommendations become generic "Trending" instead) — not a silent setting.

**Privacy & Data**
- Purpose: data-rights actions (export personal data, deactivate or permanently delete account).
- Content: two clearly separated actions — "Deactivate" (reversible) vs "Delete My Data" (irreversible).
- Actions: "Export My Data"; "Deactivate Account"; "Delete My Data" — the last one needs a strong two-step confirmation (re-enter password), since it immediately signs the user out everywhere and can't be undone.

---

### 5. Cross-screen behaviors worth designing consistently

- **Real-time nudges**: a few things update live without the user acting — the donation ticker, live chat, and a "hurry, low stock" push when a wishlisted show is close to selling out. Everything else (donation pipeline progress) is manual-refresh only. Don't design loading spinners that imply everything is realtime.
- **Guest vs logged-in parity**: most of Discover (Flow B), the donation transparency feed, and complaint filing work identically for a logged-out visitor. Don't gate the *browsing* experience — only gate the *write* actions (buy, donate, wishlist, rate), each with a clean "log in to continue" prompt that returns the user to what they were doing.
- **Countdown urgency**: the ticket hold timer (15 min) is the one place urgency/countdown UI really matters — treat it as a first-class design element, not a small corner label.
- **Phone-browser reality check**: even though desktop is the primary target, the Ticket Detail QR screen and the Log In screen in particular need to look good at phone width too — these are the two screens most likely opened on a phone (at the door, or checking email on the go).

---

*Source: [View-Design-Spec.md](View-Design-Spec.md) §2 (Audience-relevant views across groups A, B, C, D, E, F, H, I — excludes group G, F&B, which is a separate native app) and §3.1 (Audience flow diagrams). See [platform-architecture.md](platform-architecture.md) for the web/mobile split rationale. Open UX decisions this brief already made a default call on (modal vs page for Donate/Rate, shared Payment Result template, split vs merged donation feed variants) are tracked as open questions in [View-Design-Spec.md §5.3](View-Design-Spec.md#53-quyết-định-ux-cần-đội-thiết-kế-xác-nhận-be-không-quyết-định-được-đây-là-lựa-chọn-sản-phẩm) — confirm before treating this brief as final.*

---

# Surface 2: Audience — Mobile (F&B Ordering)


> Distilled for Google Stitch from [View-Design-Spec.md](View-Design-Spec.md) §2 group G. Deliberately narrow-scope native app — see [platform-architecture.md](platform-architecture.md) for why this is split off from the main Audience experience, which lives on the website ([stitch-brief-audience-web.md](stitch-brief-audience-web.md)).

---

### 1. App overview

A single-purpose native mobile app used **only** while physically at a MusicLounge venue, watching a show in person: order food and drinks from the venue's menu without flagging down staff, and track your order's status. This is not a general MusicLounge app — no browsing shows, no tickets, no livestream, no donations here. If a user opens this app anywhere but a venue during a show, there's simply nothing useful for them to do; design around that reality rather than padding the app with unrelated features.

**Platform**: Native mobile app (iOS/Android). Assume the phone is in the user's hand at their table — design for one-handed use, large tap targets, minimal typing.

### 2. Suggested visual direction *(a starting point — adjust freely)*

Should feel like a fast, low-friction utility, not a full entertainment app — closer to a restaurant QR-ordering experience than to the main MusicLounge brand. A lighter touch of the same "warm, intimate venue" palette from the main site keeps it recognizably MusicLounge, but the design should prioritize speed and clarity over atmosphere: this gets opened mid-conversation, in low light, order needs to go in fast.

### 3. Navigation

No tab bar needed for a 2-screen app — a simple flow: open app → Order screen → after ordering, a persistent way back to "My Order" status (e.g. a small floating status pill or badge that's always visible while an order is active).

---

### 4. Screens

**Order F&B**
- Purpose: browse the venue's menu and place an order.
- Content: menu grouped by category, item cards (photo, name, price), cart/order summary (likely a bottom sheet or persistent mini-cart bar).
- Actions: add to cart; adjust quantity; "Place Order".
- States: sold-out items shown greyed-out with a "temporarily unavailable" label, not removed from the menu; empty state if the venue hasn't set up a menu.
- Note: the app needs to know *which venue* (and ideally which table/zone) the order belongs to — likely established once at the start of the session (e.g. a lightweight venue/table selection or a QR scan step) rather than asked again per item. Exact mechanism is an implementation detail beyond this brief — just design the ordering screen assuming venue/table context is already known by this point.

**My Order**
- Purpose: track order status without having to ask staff.
- Content: current order with a status stepper (Pending → Preparing → Served → Paid, or Cancelled as a side-exit), items and total.
- States: **status does not push-update in real time** — staff update it from their own tool, and this screen has no live channel to them. Design a clear manual "pull to refresh" affordance rather than implying the screen watches status live on its own.

---

*Source: [View-Design-Spec.md](View-Design-Spec.md) §2 group G (Đặt món F&B, Đơn F&B của tôi). See [platform-architecture.md](platform-architecture.md) for the platform-split rationale.*

---

# Surface 3: Owner — Web Dashboard


> Distilled for Google Stitch from [View-Design-Spec.md](View-Design-Spec.md) §2 (view catalog) and §3 (Owner flow diagrams), scoped to the **Owner web surface** — see [platform-architecture.md](platform-architecture.md) for why Owner is a desktop-first business dashboard, separate from the Audience site and the Staff mobile tool.
> Rewritten for a design tool, not a dev handoff: no raw field/endpoint names, no BE-only validation trivia — only what actually shapes a screen.

---

### 1. App overview

**MusicLounge for Owners** is the business dashboard for people who run a live-music venue ("lounge") on MusicLounge — set up your venue, create shows, sell tickets, run livestreams, handle tips sent to your performers, and track your earnings. This is a B2B operator tool, not the consumer app: no browsing-for-fun here, every screen exists to help an owner run their business.

**Platform**: Website, desktop-primary. This involves form-heavy setup flows, a drag-and-drop seating editor, and financial dashboards — genuinely desk work, unlike the Audience site which also needs to work well on a phone. Still keep it reasonably responsive (an owner might check today's earnings from a phone), but don't compromise the desktop layout for it.

**Out of scope here** (separate surfaces, don't design into this app): walking around the venue to scan tickets or sell at the door (that's the Staff mobile tool — an Owner personally working the counter just logs into that same tool, no separate desktop version needed), and anything an Admin does to moderate/approve content platform-wide.

### 2. Suggested visual direction *(a starting point — adjust freely, this isn't a fixed brand)*

Should read as a serious, trustworthy business tool — this is where real money and legal paperwork flow through. Keep the same warm, live-music-adjacent identity as the Audience site (so it's recognizably "MusicLounge" and not a generic admin template), but dial back the atmosphere in favor of clarity: more neutral surface area, data given room to breathe, financial figures set with careful hierarchy (a settlement dashboard is not the place for cramped tables). Light mode as the default here reads more "business tool" than the Audience site's dark-mode-friendly suggestion — but this is a suggestion, not a rule.

### 3. Top-level navigation

Top horizontal navigation bar (not a sidebar — decided once real screens were already built with this pattern, 2026-08-17), sections: **Venues**, **Shows**, **Donations**, **Finance**, **Performers**, **Staff**, **Subscription**, **Penalties**, plus an account menu (Notifications, Profile, Bank Accounts, Privacy & Data) on the right side of the same bar. If the owner has multiple venues, a venue switcher near the logo scopes Shows/Finance/Staff to the selected venue.

---

### 4. Screens by flow

#### Flow A — Onboarding

**Sign Up / Log In / Password Recovery**
- Same account flows as the Audience site (register choosing the "Owner" role, email OTP verification, login, forgot/reset password) — see [stitch-brief-audience-web.md](stitch-brief-audience-web.md) Flow A for full detail, same content and rules apply, just skinned to this dashboard's visual direction instead.

**Bank Accounts**
- Purpose: register where venue earnings (and performer payouts, on their behalf) get paid out.
- Content: list of accounts (venue's own + one per performer the owner manages), each with a verification-status badge.
- Actions: "Add Bank Account" (bank name, account number, account holder, owner type: venue or a specific performer, set-as-default); "Edit".
- States: unverified accounts show a "Pending admin verification" badge — **this is a real blocker**, not decoration: settlements and donation payouts stay stuck until verified. Surface this clearly, since it's the #1 reason an owner would see "why hasn't my money arrived." Empty state should be treated as urgent — this step blocks almost everything else, so a brand-new owner should see a prominent nudge to complete it before doing anything else.

#### Flow B — Venue Setup

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

#### Flow C — Subscription

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

#### Flow D — Show Creation & Management

**My Shows**
- Purpose: overview of every show this owner has created, at any stage.
- Content: list with status (Draft / Pending review / Published / Ongoing / Ended / Cancelled).
- Actions: "Create New Show."
- States: `Draft` links back into editing; `Pending` is read-only while awaiting admin review. Empty state points at "Create New Show."

**Create / Edit Show**
- Purpose: core show details plus the performer lineup.
- Content: name, description, format (in-person / livestream — pick exactly one, never both; researched against Eventbrite/Luma/Zoom Events, none of which treat "hybrid" as a real sibling choice either), schedule, category/genre tags, ticket quota for whichever format was picked, and a lineup builder — add performers with their role/set-time/whether they can receive tips for this show.
- Actions: search-or-create for each lineup performer (typing a name that doesn't match anything in the catalog silently creates a new performer profile on submit — the UI should make this obvious, e.g. "No match — a new performer profile will be created for '{name}'" rather than a hidden side effect); "Save Draft" / "Continue."
- States: block submission (with a clear banner, not a late error) if the owner doesn't currently have an active subscription. No hard limit on the number of performers in a lineup — don't impose an artificial cap in the UI.

**Show Control Center**
- Purpose: the hub for one show's whole lifecycle, from draft to ended — most owners will spend the most time here.
- Content: show status, ticket sales summary (orders list), and a menu of lifecycle actions.
- Actions: "Submit for Review" (needs the venue approved and at least one ticket tier); "Reschedule" (only while `Published`, not yet ongoing); "Change Format" (in-person→online is one-way, **automatically refunds 100% of confirmed in-person tickets** — needs a strong confirmation, not a casual one-click); "Change Playback Mode" (2D/3D, only relevant for online shows); "Cancel Show" (refunds 100% of confirmed tickets, blocked while a livestream is actively live — must terminate that first); "Start"/"End" (in-person shows only — these buttons disappear entirely if the show has an attached livestream, which uses its own start/end pair instead, in the Livestream Operations screen).
- Actions (links out): to Ticket Tiers, Poster, Legal & Royalty Declaration, Livestream Operations (if online).
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
- States: if the royalty reference is missing on a show that's online, show a warning here — it will hard-block starting the livestream later, better to catch it at the source.

#### Flow E — Livestream Operations

**Livestream Operations**
- Purpose: set up streaming credentials, start/end the broadcast, and monitor viewers/chat while live.
- Content: RTMP URL + Stream Key (**never shown to the audience** — treat as sensitive, maybe behind a "reveal" click), live chat feed, viewer count.
- Actions: "Create Livestream" for a show; "Start Broadcasting" (blocked until an independent admin content review clears *this livestream specifically* — separate from the show's own review — **and** the legal royalty reference is filled in; show both conditions distinctly so the owner knows exactly what's still missing); "End Broadcasting."
- States: viewer count does not update live on this screen — it's a manual-refresh number, don't imply a live counter animation. If an admin forcibly terminates the stream for a violation mid-broadcast, that needs to read clearly differently from a normal "End" (show the reason given).

#### Flow F — Donation Handling

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

#### Flow G — Finance

**Earnings Overview**
- Purpose: total income across every venue the owner runs.
- Content: summary combining settlements, ticket payments, and donations.
- Actions: none (read-only), with a link out to Bank Accounts if something looks blocked.
- States: money held up because a bank account isn't verified should show as "on hold, action needed," never silently absent — an owner should never have to wonder "did I lose this money."

**Venue Analytics**
- Purpose: detailed stats for one specific venue (distinct from the cross-venue Earnings Overview).
- Content: venue-scoped performance metrics; a venue picker if the owner runs more than one.
- States: empty state for a brand-new venue with no shows yet.

#### Flow H — Penalties & Appeals

**My Penalties & Appeals**
- Purpose: view penalty history and file an appeal.
- Content: list of penalties (warning/suspension/ban) with status.
- Actions: "Appeal" (only on an `Active` penalty; a written reason).
- States: an overturned penalty doesn't automatically lift other overlapping active penalties — show the venue's real combined restriction state, not just "this one penalty" in isolation. Distinguish an admin-reviewed overturn from an automatic one (the system auto-overturns if admin review misses its SLA) — different enough that an owner might reasonably ask why in one case but not the other.

#### Flow I — Performer Catalog

**Performer Profiles**
- Purpose: create, find, and edit performer profiles in the platform-wide shared catalog.
- Content: searchable list — avatar, name, bio snippet, genre tags.
- Actions: "Add Performer" (name, avatar, bio, type, genres); "Edit"/"Delete" — but **only visible at all** for profiles this owner created, or for admins (hide the buttons entirely for everyone else's profiles, don't show-then-reject); social media links (upsert per platform); a "Delete" that's disabled with a tooltip if the performer has ever been booked into any show, even a past one.
- States: empty search results should suggest "Create a new profile for '{search term}'" right there.

---

### 5. Cross-screen behaviors worth designing consistently

- **Financial confirmations matter**: several actions here move real money or make large refund commitments (change show format, cancel a show, mark a donation payout as paid) — these deserve a genuinely deliberate confirmation step, not a throwaway "Are you sure?" modal. This is an open design question tracked in [View-Design-Spec.md §5.3 item 10](View-Design-Spec.md#53-quyết-định-ux-cần-đội-thiết-kế-xác-nhận-be-không-quyết-định-được-đây-là-lựa-chọn-sản-phẩm) — pick a level of friction and apply it consistently across all of them.
- **Blocking prerequisites should be visible before the moment they bite**: bank account verification, subscription status, venue approval, legal declarations — all of these silently block a later action if incomplete. Wherever possible, surface the blocker at the point the owner would naturally discover the *cause*, not just at the point where the *action* fails.
- **This is a multi-venue tool from day one for some owners**: don't design single-venue assumptions into the navigation — the venue switcher should feel natural even for an owner who currently only has one.

---

*Source: [View-Design-Spec.md](View-Design-Spec.md) §2 (Owner-relevant views across groups J, K, L, M, N, O, P, Q, R, AA, plus the shared account-management views from A/B/I) and §3.2 (Owner flow diagrams). See [platform-architecture.md](platform-architecture.md) for the web/mobile split rationale — walk-in ticket sales and F&B counter management are deliberately excluded here even though an Owner is technically permitted to use them; that job belongs to [stitch-brief-staff-mobile.md](stitch-brief-staff-mobile.md) instead, which an Owner can log into directly for the same effect. Open UX decisions this brief already made a default call on are tracked in [View-Design-Spec.md §5.3](View-Design-Spec.md#53-quyết-định-ux-cần-đội-thiết-kế-xác-nhận-be-không-quyết-định-được-đây-là-lựa-chọn-sản-phẩm) — confirm before treating this brief as final.*

---

# Surface 4: Staff — Mobile


> Distilled for Google Stitch from [View-Design-Spec.md](View-Design-Spec.md) §2 (view catalog) and §3 (Staff flow diagrams), scoped to the **Staff mobile surface** — see [platform-architecture.md](platform-architecture.md) for why this is a native operational tool, separate from the Audience site and the Owner web dashboard.
> Rewritten for a design tool, not a dev handoff: no raw field/endpoint names, no BE-only validation trivia — only what actually shapes a screen.

---

### 1. App overview

**MusicLounge Staff** is the on-the-floor operational tool for venue staff working a show: sell walk-in tickets at the counter, scan tickets at the door, and manage food/drink orders from the kitchen or bar side. A staff member is assigned to exactly one venue at a time by that venue's owner — there's no self-signup here. This app is used standing up, moving around a physical space, often in low light and with one hand occupied — every screen should assume that.

**Platform**: Native mobile app (or tablet), used at the venue during show hours. Large tap targets, minimal typing, fast task completion over visual polish.

**Out of scope here**: creating/editing shows, venues, or performer profiles (Owner web dashboard); anything platform-wide (Admin console). A staff member's job is executing what the owner already set up, not configuring it.

### 2. Suggested visual direction *(a starting point — adjust freely)*

Utilitarian and fast, closer to a point-of-sale or event-check-in app than to the atmospheric Audience site. High contrast for readability in a dim venue, clear large status colors (a green "Confirmed" vs. a red "Already used" needs to read instantly, not require careful reading). A light touch of the MusicLounge palette keeps it on-brand, but clarity wins over mood here every time.

### 3. Navigation

Simple bottom tab bar, since a staff member switches between a small number of modes during a shift: **Sell** (walk-in tickets), **Check-In** (door scanning), **F&B** (kitchen/counter orders). Account access (profile, notifications) can live behind a smaller icon rather than taking a full tab slot — it's used rarely mid-shift.

---

### 4. Screens by flow

#### Flow A — Getting Started

**Log In**
- Purpose: authenticate — same login screen as the Audience/Owner sites (email/password or Google), no separate signup flow exists for Staff.
- Note: a staff member only gets this access after being assigned by a venue owner (outside this app entirely) — there's nothing to design for "becoming staff," only for using the access once granted. If access is ever revoked by the owner, the next login (or next API call) simply stops working for operational actions — design a clear "you no longer have access to this venue" state rather than assuming access, once granted, is permanent.

#### Flow B — Selling Tickets at the Counter

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

#### Flow C — Door Check-In

**Check-In Scanner**
- Purpose: scan a ticket's QR code to admit someone at the door.
- Type: camera-based scanner as the primary interaction.
- Content: live camera view; on a successful scan, a preview step shows ticket details (holder, tier) *before* committing the check-in — these are two separate steps, don't auto-confirm on scan alone.
- Actions: "Confirm Check-In" after preview.
- States: **each rejection reason needs its own clear message**, not a generic "invalid ticket" — already checked in before (most common, needs to read as "already used," not an error); wrong show/time; online-only ticket presented at a physical door (shouldn't need door check-in at all); ticket mid-transfer ("frozen," ask them to wait). After a successful check-in, briefly show a confirmation (name/tier) for 1-2 seconds, then automatically reset for the next scan — don't require a manual "done" tap between every single guest.
- Note: **there is no offline fallback** — if the connection drops mid-scan, that's a known, accepted limitation. Show connection status honestly; never let a scan appear to succeed when it didn't actually reach the server.

#### Flow D — F&B Order Management

**Order Board**
- Purpose: see and progress every food/drink order for the venue right now.
- Type: list or kanban-style board grouped by status.
- Content: orders with items, table/zone note, current status.
- Actions: "Take Order for Table" (staff placing an order on a customer's behalf, same flow whether the customer ordered themselves via their own device or not); advance an order's status — **the sequence is fixed** (Pending → Preparing → Served → Paid), so show exactly one "next step" button per order rather than a free-choice dropdown; "Cancel" is available as a separate exit at any point before Paid.
- States: empty state per status column (e.g. nothing "Preparing" right now). Marking an order "Paid" is just a status flag here — it doesn't verify money actually changed hands, so don't design any payment-confirmation ceremony around it, it's a simple tap.
- Note: the customer tracking their own order (in the separate Audience F&B app) doesn't get a live push when staff update status here — no real-time indicator needed on this screen for "customer has seen this," there isn't one.

---

### 5. Cross-screen behaviors worth designing consistently

- **Speed over confirmation dialogs**: this app is used under time pressure with a line of people waiting — minimize taps-per-transaction everywhere it's safe to (e.g. check-in auto-resets, no "are you sure" on routine status advances).
- **Single-venue context**: a staff member's session is scoped to exactly one venue for their whole shift — no venue switcher needed anywhere in this app, unlike the Owner dashboard.
- **Connection-state honesty**: check-in and walk-in sales are the two screens where a false "success" is genuinely costly (double-admits, unrecorded cash sales) — always make network/sync state visible rather than optimistically assuming success.

---

*Source: [View-Design-Spec.md](View-Design-Spec.md) §2 (Staff-relevant views across group S, plus the shared Livestream Operations view with Owner, the shared account-management views from A/B/I) and §3.3 (Staff flow diagrams). Livestream operations (creating/starting/ending a stream) are shared 1:1 with the Owner dashboard's own screen — see [stitch-brief-owner-web.md](stitch-brief-owner-web.md) Flow E for that spec; not repeated here since Staff would use the exact same interaction, just from this app's visual shell if it's ever needed on mobile. See [platform-architecture.md](platform-architecture.md) for the platform-split rationale.*

---

# Surface 5: Admin — Web Console


> Distilled for Google Stitch from [View-Design-Spec.md](View-Design-Spec.md) §2 (view catalog) and §3 (Admin flow diagrams), scoped to the **Admin web surface** — see [platform-architecture.md](platform-architecture.md) for why this is a desktop back-office console, separate from every other surface.
> Rewritten for a design tool, not a dev handoff: no raw field/endpoint names, no BE-only validation trivia — only what actually shapes a screen.

---

### 1. App overview

**MusicLounge Admin** is the internal back-office console for the platform's own operators: approve new venues, moderate show/livestream content, handle venue penalties and appeals, process refunds, verify bank accounts, resolve customer complaints, manage user accounts, and configure platform-wide settings. There's no signup here — every admin account is created out-of-band by direct database access as a deliberate security posture, so this app's journeys all start from an already-provisioned login. This is queue-driven work: most screens are "here's a list of things waiting for a decision," not a browsing experience.

**Platform**: Website, desktop-first, no exceptions — dense data tables, multi-field review forms, cross-referencing several data sources at once. Never design a mobile-optimized version of this; that's not how this job gets done.

### 2. Suggested visual direction *(a starting point — adjust freely)*

~~A no-nonsense operations console — think airline ops center or a trust-and-safety review queue, not a consumer product. Favor information density and fast scanning over visual flourish... this is the one surface where diverging furthest from the consumer brand is the right call.~~ **Superseded 2026-08-17** — user confirmed Admin should stay on the same Warm Luxury Lounge system as every other surface (see "Design Execution Principles" at the top of this file for exactly how: neutral surface tones, primary reserved for the one primary action per row, editorial list rows instead of tables). Keep this struck-through paragraph as a record of the original reasoning (internal-staff audience, review-queue nature), since it's still *why* density/scannability matter here — just not a license to drop the shared design system.

### 3. Top-level navigation

Sidebar navigation grouped by function: **Approvals** (venues, show/livestream moderation), **Penalties & Appeals**, **Finance** (refunds, donation reversals, bank verification, ledger integrity), **Complaints**, **Users**, **Platform Settings** (taxonomy, subscription plans), **Monitoring** (platform stats, background jobs), plus a notification bell that's especially important here — several queues have no natural entry point except a notification (see §5).

---

### 4. Screens by flow

#### Flow A — Venue Approval

**Pending Venues**
- Purpose: review and approve/reject newly registered venues before they can operate.
- Content: queue of venues awaiting review with their submitted details.
- Actions: "Approve" or "Reject" (with a reason).
- States: empty state "No venues waiting." Every item in this queue is actionable immediately — no conditional logic gating entries here.

#### Flow B — Content Moderation

**Moderation Queue**
- Purpose: review show and livestream submissions before they go public/live, informed by an AI-generated risk score as a suggestion (not a decision — the admin has full discretion to go against it).
- Content: tabbed or filterable queue (shows vs. livestreams), each item showing its risk score and submitted details.
- Actions: "Approve" or "Reject" — a written note is **required** when rejecting, optional when approving.
- States: separate empty state per tab. Approving a show makes it public to Audience immediately; approving a livestream unlocks the "Start Broadcasting" button on the Owner/Staff side.

#### Flow C — Penalties & Appeals

**Issue Penalty**
- Purpose: record a penalty decision against a venue.
- Content: form — target venue, penalty level (Warning / Suspension / Ban), reason, evidence reference, and a suspension-length field that only appears when the level is Suspension.
- Actions: "Issue Penalty."

**Review Appeal**
- Purpose: uphold or overturn a venue's appeal against a penalty.
- Content: the penalty and the owner's written appeal reason.
- Actions: "Overturn" or "Uphold," with a review note.
- States: **known gap** — there's no list view of every currently-open appeal anywhere in the system; an admin only reaches this screen already knowing which penalty's id they're reviewing (typically via a notification). Don't design an "all open appeals" list screen assuming the data exists to power it — either accept notifications as the sole entry point, or flag this as a backend gap to close first (tracked in [View-Design-Spec.md §5.2](View-Design-Spec.md#52-endpoint-còn-thiếu-đã-xác-nhận-qua-đọc-code-không-phải-suy-đoán)). An overturned penalty doesn't automatically clear other overlapping active penalties on the same venue, and doesn't automatically reverse any financial side-effect (like a shortened subscription) — that still needs manual follow-up outside this screen today.

#### Flow D — Finance Operations

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

#### Flow E — Complaints

**Resolve Complaints**
- Purpose: review and resolve every complaint filed on the platform, including from people with no account at all.
- Content: queue with target (ticket/show/venue/donation/penalty), category, description, evidence.
- Actions: "Resolve" — status, a resolution note, a resolved-action type, and (only relevant for Refund/Compensate) an amount.
- States: the automation behind a resolution varies by what's being resolved and what action is chosen — refund/compensate against a specific ticket auto-creates a refund request; against a donation, nothing automatic happens and the admin needs to separately use Reverse a Donation; take-down-content against a show cancels it and refunds every confirmed ticket; anything else is just a recorded decision with no further automation. Make the "what happens next" implication of each combination legible in the UI rather than uniform. A guest complainant (no account) shows their contact phone prominently in place of a name — they'll be notified by text message once resolved, not through any in-app channel.

#### Flow F — User Management

**Manage Users**
- Purpose: search, inspect, and lock/unlock any user account.
- Content: searchable/filterable user list (by name/email, role, active status); a detail view per user including any submitted ID-verification photos.
- Actions: "Deactivate" or "Reactivate" — show exactly one of the two at a time, based on current status, never both.
- States: ID photos load through a protected endpoint, not a guessable public URL — treat them as sensitive throughout.

#### Flow G — Platform Settings

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

#### Flow H — Livestream Enforcement

**Force-Stop a Livestream**
- Purpose: shut down a livestream that's actively broadcasting a violation.
- Type: this action lives on the live viewing screen itself (an admin can always open any livestream to watch it, same as an Audience viewer would, plus this one extra control) rather than as a separate screen — see [stitch-brief-audience-web.md](stitch-brief-audience-web.md) Flow E for the base viewing experience; add a "Force Stop" control visible only to admins, requiring a reason.
- States: every viewer gets disconnected the instant this fires — no confirmation delay expected on their end.

#### Flow I — Platform Monitoring

**Platform Statistics**
- Purpose: system-wide analytics (distinct from a single venue's own analytics on the Owner side — a completely separate screen, not a permissions toggle on the same one).
- Content: platform-wide metrics dashboard.

**Background Jobs**
- Purpose: manually trigger a background maintenance job on demand (for troubleshooting), from a fixed known list of job names — there's no dynamic job registry to browse.
- Actions: select a job, "Run Now."
- States: this triggers one immediate run only — make clear it does **not** change that job's regular schedule, to avoid it being mistaken for a cron-configuration screen. No synchronous result comes back — show "run requested" feedback, then let the job's actual effect show up wherever that job's output naturally lives (e.g. running the ledger job means checking back on Ledger Integrity Check afterward).

---

### 5. Cross-screen behaviors worth designing consistently

- **Notifications are the primary entry point for several queues**: unlike a typical dashboard where every workflow starts from a list screen, a few of these (appeal review, bank verification, donation reversal) currently have **no list view at all** — the admin reaches them via a notification's deep link or by already knowing an id. Design the notification panel with this in mind: it's not just an inbox here, it's load-bearing navigation for real workflows.
- **Security alerts land in the same inbox as everything else**: background jobs watching for account-drift, login spikes, push-delivery failures, and payment-reconciliation mismatches all notify admins through the same Notifications channel Owners and Audience use — there's no separate "security alerts" surface. Make sure severity is visually distinguishable within one shared list.
- **"No real payment-provider integration" is a recurring caveat, not a one-off**: refund approval and the ledger tools all operate on internal accounting only in this environment — repeat the "internal reconciliation, not a live bank transfer" framing consistently rather than only on one screen, so it isn't easy to miss.

---

*Source: [View-Design-Spec.md](View-Design-Spec.md) §2 (Admin-relevant views across groups T, U, V, W, X, Y, Z, plus the Admin-specific edit/delete rights folded into the Owner-run Performer Profiles screen, the shared Livestream viewing screen, and the shared account-management views from A/B/I) and §3.4 (Admin flow diagrams). See [platform-architecture.md](platform-architecture.md) for the platform-split rationale. Several screens above explicitly flag missing list/edit endpoints as backend gaps rather than silently designing around them — cross-check [View-Design-Spec.md §5.2](View-Design-Spec.md#52-endpoint-còn-thiếu-đã-xác-nhận-qua-đọc-code-không-phải-suy-đoán) before starting on those specific screens.*
