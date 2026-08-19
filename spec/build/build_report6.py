"""Report 6 — Software User Guides, built fresh from the pristine template."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from docxkit import Report
import facts as F

S = F.SCALE
STEP_HDR = ["Step", "Screen", "What you do", "What you should see"]
STEP_W = [1.25, 1.25, 1.9, 1.8]

r = Report("Report6_Software User Guides.docx",
           "Report6_Software User Guides - MusicLounge.docx")

# ── PASS 1 ───────────────────────────────────────────────────────────────────
r.clear_regions(
    ("1. Deliverable Package", "2. Installation Guides"),
    ("2. Installation Guides", "2.1 System Requirements"),
    ("2.1 System Requirements", "2.2 Installation Instruction"),
    ("2.2 Installation Instruction", "3. User Manual"),
    ("3. User Manual", "3.1 Overview"),
    ("3.1 Overview", "3.2 Workflow 1"),
    ("3.2 Workflow 1", "3.3 Workflow 2"),
    ("3.3 Workflow 2", None),
)

# ── PASS 2 ───────────────────────────────────────────────────────────────────
r.record_of_changes([
    [F.DOC_DATE, "A", F.TEAM[0]["name"],
     f"User guide for the complete platform: all {S['surfaces']} client applications, the backend "
     f"API and the imaging microservice, with installation covering the deployed environment, a "
     f"fresh cloud deployment, and a local development setup."],
])

# §1 Deliverable Package
a = r.heading("1. Deliverable Package")
r.add_table(a, ["No.", "Deliverable Item", "Description"], [
    ["1", "Backend API — source code",
     "The four-project solution exposing the REST and real-time API that every client consumes."],
    ["2", "Audience Website — source code",
     f"Web application covering discovery, ticket purchase, livestream viewing, donations, ratings and complaints ({S['screens_by_surface']['Audience — Website']} screens)."],
    ["3", "Owner Web Dashboard — source code",
     f"Web application covering venue setup, the seating-zone editor, the 360° tour builder, show management, donation handling and finance ({S['screens_by_surface']['Owner — Web Dashboard']} screens)."],
    ["4", "Admin Web Console — source code",
     f"Web application covering venue approval, content moderation, refunds, penalties, complaints, users, taxonomy and analytics ({S['screens_by_surface']['Admin — Web Console']} screens)."],
    ["5", "Staff Mobile application — source code",
     f"Mobile application for on-floor operations: counter sales, door check-in and the F&B order board ({S['screens_by_surface']['Staff — Mobile']} screens). Supplied as signed Android and iOS review builds."],
    ["6", "Audience Mobile F&B application — source code",
     f"Mobile application for in-venue food and drink ordering ({S['screens_by_surface']['Audience — Mobile (F&B)']} screens). Supplied as signed review builds."],
    ["7", "Panorama-stitching microservice — source code",
     "Containerised imaging service that assembles several venue photographs into one 360° panorama."],
    ["8", "Database migration scripts",
     "Code-First migrations that create and version the whole schema; there is no hand-written schema script."],
    ["9", "Configuration templates",
     "Example settings files for the API and example environment files for each client, listing every value that must be supplied per environment."],
    ["10", "Deployment configuration",
     "Pipeline definitions that build, test and deploy each component, plus the cloud resources each expects."],
    ["11", "Deployed platform",
     "The running system: the three web addresses, the API address, and the installable mobile builds. One demo account per role is supplied separately to the review committee."],
    ["12", "API reference",
     "Interactive OpenAPI documentation served by the running API itself."],
    ["13", "Reports 1–7", "The full document set, including this guide."],
], widths=[0.5, 1.85, 3.85])

# §2.1 System Requirements
a = r.heading("2.1 System Requirements")
r.add_table(a, ["Audience", "Requirement"], [
    ["End user — web",
     "Any current or previous major version of Chrome, Edge, Firefox or Safari with JavaScript enabled. The Audience Website works from phone width upward; the Owner Dashboard and Admin Console are desktop-first and expect a screen at least 1024 pixels wide."],
    ["End user — mobile",
     "Android 10 or later, or iOS 15 or later. Camera permission is required for the door check-in scanner; notification permission is optional but needed for push alerts."],
    ["End user — network",
     "A working internet connection. Door check-in and counter sales have no offline mode by design: a dropped connection blocks the action rather than silently queuing it."],
    ["Operator — cloud",
     "A cloud subscription able to host an application service, a managed SQL database, a container service, static web hosting, blob storage with a content delivery network, a secrets vault and an application-monitoring resource."],
    ["Operator — third-party accounts",
     "Payment gateway credentials (sandbox is sufficient for evaluation), livestream provider keys, a Google project for sign-in and maps, a push-messaging project, an SMS account for Vietnamese numbers, and an AI service key."],
    ["Developer — backend",
     ".NET 8 SDK (the SDK, not the runtime alone), the EF Core command-line tool for migrations, and a reachable SQL Server 2019 or later instance."],
    ["Developer — frontend",
     "Node.js 20 LTS with npm. The two mobile applications additionally need Android Studio with SDK 34 and, for iOS builds, Xcode 15 on macOS."],
    ["Developer — microservice",
     "Docker, so the imaging service can be built and run without installing its native image-processing libraries directly."],
], widths=[1.5, 4.7])

# §2.2 Installation Instruction
a = r.heading("2.2 Installation Instruction")
a = r.add_after(a, "Three routes are available depending on what you need to do.")

a = r.add_after(a, "Option A — use the deployed platform (recommended for reviewers)", "Heading 4")
a = r.add_after(a,
    "Nothing needs to be installed. Open the Audience Website, Owner Dashboard or Admin Console "
    "address supplied with this release in a supported browser and sign in with the demo account "
    "for the role you want to review. The two mobile applications are supplied as signed builds — "
    "install one on a device or emulator meeting the requirements above and sign in with the same "
    "credentials. Every workflow in §3 can be walked end to end on this environment.")

a = r.add_after(a, "Option B — deploy a fresh environment", "Heading 4")
a = r.add_paragraphs(a, [
    "1. Provision the cloud resources listed in §2.1: an application service with a staging slot, a "
    "managed SQL database, a container environment for the imaging service, static hosting for the "
    "three web bundles, a storage account with a delivery network, a secrets vault and a monitoring "
    "resource.",
    "2. Put every secret into the vault — the database connection string, gateway credentials, "
    "livestream, sign-in, push, SMS and AI keys — and grant the application service read access. No "
    "secret belongs in source control or in a settings file that ships with a build.",
    "3. Set each repository's deployment credentials and target resource names, then push to the "
    "main branch. The pipeline builds each component, runs its tests, deploys the API to the "
    "staging slot, runs the end-to-end suite against staging, and swaps staging into production. "
    "Database migrations are applied as part of the API deployment.",
    "4. Point each client's environment configuration at the deployed API address, and register that "
    "same address with the payment gateway as the return and callback URL — otherwise payment "
    "confirmations never reach the platform and tickets will stay unpaid.",
])

a = r.add_after(a, "Option C — run locally for development", "Heading 4")
a = r.add_paragraphs(a, [
    "1. Backend — copy the example settings file in the API project to a local settings file and "
    "set the database connection string. Windows Authentication against a local instance is the "
    "shipped default and usually needs no edit; for SQL Authentication supply a user and password "
    "instead, and for a named instance give the instance name (escaped, because the value sits "
    "inside JSON). The payment and livestream sections may be left blank — that disables only those "
    "two features and every other screen still works.",
    "2. Create the schema — from the repository root run the EF Core database-update command against "
    "the Infrastructure project with the API project as the startup project. It creates the database "
    "and every table at the current migration; a final success line with no error means it worked.",
    "3. Start the API — run the API project. When it reports the address it is listening on, the API "
    "is up; leave that terminal open. Verify by opening the interactive API documentation in a "
    "browser, or by requesting the health endpoint from another terminal.",
    "4. Start a client — in the chosen client project, copy the example environment file, set the "
    "API address to the local one, then install dependencies and run the development server. The "
    "web applications start on the port the backend already allows through its cross-origin policy. "
    "For the mobile applications, install dependencies and run the Android or iOS target.",
    "5. Start the imaging service — only needed to exercise the 360° auto-stitch feature. Bring up "
    "its container from the service folder, then set the imaging-service address in the API's local "
    "settings to the address it reports.",
])

a = r.add_after(a, "Troubleshooting", "Heading 4")
r.add_table(a, ["Symptom", "Cause and fix"], [
    ["A page loads but every action fails with a network error",
     "The client is pointed at the wrong API address, or the API is not running. Check the environment file, and confirm the API answers on its health endpoint."],
    ["The API will not start because the port is in use",
     "A previous run still holds it. Stop the other process, then start again."],
    ["Creating the schema fails with a login error",
     "Wrong credentials or the wrong authentication mode. Re-check the connection string and confirm the database service is running."],
    ["Signed up but no verification code arrived",
     "In a local run no real email is sent — the code is printed in the API's terminal output. In a deployed environment, check the mail settings and the recipient's spam folder."],
    ["A newly created show does not appear in public search",
     "Working as designed. A show must go from draft, to pending when the Owner submits it, to published when an Admin approves it — and its venue must already be approved."],
    ["The Owner cannot submit a show for review",
     "The venue is not yet approved, the subscription is not active, or the show has no ticket tier or permit reference. The screen names which prerequisite is missing."],
    ["Payment completes at the gateway but the ticket stays unpaid",
     "The gateway's server-to-server callback is not reaching the platform. Confirm the return and callback addresses registered with the gateway match the deployed API and are reachable from the internet."],
    ["Cannot sign in to the Admin Console",
     "Correct — there is no self-service path to become an Admin. The account is provisioned directly by an operator; see §3.3."],
    ["The 360° auto-stitch always fails",
     "The imaging service is unreachable. Confirm its container is running and that the API's imaging-service address points at it."],
], widths=[2.3, 3.9])

# §3.1 Overview
a = r.heading("3.1 Overview")
a = r.add_paragraphs(a, [
    F.PRODUCT_ONE_LINER + f" It is used through {S['surfaces']} applications, each built for one "
    "job: the Audience Website (discover shows, buy tickets, watch a livestream, tip a performer), "
    "the Audience Mobile app (order food and drink while seated at a venue), the Owner Web Dashboard "
    "(set up a venue, run shows, handle money), the Staff Mobile app (sell at the counter, check "
    "tickets at the door, run the F&B board) and the Admin Web Console (approve venues, moderate "
    "content, handle refunds and complaints). All five share one backend, so an action taken in one "
    "is immediately visible in the others.",

    "The four walkthroughs below follow the platform's natural order and chain into one continuous "
    "story: an Owner sets up a venue and submits a show, an Admin approves it, an Audience member "
    "buys a ticket and attends, and Staff sell and check tickets on the night. Walking them in order "
    "leaves a realistic, fully populated system. Each step names the screen you are on, what you do "
    "there, and what you should see if it worked — so a step that does not match is easy to spot.",
])

# §3.2 Workflow 1 — Owner
h = r.heading("3.2 Workflow 1")
h.runs[0].text = "3.2 Workflow 1 — Owner: set up a venue and get a show approved"
for extra in h.runs[1:]:
    extra._element.getparent().remove(extra._element)
a = r.add_after(h,
    "On the Owner Web Dashboard. This produces the state every other workflow depends on: an Owner "
    "account, an approved venue with seating zones, an active subscription, and one show submitted "
    "for review.")
owner_tbl = r.add_table(a, STEP_HDR, [
    ["1. Create the account", "Sign Up",
     "Enter your name, email and password, choose the Owner role, and accept the terms.",
     "A confirmation that a six-digit code has been emailed to you."],
    ["2. Verify the email", "Verify Email",
     "Enter the code. Use Resend if it has not arrived after a minute.",
     "You are signed in and land on My Venues, empty because you have no venue yet."],
    ["3. Register the venue", "Create / Edit Venue",
     "Fill in name, description, address (pick the point on the map) and atmosphere, then upload the business licence.",
     "The venue appears on My Venues marked Pending — it is not yet public."],
    ["4. Add a payout account", "Bank Accounts",
     "Add the account that venue earnings should be paid into and mark it as default.",
     "The account is listed as unverified; an Admin verifies it before any settlement is released."],
    ["5. Subscribe to a plan", "Subscription Plans → My Subscription",
     "Compare the plans, choose one and pay. You are redirected to the gateway and back.",
     "My Subscription shows an active plan with its expiry and its limits."],
    ["6. Wait for venue approval", "My Venues",
     "Nothing to do — an Admin reviews it (Workflow 2). Refresh to check.",
     "The badge changes from Pending to Approved. Until then no show can be published."],
    ["7. Lay out the seating", "Seating Zone Editor",
     "Create each zone with its real capacity, then drag it onto the floor plan so the layout matches the room.",
     "Zones are saved against the venue and reused by every future show, not re-entered each time."],
    ["8. Build the 360° tour (optional)", "360° Tour Management",
     "Upload a finished panorama, or upload several room photos and choose Auto-Stitch. Then place hotspots to link scenes.",
     "Auto-stitch shows a processing state and finishes in roughly 15 to 30 seconds; the scene then appears on the public venue page."],
    ["9. Create the show", "Create / Edit Show",
     "Enter name, description and schedule, add the performer line-up, and upload or generate a poster.",
     "The show is saved as a draft and appears on My Shows."],
    ["10. Price the tickets", "Ticket Tiers",
     "Add at least one tier with its capacity, and one price window with sale start and end.",
     "The tier is listed with its window. Total capacity may not exceed your plan's limit."],
    ["11. Declare the legal permit", "Legal & Royalty Declaration",
     "Enter the performance-permit reference and, where applicable, the royalty reference.",
     "The declaration is saved. Without it the show cannot be submitted."],
    ["12. Submit for review", "Show Control Center",
     "Choose Submit for Review.",
     "Status moves from draft to pending. The show is still not public — an Admin must approve it. If a prerequisite is missing the screen names which one."],
], widths=STEP_W)

# §3.3 Workflow 2 — Admin
h = r.heading("3.3 Workflow 2")
h.runs[0].text = "3.3 Workflow 2 — Admin: approve the venue and publish the show"
for extra in h.runs[1:]:
    extra._element.getparent().remove(extra._element)
a = r.add_after(h,
    "On the Admin Web Console. There is deliberately no way to register as an Admin: the first "
    "account is provisioned directly by an operator, and an automated check alerts if an Admin "
    "account ever appears outside the known set. Sign in with the Admin credentials supplied with "
    "the release.")
admin_tbl = r.add_table(a, STEP_HDR, [
    ["1. Sign in", "Log In", "Sign in with the provisioned Admin account.",
     "The console opens on its review queues. Signing in with a non-Admin account simply does not show them."],
    ["2. Approve the venue", "Pending Venues",
     "Open the venue from Workflow 1, check the licence and address, then approve — or reject with a reason.",
     "The venue becomes approved and its Owner is notified. A rejected venue cannot be amended and resubmitted; the Owner must create a new one, which keeps the rejection on an immutable record."],
    ["3. Review the show", "Moderation Queue",
     "Open the pending show. An AI risk score is shown as advice; read the description, poster and line-up yourself, then approve or reject with a note.",
     "On approval the show becomes publicly visible immediately. The queue tracks a 24-hour deadline and warns as it approaches."],
    ["4. Verify the payout account", "Verify Bank Account",
     "Reconcile the account holder against the venue's registered business, then mark it verified.",
     "The account becomes verified. Settlements to that venue stay blocked until this is done."],
    ["5. Confirm it is public", "Audience Website — Home / Show Search",
     "Open the Audience Website without signing in and search for the show.",
     "The approved show appears in public results — the same view any visitor gets."],
], widths=STEP_W)

# §3.4 Workflow 3 — Audience
a = r.add_after(admin_tbl,
                "3.4 Workflow 3 — Audience: find a show, buy a ticket, attend and tip", "Heading 3")
a = r.add_after(a,
    "On the Audience Website, with the Audience Mobile app used once inside the venue. This is the "
    "full customer journey from discovering a show to tipping the performer during it.")
aud_tbl = r.add_table(a, STEP_HDR, [
    ["1. Create the account", "Sign Up → Verify Email",
     "Register with the Audience role and enter the emailed code.",
     "You are signed in and land on the home page."],
    ["2. Find a show", "Home / Show Search", "Search or filter by genre, mood, date or city.",
     "Matching published shows appear. A sold-out show is visibly disabled rather than failing after you click."],
    ["3. Check the details", "Show Detail → Venue Detail → 360° Tour",
     "Read the line-up and tiers, open the venue page, and explore the tour to see the room before booking.",
     "Buy Tickets is enabled only while the show is on sale; otherwise the reason is shown in its place."],
    ["4. Choose tickets", "Select Tickets", "Pick a tier and quantity, then confirm the hold.",
     "A 15-minute countdown starts. Those seats are reserved for you and released automatically if you do not pay."],
    ["5. Pay", "Gateway checkout → Payment Result",
     "Complete the payment and wait to be returned.",
     "The result screen confirms the purchase. If you close the tab too early the ticket still confirms, because the gateway tells the platform independently."],
    ["6. Get the ticket", "My Tickets → Ticket Detail", "Open the ticket.",
     "A QR code with the show and seating details — the screen to show at the door, readable at phone width."],
    ["7. Enter the venue", "Ticket Detail on your phone", "Show the QR code to Staff at the door.",
     "Staff scan and confirm; the ticket is marked used. It is valid for the whole evening, not a fixed showtime."],
    ["8. Order food and drink", "Audience Mobile app — Order F&B → My Order",
     "Browse the menu, add items and place the order from your table.",
     "The order shows its status, advancing through preparing and served as staff work through it. Refresh to update."],
    ["9. Watch from home instead", "Live Viewing Room",
     "If you bought a livestream ticket, open the show at its start time.",
     "The stream plays with live chat and a viewer count. Access is limited to genuine ticket holders and to a maximum number of devices; a refusal states which reason applies."],
    ["10. Tip a performer", "Donate, over the viewing room",
     "Choose a performer, enter an amount and an optional message, decide whether to appear anonymously, then pay.",
     "The donation appears on the public ticker. Its status then advances as the venue acknowledges receipt and forwards the performer's share — visible on My Donations."],
    ["11. Rate the show", "Rate Show, on Show Detail",
     "After the show ends, leave a rating and a comment.",
     "Your rating is added to the show's public average."],
], widths=STEP_W)

# §3.5 Workflow 4 — Staff
a = r.add_after(aud_tbl,
                "3.5 Workflow 4 — Staff: run the floor on show night", "Heading 3")
a = r.add_after(a,
    "On the Staff Mobile app. A Staff account is created by an Owner assigning an existing account "
    "to their venue — there is no Staff self-registration — and the session is scoped to that one "
    "venue for the whole shift, so there is no venue switcher to get wrong.")
staff_tbl = r.add_table(a, STEP_HDR, [
    ["1. Get assigned", "Owner Dashboard — Staff Management",
     "The Owner enters the staff member's existing account email and assigns them to the venue.",
     "The account gains Staff access to that venue only. One account may be actively assigned to one venue at a time."],
    ["2. Sign in", "Staff Mobile — Log In", "Sign in with the assigned account.",
     "The app opens on a bottom tab bar — Sell, Check-In, F&B — already scoped to your venue."],
    ["3. Sell at the counter", "Walk-In Sale → Sale Confirmation",
     "Pick the show and tier, set the quantity, take cash, and confirm.",
     "A confirmed ticket with its QR code, ready to hand over or scan straight away. Walk-in sales continue after the show has started."],
    ["4. Check tickets at the door", "Check-In Scanner",
     "Scan the customer's code and review the preview before confirming.",
     "A clear accepted result, or a specific refusal — already used, wrong show, online-only ticket, or frozen mid-transfer. If the connection drops the scan fails visibly rather than appearing to succeed."],
    ["5. Work the F&B orders", "Order Board",
     "Advance each order through preparing, served and paid, or cancel it.",
     "The board offers exactly one next action per order rather than a free choice of status, so an order cannot skip a step."],
    ["6. Start and end the broadcast", "Show Control Center (Start/End)",
     "At show time start the broadcast; stop it when the show ends.",
     "Viewers holding a livestream ticket can now join. Staff see only Start and End — every other show action is hidden, not merely disabled."],
], widths=STEP_W)

# §3.6 Key things to know
a = r.add_after(staff_tbl, "3.6 Key things to know", "Heading 3")
r.add_table(a, ["Topic", "What to know"], [
    ["Signing in", "One account works across every surface it has access to. Your role decides what you see; there are no separate logins per application."],
    ["Roles", "Anyone may register as Audience or Owner. Staff access is granted by an Owner. Admin accounts are provisioned by an operator and cannot be self-registered."],
    ["Venue approval", "A venue must be approved before it is publicly listed or may publish any show."],
    ["Show lifecycle", "Draft, then pending once the Owner submits, then published once an Admin approves. Only a published show is publicly visible or sellable."],
    ["Ticket validity", "A ticket is valid for the whole evening rather than a fixed start time, and walk-in sales continue after the show has begun."],
    ["Ticket holds", "Selecting tickets reserves them for 15 minutes. If payment is not completed the hold is released and the seats return to sale."],
    ["Payments", "All payments go through the gateway; the platform never sees card details. A confirmation can arrive even if you close the browser tab."],
    ["Donations", "A tip is not paid to the performer instantly — the venue acknowledges receipt and then forwards the performer's share, and every step is visible to the donor and on the public feed."],
    ["Refunds", "Cancelling a ticket or a show creates a refund request that an Admin reviews; refunds are never paid automatically."],
    ["Offline behaviour", "Door check-in and counter sales require a connection. A dropped connection blocks the action rather than silently queuing it — an accepted trade-off to avoid double-selling a seat."],
    ["Privacy", "You can export or permanently erase your own personal data from Privacy & Data. Erasure is irreversible and signs you out everywhere; financial records are retained in anonymised form as the law requires."],
    ["Notifications", "In-app notifications appear on every surface; push notifications require granting permission in the mobile apps; some events also arrive by email or SMS."],
], widths=[1.4, 4.8])

path = r.save()
print(f"built {path}")
