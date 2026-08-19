"""Single source of truth for every figure that appears in more than one report.

Nothing in the report builders may hard-code a count. If a number is wrong it is wrong
here, once, and every report changes together — which is what stops the reports drifting
apart from each other the way they did before.

Scope note: these reports document the COMPLETE MusicLounge platform (five client
surfaces + backend API + imaging microservice + Azure deployment) as delivered, not the
backend repository alone.
"""

# ── project identity ─────────────────────────────────────────────────────────
PROJECT = dict(
    name="MusicLounge — Live-Music Venue Ticketing, Livestream & Donation Platform",
    code="SU26SE039",
    group="GSU26SE68",
    software_type=("Web + Native Mobile, over one shared REST/SignalR API — five client surfaces "
                   "(Audience Website, Audience Mobile F&B app, Owner Web Dashboard, Staff Mobile "
                   "app, Admin Web Console)"),
)

SUPERVISOR = dict(name="Nguyễn Trọng Tài", role="Supervisor", email="taint@fpt.edu.vn",
                  mobile="<<mobile>>")

TEAM = [
    dict(name="Hà Đông Hải", role="Leader", email="<<email>>", mobile="<<mobile>>", short="Hải"),
    dict(name="Huỳnh Trung Nhiên", role="Member", email="<<email>>", mobile="<<mobile>>", short="Nhiên"),
    dict(name="Nguyễn Quang Anh Khoa", role="Member", email="<<email>>", mobile="<<mobile>>", short="Khoa"),
    dict(name="Nguyễn Huy Phúc", role="Member", email="<<email>>", mobile="<<mobile>>", short="Phúc"),
]

TIMELINE = dict(start="20/05/2026", end="31/08/2026", calendar_days=101, weeks=15)
DOC_DATE = "16/08/2026"

# ── platform scale (measured from the delivered system) ──────────────────────
SCALE = dict(
    surfaces=5,
    # A screen shared by two surfaces is built once but listed under each surface that
    # uses it, so the per-surface figures sum to more than the distinct total. The seven
    # account screens (Notifications, Profile, Edit Profile, ID Verification, AI
    # Preferences, Privacy & Data, My Complaints) are shared by Audience Web and Owner
    # Web: 82 listed − 7 shared = 75 distinct.
    screens_distinct=75,
    screens_shared=7,
    screens_by_surface={
        "Audience — Website": 30,
        "Audience — Mobile (F&B)": 2,
        "Owner — Web Dashboard": 27,
        "Staff — Mobile": 7,
        "Admin — Web Console": 16,
    },
    entities=68,
    controllers=25,
    endpoints=209,
    job_classes=30,
    recurring_jobs=22,
    feature_folders=28,
    business_rules=30,
    actors=6,
    login_roles=4,
    effort_man_days=488,
)
# Derived, never typed: the use-case count comes from the catalogue itself, so it can
# never disagree with the table that lists them.
import usecases as _uc
SCALE["use_cases"] = _uc.TOTAL

SCALE["screens_listed"] = sum(SCALE["screens_by_surface"].values())
assert SCALE["screens_listed"] - SCALE["screens_shared"] == SCALE["screens_distinct"], (
    f'{SCALE["screens_listed"]} listed - {SCALE["screens_shared"]} shared '
    f'!= {SCALE["screens_distinct"]} distinct')

# ── test results (measured) ──────────────────────────────────────────────────
TESTS = dict(
    backend_suites=61, backend_tests=460, backend_passed=456, backend_failed=4,
    frontend_suites=100, frontend_tests=307,
    e2e_suites=1, e2e_tests=28,
)
TESTS["total_suites"] = TESTS["backend_suites"] + TESTS["frontend_suites"] + TESTS["e2e_suites"]
TESTS["total_tests"] = TESTS["backend_tests"] + TESTS["frontend_tests"] + TESTS["e2e_tests"]
TESTS["total_failed"] = TESTS["backend_failed"]
TESTS["total_passed"] = TESTS["total_tests"] - TESTS["total_failed"]
TESTS["pass_rate"] = f"{TESTS['total_passed'] / TESTS['total_tests'] * 100:.1f}%"

# ── technology ───────────────────────────────────────────────────────────────
STACK = dict(
    backend="ASP.NET Core 8 (C#), Clean Architecture, MediatR (CQRS), FluentValidation, EF Core 8",
    database="SQL Server 2022 / Azure SQL Database",
    jobs="Hangfire",
    realtime="SignalR",
    web_frontend="React 18 + TypeScript 5 + Vite 5 + Tailwind CSS 3, TanStack Query, React Router",
    mobile_frontend="React Native + TypeScript, React Navigation",
    microservice="Python 3.12 + OpenCV + Hugin (panorama stitching)",
    cloud="Microsoft Azure",
)

AZURE = [
    ("Azure App Service (Linux)", "Hosts the ASP.NET Core API, SignalR hubs and the Hangfire worker; a staging slot is swapped into production on release."),
    ("Azure SQL Database", "Transactional data and the Hangfire job store, with automated backup and point-in-time restore."),
    ("Azure Container Apps", "Runs the containerised panorama-stitching microservice, scaled independently of the API."),
    ("Azure Static Web Apps", "Serves the three React web bundles from the edge."),
    ("Azure Blob Storage + CDN", "Stores and delivers uploaded images, business licences, posters and 360° panoramas."),
    ("Azure Key Vault", "Holds every secret; injected at deploy time, never committed or baked into a build."),
    ("Application Insights + Azure Monitor", "Request traces, dependency timings, background-job outcomes and alert rules."),
    ("GitHub Actions", "Builds, tests and deploys each component; the artefact tested in staging is the one promoted."),
]

EXTERNAL = [
    ("VNPay", "Payment Gateway", "The sole payment intermediary for tickets, donations and subscriptions. The platform never handles card data — the customer is redirected to VNPay and an independent server-to-server IPN callback is the authoritative confirmation."),
    ("Mux / Cloudflare Stream", "Live Video", "RTMP ingest for broadcasting and HLS playback for viewers, behind one provider factory so either can be selected per environment."),
    ("Firebase Cloud Messaging", "Push Notification", "Delivers push notifications to the two mobile applications; a persistently failing device token is pruned automatically."),
    ("Google OAuth", "Identity", "Social sign-in alternative to local email and password accounts."),
    ("Google Maps", "Geolocation", "Venue address display and map-based location picking during venue setup."),
    ("Twilio", "SMS Gateway", "OTP and notification SMS through Twilio Programmable Messaging. Vietnamese carriers reject long-code SMS, and an Alphanumeric Sender ID takes weeks to register, so the capstone demonstration sends between international numbers; the number is configuration, not code."),
    ("Gemini", "AI Services", "Moderation risk scoring, show-poster generation and recommendation feature scoring. Every call degrades gracefully to a neutral result rather than blocking the request."),
    ("OpenAI", "AI Services (alternate)", "Alternate poster-image provider behind the same interface as Gemini."),
]

# ── actors ───────────────────────────────────────────────────────────────────
ACTORS = [
    ("Guest", "An unauthenticated visitor. Can browse published shows and venues, explore a 360° tour, and file a complaint with a reference number — but cannot buy, watch or donate."),
    ("Audience", "A registered customer. Discovers shows, buys and manages tickets, watches gated livestreams, tips performers, orders food and drink in-venue, rates shows and files complaints."),
    ("Owner", "Runs one or more venues. Registers the venue, manages seating zones and the 360° tour, creates and operates shows, handles donations and views earnings. Pays a platform subscription."),
    ("Staff", "Assigned by an Owner to exactly one venue. Sells walk-in tickets at the counter, checks tickets at the door, works the F&B order board and starts or ends a broadcast."),
    ("Admin", "Platform operator. Approves venues, moderates shows and livestreams, processes refunds, verifies bank accounts, issues penalties, resolves complaints and monitors the ledger. Provisioned directly in the database — there is deliberately no self-service path."),
    ("Performer", "Appears in a show's line-up and receives donations. Has a public profile and a payout bank account but no login of its own — an Owner maintains the record on their behalf."),
]

# ── legal basis ──────────────────────────────────────────────────────────────
LEGAL = [
    ("Payment intermediation", "Decree 52/2024", "All payment flows go through the licensed intermediary VNPay; the platform never touches card data."),
    ("Withholding tax", "Decree 117/2025", "Tax withheld on qualifying payments is tracked per transaction and posted to a dedicated tax ledger account."),
    ("Content takedown & phone verification", "Decree 147/2024", "A 24-hour moderation SLA with an internal warning at ~20 hours, and a verified-phone field on the user record."),
    ("Mandatory complaint channel", "Decree 85/2021", "The complaint entity and its guest-accessible filing and lookup path exist to satisfy this obligation."),
    ("Personal data protection", "Law 91/2025/QH15", "AI consent defaults to off, personal data can be exported and erased on request, and erasure anonymises in place rather than hard-deleting, so legally-retained financial records survive."),
    ("Performance licensing and music copyright", "Decree 144/2020 · VCPMC", "A performance-permit reference and, where applicable, a royalty reference are required before a show may be submitted for review."),
]

# ── recurring wording ────────────────────────────────────────────────────────
PRODUCT_ONE_LINER = ("MusicLounge is a ticketing, livestream and donation platform built for small "
                     "live-music venues in Vietnam.")


# ── work breakdown structure ─────────────────────────────────────────────────
# Effort covers the whole platform, not the backend alone. Group rows carry no
# estimate of their own; only leaf rows do, and the total is computed, never typed.
WBS = [
    ("1", "Auth & Account Management", None, None),
    ("1.1", "Register / verify / login, Google OAuth, JWT + policy-based authorization", "Complex", 12),
    ("1.2", "Forgot & reset password, profile edit, identity-document upload", "Medium", 6),
    ("1.3", "Personal-data export and erasure (anonymise in place)", "Medium", 5),
    ("2", "Venue Management", None, None),
    ("2.1", "Venue CRUD, business-licence and photo upload", "Medium", 6),
    ("2.2", "Seating-zone drag-and-drop editor (2D/3D)", "Complex", 10),
    ("2.3", "360° virtual tour: scene and hotspot editor, auto-stitch integration", "Complex", 14),
    ("2.4", "Staff assignment, venue gallery, custom criteria", "Medium", 7),
    ("3", "Venue Approval & Content Moderation", None, None),
    ("3.1", "Venue approve / reject queue", "Simple", 3),
    ("3.2", "AI-assisted show and livestream moderation queue", "Complex", 10),
    ("4", "Show Management", None, None),
    ("4.1", "Show CRUD and performer line-up builder", "Medium", 8),
    ("4.2", "Show lifecycle: submit, publish, reschedule, change format, cancel", "Complex", 10),
    ("4.3", "Ticket tiers, pricing windows, AI poster generation", "Medium", 8),
    ("4.4", "Legal permit and royalty declaration", "Simple", 2),
    ("5", "Ticketing", None, None),
    ("5.1", "Ticket hold (15 minutes) and quota management", "Medium", 6),
    ("5.2", "VNPay payment integration and QR generation", "Complex", 10),
    ("5.3", "Transfer, cancellation and refund-request workflow", "Medium", 8),
    ("5.4", "Walk-in sale and door check-in", "Medium", 7),
    ("6", "Livestream", None, None),
    ("6.1", "Provider integration (Mux / Cloudflare) and broadcast start / end", "Complex", 12),
    ("6.2", "Access-gated viewing room, chat, viewer count", "Complex", 12),
    ("6.3", "Concurrent-device session limiting", "Medium", 5),
    ("7", "Donation & Financial Ledger", None, None),
    ("7.1", "Donate flow, owner acknowledgement, performer payout", "Complex", 11),
    ("7.2", "Double-entry ledger and reconciliation job", "Complex", 12),
    ("7.3", "Two-tranche settlement, public donation ticker and feed", "Complex", 11),
    ("8", "F&B Ordering", None, None),
    ("8.1", "Menu CRUD and mobile ordering flow", "Medium", 6),
    ("8.2", "Staff order board with status progression", "Simple", 4),
    ("9", "Subscription & Billing", None, None),
    ("9.1", "Plan CRUD (Admin); subscribe, renew, cancel (Owner)", "Medium", 7),
    ("10", "Complaints & Venue Penalties", None, None),
    ("10.1", "Complaint filing, lookup and resolution, including the guest path", "Medium", 7),
    ("10.2", "Penalty issue, appeal and automatic overturn workflow", "Medium", 7),
    ("11", "Recommendation & Personalisation", None, None),
    ("11.1", "Behaviour logging, event scoring, hybrid recommender", "Complex", 12),
    ("11.2", "Follow, wishlist and rating", "Simple", 4),
    ("12", "Notifications", None, None),
    ("12.1", "Push integration, notification inbox, SMS integration", "Medium", 8),
    ("13", "Admin Back-Office", None, None),
    ("13.1", "User management, bank-account verification, ledger-integrity check", "Medium", 8),
    ("13.2", "Taxonomy and subscription-plan management, platform analytics", "Medium", 6),
    ("14", "Backend Cross-Cutting Work", None, None),
    ("14.1", "Clean Architecture scaffold, CQRS pipeline, repository and unit of work", "Complex", 10),
    ("14.2", "Background jobs for SLA, expiry, settlement and security monitoring", "Complex", 14),
    ("14.3", "Security hardening and legal/compliance mapping", "Complex", 12),
    ("14.4", "Backend test suite", "Complex", 15),
    ("15", "Frontend Implementation", None, None),
    ("15.1", "Auth and account screens across all surfaces", "Medium", 10),
    ("15.2", "Venue setup, seating-zone editor, 360° tour builder", "Complex", 18),
    ("15.3", "Venue approval and moderation review queues", "Medium", 6),
    ("15.4", "Show wizard, line-up builder, ticket tier and poster screens", "Complex", 10),
    ("15.5", "Browse, purchase, hold countdown, QR ticket, walk-in point of sale", "Complex", 14),
    ("15.6", "Livestream viewing room, chat and broadcast control", "Complex", 12),
    ("15.7", "Donation widget, public ticker, payout and ledger views", "Medium", 9),
    ("15.8", "F&B ordering app and staff order board", "Medium", 8),
    ("15.9", "Subscription plan and billing screens", "Medium", 5),
    ("15.10", "Complaint and penalty screens", "Medium", 6),
    ("15.11", "Recommendation feed, follow, wishlist and rating", "Medium", 5),
    ("15.12", "Notification inbox and push permission handling", "Simple", 4),
    ("15.13", "Admin back-office screens and analytics dashboard", "Medium", 10),
    ("15.14", "Design system, routing and auth guards, API client, responsive behaviour", "Complex", 12),
    ("16", "UI/UX Design", None, None),
    ("16.1", "Design system and style guide", "Medium", 4),
    ("16.2", "Per-surface design briefs, view catalogue and screen-flow diagrams", "Complex", 8),
    ("16.3", "High-fidelity mock-ups per view", "Complex", 10),
    ("17", "Deployment & Hosting", None, None),
    ("17.1", "Azure provisioning, CI/CD pipeline, domain and TLS", "Medium", 6),
    ("18", "Documentation", None, None),
    ("18.1", "Domain and architecture documentation", "Medium", 6),
    ("18.2", "SEP490 report set", "Complex", 10),
]
WBS_TOTAL = sum(row[3] for row in WBS if row[3])
assert WBS_TOTAL == SCALE["effort_man_days"], (
    f"WBS rows sum to {WBS_TOTAL} but SCALE says {SCALE['effort_man_days']}")
