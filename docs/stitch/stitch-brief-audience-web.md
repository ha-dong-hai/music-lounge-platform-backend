# Stitch Design Brief — MusicLounge, Audience Website

> Distilled for Google Stitch from [View-Design-Spec.md](View-Design-Spec.md) §2 (view catalog) and §3 (Audience flow diagrams), scoped to the **web surface only** — see [platform-architecture.md](platform-architecture.md) for why Audience is web-first while a separate native app exists purely for venue F&B ordering ([stitch-brief-audience-mobile-fnb.md](stitch-brief-audience-mobile-fnb.md)).
> Rewritten for a design tool, not a dev handoff: no raw field/endpoint names, no BE-only validation trivia — only what actually shapes a screen.

---

## 1. App overview

**MusicLounge** is a website for discovering and attending live-music shows at small venues ("lounges") in Vietnam — buy tickets, watch livestreams of shows you can't attend in person, tip ("donate") performers in real time while watching, and manage your account. This is the primary consumer surface: no business tools, just browse → buy → attend/watch → engage.

**Platform**: Responsive website — desktop is the primary design target (most browsing/purchase decisions happen there), but every screen must also work cleanly in a phone's browser, since a ticket's QR code needs to be pulled up at the venue door without requiring an app install. Food/drink ordering while physically at a show is deliberately **out of scope here** — that's a separate, minimal native app (see linked brief above); don't design an in-venue ordering flow into this site.

## 2. Suggested visual direction *(a starting point — adjust freely, this isn't a fixed brand)*

Live-music, nightlife, intimate-venue energy — think small jazz/acoustic lounges, not stadium concerts. Suggest: dark-mode-friendly base (venues are evening/night experiences), one warm accent color (amber/coral range reads as "stage light" without being a cliché neon-club look), generous imagery (venue photos, performer avatars, show posters) since this is a highly visual, discovery-driven site. Avoid generic SaaS-dashboard styling — this should feel like a consumer entertainment site (closer to a ticketing/streaming service than a business tool).

## 3. Top-level navigation

Standard website header, not a mobile tab bar: logo, primary nav links (**Discover**, **My Tickets**, **Following**), a search bar, and an account menu (avatar dropdown → Notifications, My Donations, My F&B history — read-only here, see below —, My Complaints, Profile, Privacy & Data). Keep the header sticky/persistent since navigation happens across long browsing sessions.

---

## 4. Screens by flow

### Flow A — Onboarding

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

### Flow B — Discover

**Home / Show Search**
- Purpose: browse and search live shows.
- Content: search bar, filter sidebar or chips (genre, mood, date, price, format), trending section, show cards (poster, venue name, date, price range) in a grid.
- Actions: click a show card → Show Detail; apply filters; type-ahead search suggestions.
- States: empty state for "no results" (distinct from a loading skeleton).

**Show Detail**
- Purpose: everything about one show before deciding to buy a ticket, watch live, or rate it.
- Content: poster/cover image, show name, date/time, venue name + link, format badge (in-person / livestream / hybrid), performer lineup (avatar, name, bio snippet — click to see performer's public page), description, ticket price range.
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

### Flow C — Following

**Following / Wishlist**
- Purpose: manage venues followed and shows wishlisted.
- Content: two tabs — "Venues" and "Shows".
- Actions: unfollow / remove from wishlist.
- States: separate empty state per tab.

### Flow D — Buying a Ticket

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

### Flow E — Watching Live & Donating

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

### Flow F — Rating

**Rate Show**
- Type: modal on top of Show Detail.
- Content: star rating + comment text field, show name/poster for context.
- Actions: "Submit Rating".
- Note: only ever reachable once per show — after submitting, the "Rate" button on Show Detail disappears rather than the form showing an error on a second attempt.

### Flow G — Complaints

**File a Complaint**
- Purpose: report a problem (bad ticket experience, unresolved donation, venue conduct, etc.) — reachable even by someone not logged in.
- Content: category picker, target (which show/ticket/venue — often pre-filled if opened from that screen's context), description, optional evidence/photos, phone number (required if not logged in).
- Actions: "Submit".
- States: on submit while logged out, **the confirmation screen must clearly display and let the user save the complaint's reference number** — it's their only way to check on it later (guest lookup uses this number + their phone).

**My Complaints**
- Purpose: track filed complaints (logged-in users only).
- Content: list with status; click for full detail including admin's resolution notes.
- States: empty state.

### Flow H — Notifications & Profile

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

## 5. Cross-screen behaviors worth designing consistently

- **Real-time nudges**: a few things update live without the user acting — the donation ticker, live chat, and a "hurry, low stock" push when a wishlisted show is close to selling out. Everything else (donation pipeline progress) is manual-refresh only. Don't design loading spinners that imply everything is realtime.
- **Guest vs logged-in parity**: most of Discover (Flow B), the donation transparency feed, and complaint filing work identically for a logged-out visitor. Don't gate the *browsing* experience — only gate the *write* actions (buy, donate, wishlist, rate), each with a clean "log in to continue" prompt that returns the user to what they were doing.
- **Countdown urgency**: the ticket hold timer (15 min) is the one place urgency/countdown UI really matters — treat it as a first-class design element, not a small corner label.
- **Phone-browser reality check**: even though desktop is the primary target, the Ticket Detail QR screen and the Log In screen in particular need to look good at phone width too — these are the two screens most likely opened on a phone (at the door, or checking email on the go).

---

*Source: [View-Design-Spec.md](View-Design-Spec.md) §2 (Audience-relevant views across groups A, B, C, D, E, F, H, I — excludes group G, F&B, which is a separate native app) and §3.1 (Audience flow diagrams). See [platform-architecture.md](platform-architecture.md) for the web/mobile split rationale. Open UX decisions this brief already made a default call on (modal vs page for Donate/Rate, shared Payment Result template, split vs merged donation feed variants) are tracked as open questions in [View-Design-Spec.md §5.3](View-Design-Spec.md#53-quyết-định-ux-cần-đội-thiết-kế-xác-nhận-be-không-quyết-định-được-đây-là-lựa-chọn-sản-phẩm) — confirm before treating this brief as final.*
