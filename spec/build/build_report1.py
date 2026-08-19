"""Report 1 — Project Introduction, built fresh from the pristine FPT template."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from docxkit import Report
import facts as F

r = Report("Report1_Project Introduction.docx",
           "Report1_Project Introduction - MusicLounge.docx")

# ── PASS 1: clear every templated region before inserting anything ───────────
r.clear_regions(
    ("1.1 Project Information", "1.2 Project Team"),
    ("1.2 Project Team", "2. Product Background"),
    ("2. Product Background", "3. Existing Systems"),
    ("3. Existing Systems", "3.1 System name1"),
    ("3.1 System name1", "3.2 System name2"),
    ("3.2 System name2", "4. Business Opportunity"),
    ("4. Business Opportunity", "5. Software Product Vision"),
    ("5. Software Product Vision", "6. Project Scope & Limitations"),
    ("6. Project Scope & Limitations", "6.1 Major Features"),
    ("6.1 Major Features", "6.2 Limitations & Exclusions"),
    ("6.2 Limitations & Exclusions", None),
)

# ── PASS 2: insert real content ─────────────────────────────────────────────
r.record_of_changes([
    [F.DOC_DATE, "A", F.TEAM[0]["name"],
     "Project Introduction for the complete MusicLounge platform — five client surfaces, the "
     "backend API, the imaging microservice and the Azure deployment."],
])

# §1.1
a = r.heading("1.1 Project Information")
a = r.add_bullets(a, [
    f"Project name: {F.PROJECT['name']}",
    f"Project code: {F.PROJECT['code']}",
    f"Group name: {F.PROJECT['group']}",
    f"Software type: {F.PROJECT['software_type']}",
    f"Duration: {F.TIMELINE['start']} – {F.TIMELINE['end']} ({F.TIMELINE['weeks']} weeks)",
])

# §1.2
a = r.heading("1.2 Project Team")
r.add_table(a, ["Full Name", "Role", "Email", "Mobile"],
            [[F.SUPERVISOR["name"], F.SUPERVISOR["role"], F.SUPERVISOR["email"], F.SUPERVISOR["mobile"]]] +
            [[m["name"], m["role"], m["email"], m["mobile"]] for m in F.TEAM],
            widths=[2.0, 1.1, 1.9, 1.2])

# §2
a = r.heading("2. Product Background")
r.add_paragraphs(a, [
    "Demand for live music in small, intimate rooms — music lounges, acoustic cafés, “phòng "
    "trà” — has been rising in Vietnam, particularly among younger audiences. The venues that "
    "serve that demand are small businesses running a different show most nights of the week, and "
    "they run today on a patchwork of manual processes and disconnected tools.",

    "Door sales are cash and paper. A venue that wants to sell online falls back on a general "
    "event-ticketing platform designed around a single fixed showtime, which fits a cinema or a "
    "stadium concert far better than a lounge where a ticket is really valid for the whole evening, "
    "where walk-in sales continue after the music has started, and where the owner may price by "
    "time slot rather than by seat.",

    "Livestreaming a show — increasingly common as a way to reach an audience beyond the room's "
    "capacity — typically runs on a general streaming platform with no way to limit viewing to "
    "genuine ticket holders. Tips that audience members want to give a performer happen informally, "
    "as cash at the door or an ad-hoc bank transfer, with no shared record of what was promised, "
    "collected or actually paid out, and no transparency to the performer about what the venue "
    "collected on their behalf.",

    "MusicLounge replaces that patchwork with one platform built around how a small live-music "
    "venue actually operates.",
])

# §3
a = r.heading("3. Existing Systems")
a = r.add_after(a, "Three existing products, each covering part of what MusicLounge brings together.")

a = r.heading("3.1 System name1")
a.runs[0].text = "3.1 Ticketbox (ticketbox.vn)"
for extra in a.runs[1:]:
    extra._element.getparent().remove(extra._element)
r.add_bullets(a, [
    "Vietnam's largest general-purpose event ticketing platform, used for concerts, conferences and festivals nationwide.",
    "Actors: event organiser, buyer, platform (payment processing and ticket delivery).",
    "Features: online sales with QR entry, seat and zone selection, organiser dashboards, several payment methods.",
    "Pros: strong brand recognition and buyer trust in Vietnam, mature payment integration, handles large one-off events well.",
    "Cons for this segment: built around a single fixed-showtime event rather than a venue running a different show most nights; no livestream, no performer tipping, no in-venue food and drink ordering; and no recurring per-venue tooling — seating zones, staff, a subscription relationship — because each event is treated as a standalone listing.",
])

a = r.heading("3.2 System name2")
a.runs[0].text = "3.2 Twitch + Streamlabs"
for extra in a.runs[1:]:
    extra._element.getparent().remove(extra._element)
a = r.add_bullets(a, [
    "The dominant global pairing for live-streamed content with real-time viewer donations: Twitch for the stream, Streamlabs for the on-stream donation alert overlay.",
    "Actors: streamer, viewer, platform.",
    "Features: live video with chat, real-time donation alerts, subscription-based creator monetisation.",
    "Pros: an extremely mature real-time tipping experience, and a pattern audiences already recognise.",
    "Cons for this segment: a general content platform with no tie to a physical venue or a specific in-person show — no ticketing, no door to check a ticket at, no food and drink ordering; and a donation goes straight to the individual streamer's own account with no venue-level acknowledgement, settlement or commission step, which does not match a relationship where the venue is financially and legally the intermediary handling money on the performer's behalf.",
])

a = r.add_after(a, "3.3 Veeps (veeps.com)", "Heading 3")
r.add_bullets(a, [
    "A ticketed livestream platform for touring artists, now owned by Live Nation. Its hybrid mode — livestreaming a show while it plays to an in-person audience — is the closest real-world precedent to MusicLounge's own model.",
    "Actors: artist, viewer, platform, and in the hybrid programme the partner venue supplying the room.",
    "Features: pay-per-view livestream tickets or an all-access subscription, live chat, VIP add-ons, and an on-demand replay catalogue.",
    "Pros: a proven hybrid concept, and artist-friendly economics — the full ticket price goes to the artist and the platform takes a flat service fee.",
    "Cons for this segment: built for touring acts at large venues rather than a neighbourhood lounge — no seating-zone, walk-in or box-office tooling, and no food and drink ordering; its donation feature is a fixed charity option attached to the ticket rather than real-time tipping; and money flows from ticket to artist directly, with no venue-side settlement step.",
])

# §4
a = r.heading("4. Business Opportunity")
r.add_paragraphs(a, [
    "Among the platforms most commonly used for this space — a general ticketing platform (§3.1), a "
    "general livestream-and-tipping platform (§3.2), and the closest hybrid precedent (§3.3) — none "
    "brings together the four things a small Vietnamese live-music venue needs at once: ticketing "
    "that matches whole-evening validity and continuing walk-in sales rather than a fixed showtime; "
    "a livestream limited to genuine ticket holders; a transparent, auditable path from an audience "
    "member's tip to the performer who earned it; and the in-venue operational tools — food and "
    "drink ordering, door check-in, counter sales — that staff need while a show is running.",

    "That combination is the opportunity. A platform built for the small live-music-venue segment, "
    "rather than adapted down from a stadium-concert or general-streaming product, can serve a "
    "segment that existing ticketing and streaming platforms address only in part.",
])

# §5
a = r.heading("5. Software Product Vision")
r.add_paragraphs(a, [
    "For independent live-music venue owners in Vietnam who want to sell tickets, stream their "
    "shows and pay performers transparently without stitching together three disconnected products, "
    "and for the audiences who attend or watch those shows,",
    "the MusicLounge platform is a ticketing, livestream and donation marketplace",
    "that lets a venue sell both online and walk-in tickets on its own real terms, limit a "
    "livestream to genuine ticket holders, and route audience tips to performers through an "
    "auditable payout pipeline — while an audience member discovers shows, buys a ticket, watches "
    "from home when they cannot attend, and tips a performer in real time, all from one account.",
    "Unlike a general-purpose event-ticketing platform, which assumes a fixed showtime and offers "
    "neither livestreaming nor tipping, and unlike a general livestream platform, which has no "
    "ticketing, no venue relationship and no in-person operational tools, MusicLounge is built "
    "around the specific operating pattern of a small venue running a different show most nights.",
])

# §6
a = r.heading("6. Project Scope & Limitations")
r.add_after(a, "What the platform delivers is listed in §6.1; what it deliberately does not "
               "attempt is listed in §6.2, so expectations are set on both sides.")

FEATURES = [
    "Account and identity — self-service registration for Audience and Owner, email OTP and phone verification, Google sign-in, profile and identity-document management, and self-service export or permanent erasure of one's own personal data. One identity works across all five client surfaces.",
    "Venue setup — venue registration with business-licence upload, profile and photo-gallery management, and a drag-and-drop seating-zone editor whose zones are reused by every show at that venue.",
    "360° virtual venue tour — the Owner uploads a finished panorama or several ordinary room photos that the imaging microservice stitches automatically, then places navigation and information hotspots; audiences explore the finished tour from the venue page.",
    "Venue approval — an Admin review queue that must approve a venue before it is publicly listed or may publish any show.",
    "Show management — a creation wizard with a performer line-up builder, ticket tiers, time-slot pricing and an AI-generated or uploaded poster, covering the full lifecycle from draft through review, publication, rescheduling, format change and cancellation.",
    "Content moderation — AI risk-scoring of every show and livestream, with the final decision always taken by a human Admin, under a 24-hour review deadline.",
    "Discovery and personalisation — public browsing and search, show and venue pages, follow and wishlist, show ratings, and an opt-in AI-personalised recommendation feed.",
    "Online ticketing — tier selection held for 15 minutes behind a visible countdown, VNPay checkout, a QR ticket, peer-to-peer transfer, and cancellation or refund requests.",
    "Box office and door operations — cash walk-in sales at the counter and QR check-in at the door, both honouring whole-evening ticket validity rather than a fixed start time.",
    "Gated livestream — broadcast control for the venue, with viewing limited to genuine ticket holders within a concurrent-device limit, plus real-time chat and a viewer count.",
    "Real-time performer donations — in-stream tipping with a public donation ticker, a transparent settled-donation feed, and an owner-acknowledgement to performer-payout pipeline with evidence captured at each step.",
    "In-venue food and drink ordering — menu browsing, cart and live order tracking from a native mobile app, with a kitchen and counter order board for staff.",
    "Payments and double-entry ledger — every movement of money recorded as a balanced accounting journal, with two-tranche settlement payouts to venue owners and automated reconciliation against the payment gateway.",
    "Owner subscriptions — a plan catalogue, subscribe, renew and cancel, and entitlement caps (tickets per show, AI-poster and tour-scene quotas) enforced at the point of use.",
    "Complaints — a channel open even to guests, filed and later looked up by reference number and phone with no account required, feeding an Admin resolution queue with deadline tracking.",
    "Venue penalties and appeals — penalty issuance with evidence, Owner appeal submission, and automatic escalation or overturn when a review deadline passes.",
    "Admin back office — user management, refund processing, bank-account verification, taxonomy and subscription-plan management, ledger-integrity checking, platform analytics and background-job monitoring.",
    "Notifications — an in-app inbox on every surface, push notifications to the mobile apps, and email and SMS for events that must reach a user while they are away from the platform.",
]
a = r.heading("6.1 Major Features")
a = r.add_after(a, "Each feature below is traceable to the use cases in Report 3 §2.2 and to the "
                   "screens catalogued in Report 3 §3.1.")
for i, text in enumerate(FEATURES, 1):
    a = r.add_after(a, f"FE-{i:02d}:\t{text}", "List Paragraph")

LIMITS = [
    "The VNPay integration runs against the sandbox environment; there is no live production merchant relationship, consistent with this being an academic capstone rather than a commercially trading platform.",
    "There is no self-service or API path to create an Admin account. Every Admin exists only through a direct database provisioning step, watched by an automated drift-detection job. This is a deliberate security posture, not a missing feature.",
    "A Staff account may be actively assigned to exactly one venue at a time; someone genuinely working at two venues needs two accounts. This is an intentional simplification.",
    "Refund approval reverses the internal ledger entries and records the payout for manual transfer; it does not settle money back through the live payment provider, matching the sandbox limitation above.",
    "SMS delivery for one-time codes and notifications targets Vietnamese phone numbers only.",
    "Door check-in and counter sales have no offline mode. A dropped connection blocks the action rather than silently queuing it — an accepted trade-off against the risk of double-selling a seat.",
    "Bank-account verification is a manual Admin action taken after reconciling ownership outside the system; there is no banking-API integration to verify an account holder automatically.",
    "The two native mobile applications are distributed as signed internal review builds rather than published to the public App Store and Google Play, which is a capstone-scope decision rather than a technical constraint.",
    "The user interface ships in Vietnamese only; no language switch is provided, because the platform targets the Vietnamese market exclusively.",
]
a = r.heading("6.2 Limitations & Exclusions")
for i, text in enumerate(LIMITS, 1):
    a = r.add_after(a, f"LI-{i:02d}:\t{text}", "List Paragraph")

path = r.save()
print(f"built {path}")
