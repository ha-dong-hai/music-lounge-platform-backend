"""Report 3 — Software Requirement Specification, built fresh from the pristine template."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from docxkit import Report
import facts as F
import usecases as U

DIA = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "diagrams", "out"))
S = F.SCALE

r = Report("Report3_Software Requirement Specification.docx",
           "Report3_Software Requirement Specification - MusicLounge.docx")

# ── PASS 1 ───────────────────────────────────────────────────────────────────
r.clear_regions(
    ("1. Product Overview", "2. User Requirements"),
    ("2. User Requirements", "2.1 Actors"),
    ("2.1 Actors", "2.2 Use Cases"),
    ("2.2 Use Cases", "2.2.1 Diagram(s)"),
    ("2.2.1 Diagram(s)", "2.2.2 Descriptions"),
    ("2.2.2 Descriptions", "3. Functional Requirements"),
    ("3. Functional Requirements", "3.1 System Functional Overview"),
    ("3.1 System Functional Overview", "3.1.1 Screens Flow"),
    ("3.1.1 Screens Flow", "3.1.2 Screen Descriptions"),
    ("3.1.2 Screen Descriptions", "3.1.3 Screen Authorization"),
    ("3.1.3 Screen Authorization", "3.1.4 Non-Screen Functions"),
    ("3.1.4 Non-Screen Functions", "3.1.5 Entity Relationship Diagram"),
    ("3.1.5 Entity Relationship Diagram", "3.2 <<Feature Name 1>>"),
    ("3.2 <<Feature Name 1>>", "4. Non-Functional Requirements"),
    ("4. Non-Functional Requirements", "4.1 External Interfaces"),
    ("4.1 External Interfaces", "4.2 Quality Attributes"),
    ("4.2 Quality Attributes", "5. Requirement Appendix"),
    ("5. Requirement Appendix", "5.1 Business Rules"),
    ("5.1 Business Rules", "5.2 Common Requirements"),
    ("5.2 Common Requirements", "5.3 Application Messages List"),
    ("5.3 Application Messages List", None),
)

# ── PASS 2 ───────────────────────────────────────────────────────────────────
r.record_of_changes([
    [F.DOC_DATE, "A", F.TEAM[0]["name"],
     f"Software Requirement Specification for the complete platform: {S['surfaces']} client "
     f"surfaces, {S['use_cases']} use cases, {S['screens_distinct']} distinct screens and "
     f"{S['entities']} entities."],
])

# §1 Product Overview
a = r.heading("1. Product Overview")
a = r.add_paragraphs(a, [
    F.PRODUCT_ONE_LINER + " It replaces the patchwork of paper door sales, general-purpose "
    "ticketing, ungated streaming and informal cash tipping that such venues rely on today with a "
    "single system built around how they actually operate: a ticket valid for the whole evening, "
    "walk-in sales that continue after the music starts, a livestream limited to genuine ticket "
    "holders, and an auditable path from an audience member's tip to the performer who earned it.",

    f"The platform is delivered as {S['surfaces']} client applications over one shared REST and "
    "SignalR API, each built for one job rather than one universal app: the Audience Website "
    "(discover, buy, watch, tip, rate), the Audience Mobile app (order food and drink while seated "
    "at a venue), the Owner Web Dashboard (venue setup, shows, money), the Staff Mobile app "
    "(counter sales, door check-in, the F&B board) and the Admin Web Console (approval, moderation, "
    "refunds, complaints, monitoring).",

    "Figure 1 shows the system boundary: who exchanges data with the platform, and in which "
    "direction. Everything inside the boundary is built by this project; everything outside is a "
    "third-party service the platform integrates with.",
])
a = r.add_figure(a, os.path.join(DIA, "context.png"),
                 "System context — actors and external services around the platform boundary")
a = r.add_paragraphs(a, [
    "Constraints and assumptions. All payments are intermediated by a licensed gateway and the "
    "platform never handles card data. The user interface is Vietnamese-only, because the platform "
    "targets the Vietnamese market. Door check-in and counter sales assume connectivity — there is "
    "no offline mode, deliberately, to avoid double-selling a seat. Administrator accounts are "
    "provisioned directly by an operator; there is no self-service path to become one.",
])

# §2.1 Actors
a = r.heading("2.1 Actors")
a = r.add_after(a,
    f"The platform has {S['actors']} actors. Four of them ({S['login_roles']}) hold accounts and "
    "sign in; Guest is the unauthenticated visitor, and Performer is a catalogue record maintained "
    "by an Owner rather than a login of its own.")
r.add_table(a, ["#", "Actor", "Description"],
            [[str(i), name, desc] for i, (name, desc) in enumerate(F.ACTORS, 1)],
            widths=[0.45, 1.15, 4.6])

# §2.2 Use Cases
a = r.heading("2.2 Use Cases")
r.add_after(a,
    f"{S['use_cases']} use cases across {len(U.GROUPS)} feature areas. Each is named as a verb "
    "phrase from the actor's point of view and describes an interaction that leaves the actor with "
    "something of value.")

a = r.heading("2.2.1 Diagram(s)")
a = r.add_after(a,
    "The use cases are split across two diagrams to stay legible: the public and customer-facing "
    "side, then the operator side. An actor generalisation arrow means the child actor can also do "
    "everything the parent can.")
a = r.add_figure(a, os.path.join(DIA, "usecase-audience.png"),
                 "Use cases — Guest and Audience")
a = r.add_figure(a, os.path.join(DIA, "usecase-operator.png"),
                 "Use cases — Owner, Staff and Admin")

a = r.heading("2.2.2 Descriptions")
r.add_table(a, ["ID", "Feature Area", "Use Case", "Actors", "Description"],
            [[uid, feat, name, actors, desc] for uid, feat, name, actors, desc in U.numbered()],
            widths=[0.6, 1.15, 1.35, 1.05, 2.05])

# §3.1 System Functional Overview
a = r.heading("3.1 System Functional Overview")
r.add_after(a,
    f"This section gives the functional shape of the whole platform: how screens are distributed "
    f"across the {S['surfaces']} surfaces (§3.1.1), what each screen is for (§3.1.2), who may reach "
    f"it (§3.1.3), the {S['job_classes']} functions that run without a screen at all (§3.1.4), and "
    f"the {S['entities']} entities behind them (§3.1.5).")

a = r.heading("3.1.1 Screens Flow")
a = r.add_paragraphs(a, [
    "Splitting the product into five applications is a product decision, not a technical one — the "
    "API does not care which client calls it. Each surface exists because the actor's work is "
    "genuinely different in shape: browsing and buying happens occasionally and works fine in a "
    "browser; ordering a drink at the table is the one moment a phone app is genuinely better; "
    "venue administration is desk work with multi-field forms and a drag-and-drop editor; floor "
    "operations happen standing up, moving around the room; and back-office review needs dense "
    "tables on a large screen.",
    f"The {S['screens_listed']} screen entries below sum to more than the "
    f"{S['screens_distinct']} distinct screens actually built, because {S['screens_shared']} account "
    "screens (notifications, profile, edit profile, identity verification, AI preferences, privacy "
    "and data, my complaints) are the same screens reused by both the Audience Website and the "
    "Owner Dashboard rather than built twice.",
])
r.add_table(a, ["Surface", "Primary Actor", "Client Type", "Screens", "Why this surface exists"], [
    ["Audience — Website", "Audience", "Responsive website", str(S['screens_by_surface']['Audience — Website']),
     "Concert-going is occasional rather than daily, so discovery, purchase, viewing and the QR at the door all work in a browser."],
    ["Audience — Mobile (F&B)", "Audience", "Native app", str(S['screens_by_surface']['Audience — Mobile (F&B)']),
     "The one genuinely phone-in-hand situation: ordering food and drink while seated at the venue. Deliberately narrow."],
    ["Owner — Web Dashboard", "Owner", "Website (dashboard)", str(S['screens_by_surface']['Owner — Web Dashboard']),
     "Desk work: multi-field forms, a drag-and-drop seating editor, financial dashboards."],
    ["Staff — Mobile", "Staff", "Native app / tablet", str(S['screens_by_surface']['Staff — Mobile']),
     "On-site and moving: counter sales, door scanning, the kitchen and bar order board."],
    ["Admin — Web Console", "Admin", "Website (console)", str(S['screens_by_surface']['Admin — Web Console']),
     "Back office: dense review queues, ledger reconciliation, user administration."],
], widths=[1.25, 0.9, 1.0, 0.6, 2.45])

a = r.heading("3.1.2 Screen Descriptions")
SCREENS = [
    ("Audience — Website", [
        ("Sign Up", "Create an account and choose the Audience or Owner role."),
        ("Verify Email", "Enter the emailed one-time code to activate the account."),
        ("Log In", "Authenticate with email and password, or with Google."),
        ("Forgot / Reset Password", "Request a reset link and set a new password."),
        ("Home / Show Search", "Browse and filter published shows."),
        ("Show Detail", "Line-up, tiers, availability and the route into purchase."),
        ("Venue Detail", "Venue profile, gallery and upcoming shows."),
        ("360° Virtual Tour", "Navigate panorama scenes and hotspots."),
        ("Performer Public Page", "A performer's biography, links and appearances."),
        ("For You (Recommendations)", "Opt-in personalised feed of suggested shows."),
        ("Following / Wishlist", "Venues followed and shows saved."),
        ("Select Tickets", "Choose tier and quantity; starts the countdown hold."),
        ("Payment Result", "Outcome of a gateway payment for ticket, donation or subscription."),
        ("My Tickets", "Confirmed, used and cancelled tickets."),
        ("Ticket Detail", "The QR code and seating details shown at the door."),
        ("Transfer Ticket", "Offer a ticket to another account."),
        ("My Refund Requests", "Status of refund requests raised."),
        ("Live Viewing Room", "Gated stream with chat and viewer count."),
        ("Donate (modal)", "Tip a performer, with privacy choices."),
        ("Public Donation Ticker", "Donations appearing as they clear."),
        ("Donation Transparency Feed", "Settled donations across the platform."),
        ("My Donations", "Status of tips given, through to performer payout."),
        ("Rate Show (modal)", "Leave a rating and comment after a show."),
        ("File a Complaint", "Report an issue; available without an account."),
        ("Complaint Lookup", "Retrieve a complaint by reference number and phone."),
        ("My Complaints", "Complaints raised by the signed-in user."),
        ("Notifications", "In-app notification inbox."),
        ("Profile / Edit Profile", "View and update personal details."),
        ("ID Verification", "Upload identity documents."),
        ("Privacy & Data", "AI consent, data export and account erasure."),
    ]),
    ("Audience — Mobile (F&B)", [
        ("Order F&B", "Browse the venue menu and build an order."),
        ("My Order", "Track an order through preparing, served and paid."),
    ]),
    ("Owner — Web Dashboard", [
        ("My Venues", "Venues owned, with approval status."),
        ("Create / Edit Venue", "Venue profile, address and business licence."),
        ("Seating Zone Editor", "Define zones and arrange them on the floor plan."),
        ("360° Tour Management", "Upload or auto-stitch scenes and place hotspots."),
        ("Venue Extras", "Gallery images and venue-specific criteria."),
        ("Staff Management", "Assign and deactivate staff for a venue."),
        ("Bank Accounts", "Payout accounts and their verification status."),
        ("Subscription Plans", "Compare available plans."),
        ("My Subscription", "Current plan, entitlements and expiry."),
        ("My Shows", "Shows at the owner's venues, by status."),
        ("Create / Edit Show", "Schedule, description, format and line-up."),
        ("Show Control Center", "Submit, publish, reschedule, change format, cancel, start, end."),
        ("Ticket Tiers", "Tiers, capacities and pricing windows."),
        ("Poster", "Upload or generate the show poster."),
        ("Legal & Royalty Declaration", "Permit and royalty references."),
        ("Livestream Operations", "Broadcast controls and viewer count."),
        ("Pending Acknowledgment", "Donations awaiting the owner's confirmation of receipt."),
        ("Awaiting Payout", "Donations acknowledged but not yet paid to the performer."),
        ("Earnings Overview", "Revenue, fees and settlements."),
        ("Venue Analytics", "Attendance and sales trends."),
        ("My Penalties & Appeals", "Penalties against the venue and appeals raised."),
        ("Performer Profiles", "Create and maintain performer records."),
        ("Manage F&B Menu", "Menus, items and prices."),
        ("Manage F&B Orders at Counter", "Orders placed at the counter."),
        ("Walk-In Sale", "Sell a ticket for cash at the counter."),
        ("Complaint Response", "Respond to complaints concerning the venue."),
        ("Notifications", "In-app notification inbox (shared with Audience Website)."),
    ]),
    ("Staff — Mobile", [
        ("Log In", "Authenticate; the session is scoped to one venue."),
        ("Walk-In Sale", "Sell a ticket for cash at the counter."),
        ("Sale Confirmation / QR", "The issued ticket and its code."),
        ("Check-In Scanner", "Scan a ticket at the door and confirm entry."),
        ("Order Board", "Advance F&B orders through their statuses."),
        ("Show Control Center (Start/End)", "Start and end the broadcast only."),
        ("Notifications", "In-app notification inbox."),
    ]),
    ("Admin — Web Console", [
        ("Pending Venues", "Approve or reject newly registered venues."),
        ("Moderation Queue", "Review shows and livestreams with AI risk scores."),
        ("Issue Penalty", "Record a penalty against a venue with evidence."),
        ("Review Appeal", "Uphold or overturn a contested penalty."),
        ("Refund Requests", "Approve or reject refunds, in full or in part."),
        ("Reverse a Donation", "Post a compensating journal for a donation."),
        ("Verify Bank Account", "Mark a payout account verified."),
        ("Ledger Integrity Check", "Confirm every journal balances."),
        ("Resolve Complaints", "Close complaints with an outcome."),
        ("Manage Users", "Search, deactivate and reactivate accounts."),
        ("Taxonomy Management", "Create, edit and delete platform tags."),
        ("Subscription Plan Management", "Maintain the plan catalogue."),
        ("Force-Stop a Livestream", "Terminate a stream that breaches policy."),
        ("Platform Statistics", "Platform-wide activity and revenue."),
        ("Background Jobs", "Scheduled job outcomes and manual triggering."),
        ("Notifications", "In-app notification inbox."),
    ]),
]
rows = []
n = 0
for surface, screens in SCREENS:
    for name, desc in screens:
        n += 1
        rows.append([str(n), surface, name, desc])
assert n == S["screens_listed"], f"{n} screen rows but facts says {S['screens_listed']}"
r.add_table(a, ["#", "Surface", "Screen", "Description"], rows,
            widths=[0.45, 1.35, 1.6, 2.8])

a = r.heading("3.1.3 Screen Authorization")
a = r.add_after(a,
    "Access is enforced on the API, not in the client: a route guard only decides what to render, "
    "and every request is re-checked server-side against the caller's role and, where the data "
    "belongs to one venue, against that venue.")
r.add_table(a, ["Screen group", "Guest", "Audience", "Owner", "Staff", "Admin"], [
    ["Public discovery (home, show, venue, tour, performer)", "X", "X", "X", "", ""],
    ["Account (sign up, verify, sign in, reset)", "X", "X", "X", "X", "X"],
    ["Profile, notifications, privacy and data", "", "X", "X", "X", "X"],
    ["Ticket purchase, my tickets, transfer, refund", "", "X", "", "", ""],
    ["Live viewing room", "", "X", "X", "X", "X"],
    ["Donate, my donations", "", "X", "", "", ""],
    ["Donation ticker and transparency feed", "X", "X", "X", "", ""],
    ["Order F&B, my order", "", "X", "", "", ""],
    ["File and look up a complaint", "X", "X", "X", "X", "X"],
    ["Venue setup, seating, tour, staff, bank accounts", "", "", "X", "", ""],
    ["Show creation, tiers, poster, legal declaration", "", "", "X", "", ""],
    ["Show control centre", "", "", "X", "Start/End only", ""],
    ["Livestream operations", "", "", "X", "X", ""],
    ["Donation acknowledgement and payout", "", "", "X", "", ""],
    ["Earnings, analytics, penalties and appeals", "", "", "X", "", ""],
    ["Walk-in sale, check-in scanner, order board", "", "", "X", "X", ""],
    ["Approval, moderation, refunds, penalties, complaints", "", "", "", "", "X"],
    ["Users, taxonomy, plans, statistics, jobs", "", "", "", "", "X"],
], widths=[2.5, 0.6, 0.75, 0.65, 0.9, 0.6])

a = r.heading("3.1.4 Non-Screen Functions")
a = r.add_after(a,
    f"{S['job_classes']} functions run outside any request and without a user identity — "
    f"{S['recurring_jobs']} on a recurring schedule and the remainder enqueued by an action that "
    "must not block the caller.")
r.add_table(a, ["#", "Area", "Function", "Description"], [
    ["1", "Ticketing", "Release expired holds", "Return reserved capacity to sale when a hold's countdown lapses unpaid."],
    ["2", "Ticketing", "Expire ticket transfers", "Cancel a pending peer-to-peer transfer that is never accepted."],
    ["3", "Payment", "Cancel abandoned payments", "Close payment attempts the customer never completed."],
    ["4", "Payment", "Reconcile with the gateway", "Compare platform records against the gateway's own to catch a missed notification."],
    ["5", "Payment", "Release due settlements", "Pay an owner's share on the configured two-tranche schedule."],
    ["6", "Donation", "Auto-confirm donations", "Confirm receipt on the owner's behalf once the acknowledgement window passes."],
    ["7", "Donation", "Expire stuck donations", "Close donations that never cleared payment."],
    ["8", "Donation", "Check overdue payouts", "Escalate donations acknowledged but not forwarded to the performer."],
    ["9", "Moderation", "Score content with AI", "Produce a risk score and recommendation for a submitted show or livestream."],
    ["10", "Moderation", "Alert on moderation SLA breach", "Warn before the review deadline is missed."],
    ["11", "Complaint", "Alert on complaint SLA breach", "Warn before a complaint response deadline is missed."],
    ["12", "Penalty", "Apply due penalties", "Bring a scheduled suspension or ban into effect."],
    ["13", "Penalty", "Auto-approve overdue appeals", "Overturn an appeal left unreviewed past its deadline."],
    ["14", "Subscription", "Warn of subscription expiry", "Notify an owner before a plan lapses."],
    ["15", "Subscription", "Expire subscriptions", "Deactivate a plan once its term ends."],
    ["16", "Show", "Send event reminders", "Remind ticket holders ahead of a show."],
    ["17", "Recommendation", "Recompute user event scores", "Refresh the interest matrix from recent behaviour."],
    ["18", "Recommendation", "Refresh recommendations", "Rebuild the personalised feed for active users."],
    ["19", "Recommendation", "Refresh one user's recommendations", "Rebuild a single feed after an explicit preference change."],
    ["20", "Recommendation", "Log user behaviour", "Record an interaction event for consenting users."],
    ["21", "Venue", "Stitch a tour scene", "Assemble uploaded photographs into one panorama via the imaging service."],
    ["22", "Finance", "Check ledger integrity", "Verify that every journal balances."],
    ["23", "Security", "Detect login spikes", "Alert on a burst of failed sign-ins from one source."],
    ["24", "Security", "Detect admin drift", "Alert if an administrator account appears outside the known set."],
    ["25", "Notification", "Prune stale device tokens", "Remove push registrations that persistently fail."],
    ["26", "Notification", "Alert on push failures", "Escalate a systemic push-delivery problem."],
    ["27", "Auth", "Send email verification code", "Deliver the sign-up one-time code."],
    ["28", "Auth", "Send phone verification code", "Deliver the phone one-time code by SMS."],
    ["29", "Auth", "Send password reset email", "Deliver a reset link."],
    ["30", "Auth", "Send duplicate registration alert", "Tell an existing account that someone tried to register with its address."],
], widths=[0.45, 1.0, 1.65, 3.1])

a = r.heading("3.1.5 Entity Relationship Diagram")
a = r.add_after(a,
    f"The data model has {S['entities']} entities. Figure 4 shows the core cross-domain entities and "
    "their cardinalities in crow's-foot notation; the remainder are per-domain detail tables "
    "(taxonomy tags, join tables, logs and configuration) that hang off these.")
a = r.add_figure(a, os.path.join(DIA, "erd-core.png"),
                 "Core entity relationship diagram (crow's-foot notation)")

# §3.2 Detailed function design
a = r.heading("3.2 <<Feature Name 1>>")
a.runs[0].text = "3.2 Detailed Function Design"
for extra in a.runs[1:]:
    extra._element.getparent().remove(extra._element)
a = r.add_after(a,
    "Documenting all "
    f"{S['screens_distinct']} screens at full depth would not add proportional value here, so the "
    "features below were chosen to cover the value chain end to end — account, venue, show, ticket, "
    "livestream and donation, payment, moderation, complaint. Each gives the trigger, the actors and "
    "purpose, and the business rules that govern it. The remaining screens are specified by purpose, "
    "content and actions in §3.1.2 and by access in §3.1.3.")

FUNCS = [
    ("Register and verify an account",
     "The visitor submits the sign-up form on any surface.",
     "Creates an account in the chosen role — only Audience and Owner may self-register — and "
     "enqueues a one-time code by email. The account cannot sign in until the code is confirmed.",
     ["The password is hashed and never stored or logged in clear text.",
      "A duplicate email address is rejected at registration rather than silently merged.",
      "A wrong or expired code reports a specific error; a correct code activates the account.",
      "Resending the code is rate-limited so the address cannot be used to flood a target inbox."]),
    ("Sign in",
     "The user submits email and password, or completes Google sign-in.",
     "Validates the credentials and then the account state in order — email verified, not locked, "
     "active, not erased — before issuing a session token carrying the role and, for Staff, the "
     "venue they are assigned to.",
     ["A wrong email and a wrong password return the same generic message, so the response cannot be used to discover which addresses are registered.",
      "Repeated failures increment a counter and, past a configured threshold, lock the account for a cool-down window.",
      "A Staff account whose assignment was deactivated still signs in but carries no venue, so every venue-scoped action then fails authorisation."]),
    ("Register a venue and have it approved",
     "The Owner submits the venue form; an Admin later opens the approval queue.",
     "Creates the venue in a pending state. The Owner may edit it freely, but it is not publicly "
     "listed and no show at it may be published until an Admin approves it.",
     ["A business licence document must be uploaded before the venue can be reviewed.",
      "Approval is the single gate that unblocks public listing and show publication.",
      "A rejected venue has no resubmit path by design: the Owner creates a new record, so the rejection stays attached to an immutable one."]),
    ("Create and submit a show for review",
     "The Owner completes the show form and chooses Submit for Review.",
     "Validates that the venue is approved and that at least one ticket tier and the legal permit "
     "reference exist, then moves the show from draft into the moderation queue.",
     ["An Owner without an active subscription is blocked at submission with an explanatory message rather than a late failure.",
      "Total tier capacity may not exceed the subscription plan's per-show ticket limit.",
      "The performance permit reference is required before submission."]),
    ("Hold and pay for a ticket",
     "The Audience member selects a tier and quantity on Select Tickets.",
     "Creates a hold against a specific pricing window that expires after a configured window "
     "(15 minutes by default), decrementing available capacity for its duration, then redirects to "
     "the payment gateway.",
     ["Capacity is decremented when the hold is taken, not when payment completes, so a seat cannot be sold twice during the payment window.",
      "An expired unpaid hold is released automatically by a background job; no user action is needed.",
      "The gateway's server-to-server notification is the authoritative confirmation, so a ticket still confirms if the customer closes the browser."]),
    ("Check in a ticket at the door",
     "Staff scan the ticket's code in the check-in scanner.",
     "Resolves the code to a ticket, evaluates eligibility, and shows a preview before Staff "
     "explicitly confirm, so a mis-scan does not consume the ticket.",
     ["A ticket is valid for the whole evening rather than a fixed time window, so there is no too-early or too-late rejection.",
      "Each refusal reason is reported distinctly: already used, wrong show, online-only ticket at a physical door, or frozen mid-transfer.",
      "There is no offline fallback — a dropped connection fails visibly rather than appearing to succeed."]),
    ("Watch a gated livestream",
     "The Audience member opens the viewing room for a show that is running.",
     "Evaluates the caller before the stream is served: a confirmed ticket for this show, within the "
     "concurrent-device limit; or an operator of the venue; or an Admin.",
     ["A refused connection states which condition failed rather than showing a blank player.",
      "A transient network drop retries quietly and must not be presented as an access refusal.",
      "The viewer count is a manual-refresh figure, not a live counter."]),
    ("Donate to a performer",
     "The Audience member opens Donate on a performer in the current line-up.",
     "Only performers whose slot accepts donations are offered. On payment confirmation the platform "
     "commission and tax are deducted and posted to the ledger, and the performer's share of the "
     "original gross is frozen onto the donation so a later configuration change cannot alter what "
     "was promised.",
     ["The gross splits four ways at confirmation: platform commission, tax, the performer's share, and the remainder retained by the venue for handling the payout.",
      "Status then advances from awaiting owner acknowledgement, to acknowledged, to performer paid — a multi-day pipeline the interface must represent honestly.",
      "Anonymity, amount visibility and message publication are three independent choices captured at donation time.",
      "The public transparency feed shows only acknowledged and paid donations, since an unacknowledged one can still be cancelled or refunded."]),
    ("Process a payment and post the ledger",
     "The gateway calls back after a payment attempt, and again on retry.",
     "Verifies the callback signature and the amount before doing anything, then confirms the "
     "payment and writes a balanced double-entry journal split into gross, gateway fee, platform "
     "fee, tax withheld and net.",
     ["The handler is idempotent: the gateway retries, and a repeated callback must not post a second journal.",
      "A callback whose amount does not match the expected total is refused rather than trusted.",
      "Every journal must balance — total debits equal total credits — and a scheduled job re-verifies this."]),
    ("Moderate a show or livestream",
     "An Owner submits content for review; an Admin opens the moderation queue.",
     "An AI risk score and recommendation are produced as advice only; the Admin decides. Approval "
     "is what makes a show public, and what unlocks the broadcast control for a livestream.",
     ["The AI result never auto-approves or auto-rejects; a human decision is always required.",
      "The review deadline is 24 hours, with an internal warning raised before it is missed.",
      "If the AI service is unavailable the item still enters the queue with a neutral score rather than blocking submission."]),
    ("File and resolve a complaint",
     "Anyone, including a guest, submits the complaint form; an Admin resolves it from the queue.",
     "A signed-in complainant is linked to their account; a guest instead supplies a contact phone "
     "number and receives a reference number, which is the only way they can retrieve the case later.",
     ["A guest's reference number is shown once at submission and must be presented prominently, because it cannot be recovered from an account.",
      "Resolving with a refund against a ticket creates a refund request that still requires its own approval.",
      "Taking down a show cancels it outright and refunds every confirmed ticket — the heaviest available outcome.",
      "A guest complainant is notified of the outcome by SMS, since there is no in-app channel to reach them."]),
]
for title, trigger, desc, rules in FUNCS:
    a = r.add_after(a, title, "Heading 4")
    a = r.add_after(a, f"Trigger: {trigger}")
    a = r.add_after(a, f"Description: {desc}")
    a = r.add_after(a, "Business rules and validation:")
    a = r.add_bullets(a, rules)

# §4 Non-functional
a = r.heading("4.1 External Interfaces")
a = r.add_after(a,
    "The platform integrates the services below, plus the cloud services it is deployed onto "
    "(Report 4 §1.4). Each integration's failure behaviour is stated, because how a dependency "
    "fails matters as much as what it does.")
r.add_table(a, ["System", "Role", "Notes"],
            [[n, role, note] for n, role, note in F.EXTERNAL], widths=[1.25, 1.15, 3.8])

a = r.heading("4.2 Quality Attributes")
for sub, bullets in [
    ("Usability", [
        "Every conditional action is shown or hidden according to real eligibility rather than shown and then refused, so a user is never invited to do something that will fail.",
        "Destructive or financially consequential actions require a confirmation proportional to their blast radius, not a single generic prompt.",
        "Empty, loading and error states are visually distinct from one another on every screen — three different conditions, three different presentations.",
        "Supported browsers are the current and previous major versions of Chrome, Edge, Firefox and Safari; the mobile applications support Android 10 and iOS 15 upward.",
        "The Audience Website is responsive across phone, tablet and desktop widths; the Owner Dashboard and Admin Console are desktop-first and assume at least 1024px.",
        "All three web surfaces meet WCAG 2.2 Level AA: keyboard reachable and operable with a visible focus indicator, labelled fields with errors announced to assistive technology, and text contrast of at least 4.5:1.",
        "No status is conveyed by colour alone; every state carries a text label or icon so it survives a colour-blind reader and a black-and-white printout.",
    ]),
    ("Reliability", [
        "Content moderation is reviewed within 24 hours, with an internal warning raised before the deadline lapses.",
        "A penalty appeal is reviewed within 48 hours; an appeal left unreviewed is overturned automatically rather than left stuck.",
        "A donation not acknowledged by the owner within 24 hours is auto-confirmed on their behalf.",
        "An acknowledged donation not forwarded to the performer raises a notification at 7 days and a venue penalty at 14 days.",
        "Every threshold above is configuration-driven rather than compiled in, so tuning one is an operational change and not a release.",
        "Door check-in and counter sales have no offline fallback by explicit decision: a dropped connection fails visibly rather than appearing to succeed.",
    ]),
    ("Performance", [
        "Ticket holds and seating-zone capacity are the most concurrency-sensitive paths in the system: a venue's real occupancy limit must never be oversold under concurrent load.",
        "The load shape to design for is a ticket on-sale spike — many simultaneous hold requests against one tier the moment sales open — combined with a concurrent livestream audience and its chat traffic.",
        "API response time targets under normal load: 95th percentile under 500 ms for reads and under 1 second for writes.",
        "Frontend budget on a mid-range device over a 4G connection: largest contentful paint under 2.5 seconds, interaction to next paint under 200 ms, and an initial bundle under 250 KB compressed per surface.",
        "Long-running work never blocks a request: stitching, poster generation, moderation scoring and settlement all run as background functions, and the screen that triggered them shows an explicit processing state.",
    ]),
    ("Security", [
        "Authorisation is checked in three layers on every request that touches one venue's data: role from the token, then policy, then venue scoping.",
        "There is deliberately no interface to create or promote an administrator; every such account is provisioned directly and a scheduled function alerts if one appears outside the known set.",
        "Sign-in responses never reveal which of email or password was wrong, and password-reset requests never reveal whether an address is registered.",
        "Repeated failed sign-ins are throttled by counter and lockout, and a burst from one source is separately detected as a credential-stuffing signal.",
        "Identity documents and identifiers are served only through protected, non-guessable endpoints and are stored encrypted, never as a public URL.",
    ]),
    ("Compliance", [f"{area} — {basis}: {how}" for area, basis, how in F.LEGAL]),
    ("Maintainability", [
        "The backend follows a layered architecture with a strict inward dependency rule enforced at the project-reference level, so a violation fails the build rather than passing review.",
        "Every business-tunable number — deadlines, hold duration, commission and tax rates, settlement thresholds — lives in configuration rather than in code, so operational tuning does not require a release.",
        "All five client surfaces share one API, one design system and one generated API client, so a contract change surfaces as a compile error in every consumer rather than a runtime failure in one.",
        "Each tier is deployed from source by the same pipeline, with environment configuration and secrets supplied at deploy time, so the artefact tested in staging is the one promoted to production.",
    ]),
]:
    a = r.add_after(a, f"4.2.{['Usability','Reliability','Performance','Security','Compliance','Maintainability'].index(sub)+1} {sub}", "Heading 4")
    a = r.add_bullets(a, bullets)

# §5 Appendix
a = r.heading("5.1 Business Rules")
RULES = [
    "A venue starts pending and cannot be publicly listed, nor publish any show, until an Admin approves it.",
    "A seating zone's capacity is a real physical occupancy limit and must never be oversold, including under concurrent load.",
    "A ticket is valid for the whole evening of its show rather than a fixed time window.",
    "Walk-in sales may continue after a show has started.",
    "A ticket hold reserves capacity for a configured window and is released automatically if unpaid.",
    "Capacity is decremented when a hold is taken, not when payment completes.",
    "The payment gateway's server-to-server notification is the authoritative confirmation of payment.",
    "Every confirmed payment writes a balanced double-entry journal; total debits must equal total credits.",
    "A donation's gross splits at confirmation into platform commission, tax, the performer's share of the original gross, and the remainder retained by the venue for handling the payout.",
    "The performer's share rate is frozen onto the donation at confirmation, so a later configuration change cannot alter what an existing donation promised.",
    "The public transparency feed shows only acknowledged and paid donations.",
    "An owner must hold an active subscription to submit a show for review.",
    "Total ticket capacity for a show may not exceed the subscription plan's per-show limit.",
    "A performance permit reference is required before a show may be submitted.",
    "Every show and livestream requires a human Admin decision before becoming public; an AI score is advice only.",
    "Content moderation must be decided within 24 hours of submission.",
    "A penalty appeal not reviewed within 48 hours is overturned automatically.",
    "A donation not acknowledged within 24 hours is auto-confirmed on the owner's behalf.",
    "A Staff account may be actively assigned to exactly one venue at a time.",
    "A Staff session is scoped to that one venue for its whole duration.",
    "There is no self-service path to create or promote an administrator account.",
    "A rejected venue cannot be amended and resubmitted; a new record must be created.",
    "Cancelling a show refunds every confirmed ticket for it.",
    "Changing a show from in-person to online is one-way and refunds every confirmed in-person ticket.",
    "A refund is never paid automatically; every refund request requires an Admin decision.",
    "A payout bank account must be verified by an Admin before any settlement is released to it.",
    "Settlement is released in two tranches on a configured schedule rather than in one payment.",
    "A guest complaint is retrievable only by its reference number together with the contact phone number given.",
    "Personal data erasure anonymises the account in place and never hard-deletes records the law requires to be retained.",
    "Behaviour logging and personalised recommendations are off unless the user opts in.",
]
assert len(RULES) == S["business_rules"], f"{len(RULES)} rules but facts says {S['business_rules']}"
r.add_table(a, ["ID", "Rule Definition"],
            [[f"BR-{i:02d}", t] for i, t in enumerate(RULES, 1)], widths=[0.75, 5.45])

a = r.heading("5.2 Common Requirements")
r.add_bullets(a, [
    "Every list endpoint accepts paging parameters and clamps the page size to a maximum, so a client cannot request an unbounded result set.",
    "Every error response uses one shape across the whole API — a success flag, a human-readable message and an optional field-level error map — so clients need only one error path.",
    "Every message shown to a user is in Vietnamese and phrased in plain language, without internal identifiers or stack detail.",
    "Every monetary value is stored and calculated in a decimal type, never a floating-point one, and rounded consistently at the same point in every calculation.",
    "Every timestamp is stored with its offset and rendered in the venue's local time.",
    "Every uploaded file is served through an endpoint that checks authorisation, never as a directly guessable public URL.",
])

a = r.heading("5.3 Application Messages List")
r.add_table(a, ["#", "Code", "Type", "Context", "Content"], [
    ["1", "MSG-01", "Inline, form", "Wrong email or password at sign-in", "Email hoặc mật khẩu không đúng. Vui lòng kiểm tra lại."],
    ["2", "MSG-02", "Inline banner", "Password reset requested", "Nếu email đã đăng ký, liên kết đặt lại mật khẩu sẽ được gửi tới hộp thư của bạn."],
    ["3", "MSG-03", "Inline, form", "Verification code wrong or expired", "Mã xác thực không đúng hoặc đã hết hạn. Vui lòng gửi lại mã."],
    ["4", "MSG-04", "Toast", "Ticket hold started", "Đã giữ vé cho bạn trong 15 phút. Vui lòng hoàn tất thanh toán."],
    ["5", "MSG-05", "Toast", "Ticket hold expired", "Đã hết thời gian giữ vé. Vui lòng chọn lại."],
    ["6", "MSG-06", "Full screen", "Payment succeeded", "Thanh toán thành công. Vé của bạn đã sẵn sàng."],
    ["7", "MSG-07", "Full screen", "Payment failed", "Thanh toán không thành công. Chưa có khoản tiền nào bị trừ."],
    ["8", "MSG-08", "Inline banner", "Show cannot be submitted", "Chưa thể gửi duyệt: cần venue đã được duyệt, gói dịch vụ còn hiệu lực, ít nhất một hạng vé và khai báo giấy phép."],
    ["9", "MSG-09", "Scanner result", "Check-in accepted", "Vé hợp lệ. Mời khách vào."],
    ["10", "MSG-10", "Scanner result", "Check-in refused", "Vé không hợp lệ: {lý do cụ thể}."],
    ["11", "MSG-11", "Player overlay", "Livestream access refused", "Bạn cần vé hợp lệ cho buổi diễn này để xem, hoặc đã vượt số thiết bị cho phép."],
    ["12", "MSG-12", "Modal", "Complaint submitted by a guest", "Đã ghi nhận khiếu nại. Mã tra cứu của bạn là {mã}. Vui lòng lưu lại — đây là cách duy nhất để tra cứu."],
    ["13", "MSG-13", "Confirmation", "Erase my data", "Thao tác này không thể hoàn tác. Tài khoản sẽ bị ẩn danh vĩnh viễn và bạn sẽ bị đăng xuất khỏi mọi thiết bị."],
], widths=[0.4, 0.7, 0.95, 1.65, 2.5])

path = r.save()
print(f"built {path}")
