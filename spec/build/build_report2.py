"""Report 2 — Project Management Plan, built fresh from the pristine FPT template."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from docxkit import Report
import facts as F

r = Report("Report2_Project Management Plan.docx",
           "Report2_Project Management Plan - MusicLounge.docx")

NAMES = [m["short"] for m in F.TEAM]          # Hải · Nhiên · Khoa · Phúc

# ── PASS 1 ───────────────────────────────────────────────────────────────────
r.clear_regions(
    ("1.1 Scope & Estimation", "1.2 Project Objectives"),
    ("1.2 Project Objectives", "1.3 Project Risks"),
    ("1.3 Project Risks", "2. Management Approach"),
    ("2. Management Approach", "2.1 Project Process"),
    ("2.1 Project Process", "2.2 Quality Management"),
    ("2.2 Quality Management", "2.3 Training Plan"),
    ("2.3 Training Plan", "3. Project Deliverables"),
    ("3. Project Deliverables", "4. Responsibility Assignments"),
    ("4. Responsibility Assignments", "5. Project Communications"),
    ("5. Project Communications", "6. Configuration Management"),
    ("6. Configuration Management", "6.1 Document Management"),
    ("6.1 Document Management", "6.2 Source Code Management"),
    ("6.2 Source Code Management", "6.3 Tools & Infrastructures"),
    ("6.3 Tools & Infrastructures", None),
)

# ── PASS 2 ───────────────────────────────────────────────────────────────────
r.record_of_changes([
    [F.DOC_DATE, "A", F.TEAM[0]["name"],
     "Project Management Plan covering the whole platform: backend, five client surfaces, imaging "
     "microservice, UI/UX design, Azure deployment and documentation."],
])

# §1.1 Scope & Estimation
a = r.heading("1.1 Scope & Estimation")
a = r.add_after(a,
    "Every deliverable of the platform is broken down below and estimated in man-days. Each leaf "
    "item is rated Simple, Medium or Complex against how much of it is new logic rather than a "
    "repeat of an established pattern. Group rows carry no estimate of their own; the total is the "
    "sum of the leaves.")
rows = [[num, item, cx or "", str(eff) if eff else ""] for num, item, cx, eff in F.WBS]
rows.append(["", "Total Estimated Effort (man-days)", "", str(F.WBS_TOTAL)])
r.add_table(a, ["#", "WBS Item", "Complexity", "Est. Effort (man-days)"], rows,
            widths=[0.55, 3.65, 0.95, 1.05])

# §1.2 Project Objectives
a = r.heading("1.2 Project Objectives")
a = r.add_paragraphs(a, [
    "Scope objective — deliver a working, multi-surface platform that a review committee can "
    f"exercise end to end across all {F.SCALE['login_roles']} roles (Audience, Owner, Staff, Admin) "
    "on a deployed environment: the backend API, all five client surfaces, the imaging microservice "
    "and the Azure hosting they run on.",

    f"Time objective — fit the work breakdown above inside the project window "
    f"({F.TIMELINE['start']} – {F.TIMELINE['end']}, about {F.TIMELINE['weeks']} weeks), sequenced so "
    "that every domain's happy path is functional well before the final weeks, leaving the last two "
    "to three weeks for hardening, security and accessibility review, and documentation rather than "
    "first-time feature work.",

    f"Cost objective — effort is a fixed team-capacity budget rather than a monetary one. The work "
    f"breakdown sums to {F.WBS_TOTAL} man-days across backend, frontend, design, deployment and "
    f"documentation, delivered by the {len(F.TEAM)}-member team named in Report 1 §1.2.",

    "Quality objective — the targets below are checked at each milestone rather than only at the end.",
])
r.add_table(a, ["#", "Testing Stage", "Coverage", "Target", "Notes"], [
    ["1", "Code review", "100% of pull requests", "No unreviewed merge",
     "Every change reviewed against the project's own review checklist before merge."],
    ["2", "Unit test", "Domain and Application logic; frontend components",
     "All documented business rules covered",
     "Runs with no database and no network, so a broken rule is caught before integration."],
    ["3", "Integration test", "Every handler that touches the database",
     "0 known-broken at release",
     "Runs against SQLite in CI and against real SQL Server before each milestone."],
    ["4", "End-to-end test", "Each actor's golden path across surfaces", "0 failing scenarios",
     "Browser and device automation against the deployed staging environment."],
    ["5", "Security & accessibility", "OWASP ASVS 5.0.0; WCAG 2.2 AA",
     "0 open Blocker or Critical", "Reviewed before the release gate."],
], widths=[0.45, 1.35, 1.60, 1.25, 1.55])
a = r.para("Quality objective")
a = r.add_paragraphs(r.doc.paragraphs[-1], [
    "Milestone timeliness — at least 90% of sprint-end milestones delivered on the date committed "
    "at sprint planning.",
    f"Allocated effort — Requirements and design 18%, coding (backend and frontend) 62%, testing "
    f"12%, documentation 8% of the {F.WBS_TOTAL} man-day total.",
])

# §1.3 Project Risks
a = r.heading("1.3 Project Risks")
r.add_table(a, ["#", "Risk Description", "Impact", "Likelihood", "Response Plan"], [
    ["1", "The payment gateway behaves differently in production than in the sandbox, particularly "
          "around callback timing and retries.", "High", "Medium",
     "Build the callback handler to be idempotent and safe against duplicate or out-of-order "
     "delivery regardless of what the sandbox happens to do, rather than coding to observed "
     "sandbox behaviour."],
    ["2", "The CI database engine (SQLite) and the production engine (SQL Server) diverge on "
          "cascade rules and enum-string columns, so a fully green CI run is not proof of "
          "correctness.", "High", "High",
     "Already materialised once: a real SQL-Server-only defect passed a green SQLite suite. The "
     "full suite is re-run against a real SQL Server instance before every milestone release."],
    ["3", "Third-party AI dependency for moderation, poster generation and recommendation scoring "
          "— an outage, quota exhaustion or policy change is outside the team's control.",
     "Medium", "Medium",
     "Every AI call degrades gracefully to a neutral result rather than blocking the request, and "
     "poster generation has a second provider behind the same interface."],
    ["4", "The panorama-stitching feature depends on native image-processing binaries that behave "
          "differently across machines.", "Medium", "Medium",
     "Kept as a separate containerised microservice so a stitching failure degrades one feature "
     "instead of the whole API."],
    ["5", "Frontend work is concentrated in the second half of the schedule, so any slip in design "
          "handoff or screen wiring compresses the remaining buffer.", "High", "Medium",
     "Track frontend progress as its own burndown, and prioritise screens by role-criticality "
     "(ticket purchase, venue and show management) over lower-value surfaces if time runs short."],
    ["6", "Only a small number of Admin accounts exist and there is no self-service path to create "
          "one, so losing Admin access blocks a large surface of testing.", "Medium", "Low",
     "Document the provisioning procedure and keep at least one known-good Admin credential "
     "recorded outside any single member's personal notes."],
    ["7", "Livestream infrastructure is a paid third-party service; exhausting credit close to a "
          "demo would break the most visible feature.", "Medium", "Low",
     "Confirm provider account status and quota headroom explicitly ahead of each demo."],
    ["8", "Team familiarity with the architecture and the frontend stack varies, so a change can "
          "quietly break a layering rule or a shared convention.", "Medium", "Medium",
     "Addressed by the training plan in §2.3 and by mandatory review against the project's "
     "conventions before merge."],
    ["9", "Scope creep — the domain offers more genuinely useful features than the schedule can "
          "absorb.", "Medium", "Medium",
     "The work breakdown in §1.1 is the scope contract; new requests are logged and prioritised "
     "against it rather than added ad hoc."],
], widths=[0.4, 2.15, 0.65, 0.75, 2.25])

# §2 Management Approach
a = r.heading("2. Management Approach")
r.add_after(a, "The team works in weekly sprints aligned to the supervisor check-in cadence, with "
               "quality practices applied continuously rather than saved for the end.")

a = r.heading("2.1 Project Process")
r.add_paragraphs(a, [
    "Scrum with one-week sprints: planning at the start of the week, a working demo at the end of "
    "it, and a short retrospective before the next planning session. The work breakdown in §1.1 is "
    "the product backlog, and each leaf item is split into one to three sprint tasks at planning "
    "time.",
    "Sprints 1–2: project scaffold, CI pipeline and database migrations; backend authentication; "
    "design system and per-surface design briefs. Sprints 3–6: backend venue, show and ticketing — "
    "the core marketplace loop — in parallel with the frontend authentication and venue screens. "
    "Sprints 7–9: backend livestream, donation and ledger, the most architecturally demanding area, "
    "sequenced after the core loop is stable; frontend show and ticketing screens alongside. "
    "Sprints 10–11: backend F&B, subscription, complaints and admin back-office; frontend "
    "livestream and donation screens. Sprints 12–13: remaining frontend screens and the mobile "
    "applications; cross-cutting hardening. Sprints 14–15: Azure deployment, full regression "
    "against real SQL Server, user acceptance testing, demo rehearsal and documentation.",
])

a = r.heading("2.2 Quality Management")
a = r.add_after(a, "Five practices, applied every sprint rather than at the end of the project:")
r.add_bullets(a, [
    "Defect prevention — a fixed set of conventions per tier, enforced at review rather than "
    "discovered in testing: on the backend, data access only through the repository and unit of "
    "work, no direct database access from the API layer, and a validator for every command; on the "
    "frontend, shared design-system components instead of one-off markup, server state owned by the "
    "query library rather than hand-rolled effects, and no screen shipped without its loading, "
    "empty and error states designed.",
    "Reviewing — every pull request reviewed against the documented conventions before merge, with "
    "no direct pushes to the main branch. A change spanning tiers is raised as linked pull requests "
    "reviewed together, so an API contract and its consumer never merge out of step.",
    "Unit testing — backend domain and application logic tested with no database, and frontend "
    "components tested in isolation against a mocked API, so a broken rule or a broken screen state "
    "is caught before integration testing runs.",
    "Integration testing — real handler-to-database round trips, run in CI for speed and again "
    "against a real SQL Server instance before every milestone, to catch the class of defect the "
    "CI engine cannot reproduce.",
    "System testing — automated end-to-end journeys plus manual walkthroughs of each actor's golden "
    "path against the deployed environment, together with a security pass against OWASP ASVS 5.0.0, "
    "an accessibility pass against WCAG 2.2 Level AA, and a load pass against realistic peak shapes.",
])

a = r.heading("2.3 Training Plan")
r.add_table(a, ["Training Area", "Participants", "When, Duration", "Waiver Criteria"], [
    ["Clean Architecture and CQRS", "Any member new to the pattern", "Sprint 1, ~1 day",
     "Waived with prior project experience in the pattern"],
    ["EF Core and SQL Server: migrations, indexing, transactions", "Any member new to EF Core",
     "Sprints 1–2, ~1 day", "Waived with prior EF Core project experience"],
    ["React, TypeScript and Tailwind CSS", "Any member new to the frontend stack",
     "Sprints 2–3, ~1 day", "Waived with prior React and TypeScript experience"],
    ["React Native and mobile build tooling", "Members building the two mobile apps",
     "Sprint 10, ~0.5 day", "Waived with prior React Native experience"],
    ["Git and pull-request workflow", "All members", "Sprint 1, ~0.5 day", "Mandatory"],
    ["Azure deployment and CI/CD", "Members owning deployment", "Sprint 13, ~0.5 day",
     "Mandatory for the task owners"],
    ["OWASP ASVS 5.0.0 and WCAG 2.2 basics", "Members owning the security and accessibility passes",
     "Before Sprint 13", "Mandatory for the task owners"],
], widths=[1.85, 1.55, 1.15, 1.65])

# §3 Project Deliverables
a = r.heading("3. Project Deliverables")
r.add_table(a, ["#", "Deliverable", "Due", "Notes"], [
    ["1", "Product backlog and work breakdown", "Sprint 1, revised each sprint",
     "The scope contract for the project (§1.1)."],
    ["2", "Backend API source code", "End of project", "Clean Architecture solution."],
    ["3", "Frontend source code — three web applications", "End of project",
     "Audience Website, Owner Dashboard, Admin Console."],
    ["4", "Frontend source code — two mobile applications", "End of project",
     "Staff Mobile and Audience F&B, as signed review builds."],
    ["5", "Panorama-stitching microservice", "End of project", "Containerised imaging service."],
    ["6", "Database migration scripts", "End of project", "Code-First migrations; no hand-written DDL."],
    ["7", "Deployed platform on Azure", "End of project",
     "Reachable URLs and installable mobile builds for the review committee."],
    ["8", "Reports 1–7", "End of project", "The SEP490 document set."],
    ["9", "Test evidence", "End of project", "Test case workbooks and the executed test report."],
    ["10", "Defect list", "Continuous", "Tracked throughout; summarised in Report 5."],
    ["11", "Presentation slide deck", "Demo date", "—"],
], widths=[0.4, 2.05, 1.25, 2.5])

# §4 Responsibility Assignments
a = r.heading("4. Responsibility Assignments")
a = r.add_after(a,
    "D — Do · R — Review · S — Support · I — Informed · blank — not involved. Each row names exactly "
    "one owner so accountability is never split, and every deliverable that leaves the team has a "
    "named reviewer.")
r.add_table(a, ["Responsibility", f"{NAMES[0]} (Leader)", NAMES[1], NAMES[2], NAMES[3]], [
    ["Project planning and tracking", "D", "S", "S", "S"],
    ["Requirements analysis and specification (Reports 1 and 3)", "R", "D", "S", "S"],
    ["UI/UX design system and per-surface design briefs", "S", "D", "R", "I"],
    ["Solution architecture and design document (Report 4)", "D", "R", "S", "S"],
    ["Backend — platform scaffold and shared infrastructure", "D", "S", "S", "R"],
    ["Backend — auth, venue, show, ticketing", "R", "D", "S", "S"],
    ["Backend — livestream, donation, payment and ledger", "D", "S", "R", "S"],
    ["Backend — F&B, subscription, complaints, admin back-office", "R", "S", "D", "S"],
    ["Backend — background jobs and security hardening", "R", "I", "S", "D"],
    ["Frontend — Audience Website", "S", "D", "R", "I"],
    ["Frontend — Owner Web Dashboard", "R", "S", "D", "S"],
    ["Frontend — Admin Web Console", "S", "R", "S", "D"],
    ["Frontend — Staff and Audience mobile applications", "D", "S", "S", "R"],
    ["Test design, automation and execution (Report 5)", "R", "S", "S", "D"],
    ["Azure deployment, CI/CD and environment configuration", "D", "I", "S", "R"],
    ["Documentation and user guides (Report 6)", "S", "R", "I", "D"],
    ["Final integration, Report 7 and demo rehearsal", "D", "S", "S", "S"],
], widths=[2.6, 1.0, 0.85, 0.85, 0.9])

# §5 Project Communications
a = r.heading("5. Project Communications")
r.add_table(a, ["Communication Item", "Who / Target", "Purpose", "When, Frequency", "Type, Tool"], [
    ["Sprint planning", "Whole team and supervisor", "Agree the sprint's scope and acceptance criteria",
     "Weekly, start of sprint", "Meeting, in person or online"],
    ["Sprint demo and retrospective", "Whole team and supervisor",
     "Show working software; review what did and did not go well", "Weekly, end of sprint",
     "Meeting with a running instance"],
    ["Daily sync", "Whole team", "Surface blockers early", "Daily, about 15 minutes",
     "Chat channel or short call"],
    ["Code review", "Author and reviewer", "Catch defects and convention drift before merge",
     "Per pull request", "Pull-request comments"],
    ["Supervisor check-in", "Leader and supervisor", "Escalate risks, confirm scope decisions",
     "At least fortnightly", "Email or scheduled meeting"],
    ["Defect reporting", "Whole team", "Log and triage a found defect", "As found", "Issue tracker"],
], widths=[1.35, 1.25, 1.65, 1.15, 1.4])

# §6 Configuration Management
a = r.heading("6.1 Document Management")
r.add_paragraphs(a, [
    "All narrative documentation lives in the repository in Markdown, version-controlled alongside "
    "the code it describes, so a documentation change and the code change it documents land in the "
    "same review. A separate wiki or shared drive drifts out of sync with the code far more easily.",
    "The seven formal report documents are generated from that same source into the official "
    "templates rather than hand-edited, so a scope or design change can be reflected by rebuilding "
    "them instead of manually patching each file — which is what previously let the reports drift "
    "apart from one another.",
    "Every substantive change is recorded in that document's own Record of Changes table, so a "
    "reader opening only the document still sees its change history without consulting the "
    "repository.",
])

a = r.heading("6.2 Source Code Management")
r.add_paragraphs(a, [
    "Git hosted on GitHub, across the platform's repositories: the backend API, the client "
    "applications and the imaging microservice. Trunk-based development off a single main branch in "
    "each, with short-lived feature branches per work-breakdown task merged through a pull request.",
    "Every pull request needs at least one review before merge, and the CI pipeline runs the "
    "relevant test suite on every request with merge blocked on a red build. Database schema changes "
    "are Code-First migrations committed alongside the entity change that caused them, so there is "
    "no hand-written DDL and no schema drift between environments.",
])

a = r.heading("6.3 Tools & Infrastructures")
r.add_table(a, ["Category", "Tools / Infrastructure"], [
    ["Backend framework", F.STACK["backend"]],
    ["Database and ORM", F.STACK["database"] + "; SQLite for CI-speed integration tests"],
    ["Background jobs", f"{F.STACK['jobs']} — {F.SCALE['job_classes']} job classes, "
                        f"{F.SCALE['recurring_jobs']} on recurring schedules"],
    ["Real-time", F.STACK["realtime"] + " — livestream chat and access gating, donation ticker"],
    ["Frontend — web", F.STACK["web_frontend"]],
    ["Frontend — mobile", F.STACK["mobile_frontend"]],
    ["UI/UX design", "Shared design system and component library, driven from per-surface design briefs"],
    ["Auxiliary microservice", F.STACK["microservice"]],
    ["Payment", "VNPay (sandbox environment)"],
    ["Livestream", "Mux and Cloudflare Stream, selected at runtime behind a provider factory"],
    ["AI services", "Gemini for moderation, poster generation and recommendation scoring; OpenAI as an alternate poster provider"],
    ["Notifications", "Firebase Cloud Messaging (push), Twilio (SMS), SMTP (email)"],
    ["Cloud platform", "Microsoft Azure — " + ", ".join(name for name, _ in F.AZURE[:7])],
    ["Source control and CI/CD", "Git and GitHub with pull-request-gated GitHub Actions; build, test, then deploy to Azure on merge"],
    ["Testing", "xUnit and FluentAssertions (backend), Vitest and React Testing Library (web), "
                "Jest and React Native Testing Library (mobile), Playwright (end-to-end), k6 (load)"],
    ["API documentation", "Swagger / OpenAPI interactive documentation"],
    ["Diagramming", "draw.io for structural diagrams and PlantUML for behavioural diagrams, rendered to black-and-white images"],
], widths=[1.5, 4.7])

path = r.save()
print(f"built {path}")
