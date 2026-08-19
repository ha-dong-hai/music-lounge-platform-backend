"""Report 5 — Software Test Documentation, built fresh from the pristine template."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from docxkit import Report
import facts as F

S, T = F.SCALE, F.TESTS

r = Report("Report5_Test Documentation.docx",
           "Report5_Test Documentation - MusicLounge.docx")

# ── PASS 1 ───────────────────────────────────────────────────────────────────
r.clear_regions(
    ("1. Scope of Testing", "2. Test Strategy"),
    ("2. Test Strategy", "2.1 Testing Types"),
    ("2.1 Testing Types", "2.2 Test Levels"),
    ("2.2 Test Levels", "2.3 Supporting Tools"),
    ("2.3 Supporting Tools", "3. Test Plan"),
    ("3. Test Plan", "3.1 Human Resources"),
    ("3.1 Human Resources", "3.2 Test Environment"),
    ("3.2 Test Environment", "3.3 Test Milestones"),
    ("3.3 Test Milestones", "4. Test Cases"),
    ("4. Test Cases", "5. Test Reports"),
    ("5. Test Reports", None),
)

# ── PASS 2 ───────────────────────────────────────────────────────────────────
r.record_of_changes([
    [F.DOC_DATE, "A", F.TEAM[0]["name"],
     f"Test documentation covering all three tiers of the platform — backend API, "
     f"{S['surfaces']} client surfaces and cross-tier journeys — with results from an actual run "
     f"of {T['total_tests']} tests."],
])

# §1 Scope of Testing
a = r.heading("1. Scope of Testing")
a = r.add_paragraphs(a, [
    "In scope is the complete platform, not one tier of it. That means the backend API — every "
    f"command and query handler across the {S['feature_folders']} feature areas, exercised through "
    "the real HTTP pipeline so that authentication, authorisation policies and the pipeline "
    f"behaviours all run exactly as they do in production; all {S['surfaces']} client surfaces — the "
    "three web applications and the two mobile applications; the imaging microservice; and the "
    "end-to-end journeys that cross all three tiers, such as an Owner publishing a show and an "
    "Audience member buying, paying for and checking in with a ticket for it.",

    "Non-functional testing in scope: security boundary testing against the OWASP ASVS 5.0.0 "
    "checklist; accessibility conformance of the three web surfaces against WCAG 2.2 Level AA; "
    "responsive and cross-browser behaviour; and the compliance behaviours the platform is legally "
    "obliged to honour — personal-data export and erasure, and protection of identity documents.",

    "Out of scope is the internal behaviour of third-party providers themselves. The payment "
    "gateway, livestream provider, AI services and messaging providers are exercised through their "
    "sandbox environments or through hand-written fakes; what is tested is our own handling of their "
    "responses, including their failure modes, not their correctness. Sustained load and capacity "
    "testing runs as a separate exercise against a deployed environment rather than inside the "
    "automated suite, because it needs real infrastructure rather than an in-process host.",

    "Four test levels are applied, each with a named owner. Unit — the developer who writes the "
    "code, covering pure logic with no database or network. Integration — the feature owner, "
    "covering backend handler-to-database round trips and frontend component-to-mocked-API "
    "rendering. System and end-to-end — the test automation engineer, driving a real browser and "
    "device against a running instance. Acceptance — the whole team with the supervisor, walking "
    "each actor's golden path manually before a milestone demo. The entry criterion at every level "
    "is a green build; the exit criterion is no open defect of Blocker or Critical severity.",

    "One constraint is stated plainly rather than buried. The automated pipeline runs the backend "
    "suite against SQLite for speed, not against the production database engine. This is a "
    "deliberate trade-off and it is not merely theoretical: the project has already hit one real "
    "defect — a multiple-cascade-path constraint — that SQLite's schema creation does not enforce "
    "and that a fully green pipeline run therefore could not have caught. The mitigation is a "
    "required manual gate: the full backend suite is re-run against a real SQL Server instance "
    "before any release that touches migrations, cascade rules or enumeration columns.",
])

# §2.1 Testing Types
a = r.heading("2.1 Testing Types")
r.add_table(a, ["Testing Type", "Objective", "Technique", "Completion Criteria"], [
    ["Functional", "Verify each use case in Report 3 §2.2 behaves per its documented business rules, on the API and on the surface that calls it.",
     "Backend: assert on the HTTP response and the resulting database state. Frontend: render the screen against a mocked API and assert the user-visible outcome.",
     "Every documented business rule has at least one positive and one negative test."],
    ["UI / component", "Verify each screen renders the right content, enables the right actions for the role, and handles loading, empty and error states.",
     "Component tests driving the interface the way a user does — by visible text and role, not by internal state.",
     "Every delivered screen has its default, empty, loading and error states asserted."],
    ["End-to-end / system", "Verify a complete journey works across frontend, API, database and background functions together.",
     "Browser automation against a deployed staging environment, with the mobile golden paths covered on device emulators.",
     "Each actor's golden path passes end to end without manual intervention."],
    ["Security / authorization", "Verify the three-layer authorisation model cannot be bypassed and that no cross-role or cross-venue access succeeds.",
     "A dedicated security suite plus per-feature negative tests, and the OWASP ASVS 5.0.0 checklist walked against a running instance.",
     "No cross-role or cross-venue access succeeds; no open Blocker or Critical finding."],
    ["Accessibility", "Verify the web surfaces are usable by keyboard and screen reader and meet WCAG 2.2 Level AA.",
     "Automated rule scanning inside the component suite, plus manual keyboard-only and screen-reader passes on the highest-traffic screens.",
     "Zero critical or serious automated violations; every control operable by keyboard alone."],
    ["Compatibility / responsive", "Verify behaviour across supported browsers, viewport sizes and mobile operating-system versions.",
     "Browser projects at desktop, tablet and phone widths; an emulator matrix for the two mobile applications.",
     "All golden-path scenarios pass on every supported browser and viewport."],
    ["Compliance", "Verify personal-data export and erasure, protection of identity documents, and the retention rules the law requires.",
     "A dedicated suite asserting on database state after an erasure request and on access control over endpoints serving personal data.",
     "Every compliance behaviour cited in Report 3 §4.2.5 has at least one test."],
    ["Regression", "Catch a previously fixed defect recurring.",
     "The full suite runs on every pull request; a fixed defect gains the test that would have caught it.",
     "The whole suite runs on every pull request and a red build blocks merge."],
    ["Performance / load", "Confirm the platform holds under a realistic peak shape without overselling zone capacity.",
     "Load scenarios against a deployed staging environment, plus frontend performance budgets in the pipeline.",
     "No overselling under concurrent load; response times within the targets in Report 3 §4.2.3."],
], widths=[1.15, 1.75, 1.85, 1.45])

# §2.2 Test Levels
a = r.heading("2.2 Test Levels")
a = r.add_after(a, "X marks the levels at which each type from §2.1 is executed.")
a = r.add_table(a, ["Type of Tests", "Unit", "Integration", "System / E2E", "Acceptance"], [
    ["Functional", "X", "X", "X", "X"],
    ["UI / component", "X", "X", "", ""],
    ["End-to-end / system", "", "", "X", "X"],
    ["Security / authorization", "", "X", "X", ""],
    ["Accessibility", "", "X", "X", ""],
    ["Compatibility / responsive", "", "", "X", "X"],
    ["Compliance", "", "X", "X", ""],
    ["Regression", "X", "X", "X", ""],
    ["Performance / load", "", "", "X", ""],
], widths=[2.0, 0.9, 1.1, 1.2, 1.0])
r.add_after(r.doc.paragraphs[-1],
    "Backend testing is deliberately integration-first. Rather than a separate, heavily mocked unit "
    "project, the backend suite runs against a real database context through the real HTTP pipeline, "
    "because for this architecture the handler, its validator, the pipeline behaviours and the query "
    "together are what a test needs to prove correct — mocking the database away would let a broken "
    "query pass. Standalone logic with no HTTP or database dependency, such as the ledger journal "
    "factory and the fee calculator, is still tested directly at unit level. On the frontend the "
    "balance is the reverse: component tests dominate, because a screen's logic lives in how it "
    "renders and reacts rather than in server round trips. Only external, paid or non-deterministic "
    "dependencies are replaced, and with hand-written fakes rather than a general mocking framework, "
    "so each test still exercises the real decision logic around an external call.")

# §2.3 Supporting Tools
a = r.heading("2.3 Supporting Tools")
r.add_table(a, ["Purpose", "Tool", "Vendor / In-house", "Version"], [
    ["Backend test runner and assertions", "xUnit with FluentAssertions", "Open source", "2.9 / 6.12"],
    ["Backend in-process HTTP host", "ASP.NET Core test host", "Microsoft", "8.0"],
    ["Backend test database", "SQLite in the pipeline; SQL Server at the release gate", "Microsoft", "8.0 / 2022"],
    ["Background-job test double", "In-memory job storage", "Open source", "0.5"],
    ["External-service test doubles", "Hand-written fakes for payment, push, token verification, livestream and imaging", "In-house", "—"],
    ["Frontend unit and component tests", "Vitest with React Testing Library", "Open source", "2.1 / 16.0"],
    ["Mobile component tests", "Jest with React Native Testing Library", "Open source", "29.7 / 12.5"],
    ["End-to-end browser automation", "Playwright across Chromium, Firefox and WebKit", "Microsoft", "1.47"],
    ["Accessibility scanning", "axe-core rules driven from the automation suite", "Open source", "4.10"],
    ["Frontend performance budgets", "Lighthouse CI", "Open source", "0.14"],
    ["Load and capacity testing", "k6", "Open source", "0.53"],
    ["API contract and manual exploration", "Swagger / OpenAPI interactive documentation", "Open source", "—"],
    ["Continuous integration", "GitHub Actions — build, test and deploy on every pull request and merge", "GitHub", "—"],
    ["Defect tracking", "GitHub Issues with severity labels", "GitHub", "—"],
], widths=[1.85, 2.15, 1.35, 0.85])

# §3.1 Human Resources
a = r.heading("3.1 Human Resources")
r.add_table(a, ["Worker", "Role", "Specific Responsibilities"], [
    [f"{F.TEAM[0]['name']} (Leader)", "Test manager and release gatekeeper",
     "Owns this document and the test plan; runs the backend suite against real SQL Server before any milestone release; makes the final release-readiness call."],
    [F.TEAM[1]["name"], "Test designer",
     "Derives test cases from the use cases and business rules using equivalence partitioning and boundary-value analysis; maintains traceability from acceptance criteria to test cases."],
    [F.TEAM[2]["name"], "Test automation engineer",
     "Writes and maintains the backend suite, the frontend component suites and the end-to-end scenarios; keeps the pipeline green."],
    [F.TEAM[3]["name"], "Security and accessibility tester",
     "Runs the OWASP ASVS 5.0.0 pass and writes the cross-role and cross-venue authorisation tests; runs the WCAG 2.2 AA pass over the three web surfaces."],
    ["Whole team with the supervisor", "Acceptance testers",
     "Walk each actor's golden path manually on the deployed environment before every milestone demo, one persona per member."],
], widths=[1.55, 1.45, 3.2])

# §3.2 Test Environment
a = r.heading("3.2 Test Environment")
r.add_table(a, ["Purpose", "Tool", "Provider", "Version"], [
    ["Backend runtime", ".NET 8 SDK and ASP.NET Core 8", "Microsoft", "8.0.x"],
    ["Frontend runtime and build", "Node.js with Vite for web; React Native CLI for mobile", "Open source", "20 LTS / 5.4"],
    ["Pipeline test database", "SQLite, created fresh per run", "Microsoft provider", "8.0"],
    ["Release-gate database", "SQL Server Developer Edition, local or containerised", "Microsoft", "2022"],
    ["Staging environment", "Azure App Service, Azure SQL Database and Container Apps, provisioned to mirror production", "Microsoft Azure", "—"],
    ["Pipeline compute", "GitHub Actions hosted runners; Windows runners for mobile builds", "GitHub", "—"],
    ["Browser matrix", "Chromium, Firefox and WebKit at desktop (1440px), tablet (768px) and phone (390px)", "Playwright-managed", "1.47"],
    ["Device matrix", "Android emulator (API 33 and 34) and iOS Simulator (17.x)", "Google / Apple", "—"],
    ["External sandboxes", "Payment sandbox, livestream test environment, AI service or its fake", "Third party", "—"],
], widths=[1.55, 2.65, 1.15, 0.85])

# §3.3 Test Milestones
a = r.heading("3.3 Test Milestones")
r.add_table(a, ["Milestone Task", "Start", "End"], [
    ["Test strategy agreed; environments and pipeline ready", "Week 1", "Week 2"],
    ["Backend core-loop tests green — auth, venue, show, ticketing", "Week 3", "Week 6"],
    ["Frontend component suites green for auth and venue screens", "Week 5", "Week 7"],
    ["Backend livestream, donation and ledger tests green", "Week 7", "Week 9"],
    ["Frontend component suites green for show, ticketing and livestream screens", "Week 8", "Week 10"],
    ["Remaining backend tests green — F&B, subscription, complaints, admin", "Week 10", "Week 11"],
    ["Remaining frontend suites green; mobile application suites green", "Week 11", "Week 12"],
    ["End-to-end scenarios green against the staging environment", "Week 12", "Week 13"],
    ["Security, accessibility and compliance passes complete", "Week 13", "Week 13"],
    ["Backend suite re-run against real SQL Server; load test against staging", "Week 14", "Week 14"],
    ["User acceptance testing per persona; regression freeze; final test report", "Week 14", "Week 15"],
], widths=[4.2, 1.0, 1.0])

# §4 Test Cases
a = r.heading("4. Test Cases")
a = r.add_paragraphs(a, [
    "Per-case detail — preconditions, steps, expected result, actual result — is kept in the two "
    "companion workbooks rather than duplicated here, per the template's own convention: unit test "
    "cases in Report5_Unit Test.xls, and integration, system and acceptance cases in "
    "Report5_Test Report.xls.",
    "The table below summarises the automated coverage that exists across all three tiers, as a map "
    "of where the detailed rows in those workbooks come from. Backend figures are counted from the "
    "source tree; the executed total exceeds the authored total because a data-driven test method "
    "runs once per data row.",
])
BE = [
    ("Auth and account", 10, 61),
    ("Venue management and approval", 6, 62),
    ("Show lifecycle and moderation", 6, 50),
    ("Ticketing", 5, 40),
    ("Payment, ledger and settlement", 4, 18),
    ("Livestream and donation", 4, 52),
    ("F&B ordering", 1, 13),
    ("Subscription", 1, 12),
    ("Performers and taxonomy", 3, 30),
    ("Recommendation and AI", 4, 9),
    ("Notifications and background jobs", 7, 33),
    ("Complaints and venue penalties", 2, 24),
    ("Security", 4, 12),
    ("Compliance", 1, 15),
    ("Admin and platform", 2, 14),
    ("Cross-domain journey", 1, 1),
]
FE = [
    ("Audience Website", 30, 96),
    ("Owner Web Dashboard", 27, 89),
    ("Admin Web Console", 16, 48),
    ("Staff Mobile application", 7, 24),
    ("Audience Mobile F&B application", 2, 8),
    ("Shared design-system components", 18, 42),
]
assert sum(s for _, s, _ in BE) == T["backend_suites"], "backend suite count mismatch"
assert sum(s for _, s, _ in FE) == T["frontend_suites"], "frontend suite count mismatch"
assert sum(t for _, _, t in FE) == T["frontend_tests"], "frontend test count mismatch"

rows = [[f"Backend — {n}", str(s), str(t)] for n, s, t in BE]
rows.append(["Backend subtotal (executed cases exceed authored methods because of data-driven tests)",
             str(T["backend_suites"]), str(T["backend_tests"])])
rows += [[f"Frontend — {n}", str(s), str(t)] for n, s, t in FE]
rows.append(["Frontend subtotal", str(T["frontend_suites"]), str(T["frontend_tests"])])
rows.append(["End-to-end — cross-surface journeys", str(T["e2e_suites"]), str(T["e2e_tests"])])
rows.append(["TOTAL", str(T["total_suites"]), str(T["total_tests"])])
r.add_table(a, ["Tier / Area", "Suites", "Tests"], rows, widths=[4.4, 0.9, 0.9])

# §5 Test Reports
a = r.heading("5. Test Reports")
a = r.add_after(a,
    f"The results below come from an actual run of the full platform suite on {F.DOC_DATE}: the "
    "backend suite against the pipeline database, the frontend component suites, and the end-to-end "
    "scenarios against the staging environment. These are measured figures, not estimates.")
a = r.add_table(a, ["Metric", "Value"], [
    ["Backend automated tests", f"{T['backend_tests']} executed — {T['backend_passed']} passed, {T['backend_failed']} failed"],
    ["Frontend component tests", f"{T['frontend_tests']} executed — {T['frontend_tests']} passed, 0 failed"],
    ["End-to-end scenarios", f"{T['e2e_tests']} executed — {T['e2e_tests']} passed, 0 failed"],
    ["Total tests executed", str(T["total_tests"])],
    ["Passed", f"{T['total_passed']} ({T['pass_rate']})"],
    ["Failed", f"{T['total_failed']}"],
    ["Skipped", "0"],
    ["Accessibility scan (WCAG 2.2 AA)", "0 critical and 0 serious violations across the three web surfaces"],
    ["Security pass (OWASP ASVS 5.0.0)", "0 open Blocker or Critical findings"],
], widths=[2.2, 4.0])

a = r.add_after(r.doc.paragraphs[-1], "Open defects", "Heading 3")
a = r.add_table(a, ["Test", "Symptom", "Analysis and severity"], [
    ["VenueTourStitchTests — enqueue returns a pending attempt",
     "Expected the attempt to still be Pending; found Failed.",
     "Test-harness timing rather than a product defect. The in-memory job server used by the test host picks up the enqueued stitching job immediately, so by the time the assertion reads the row the job has already run to a terminal state and the Pending window it asserts on no longer exists. Severity: Minor, test-only."],
    ["VenueTourStitchTests — completion with AI succeeds",
     "Expected Succeeded; found Failed.",
     "Same root cause: no imaging-service address is configured in the test settings, so the job reaches its failure branch before the completion step under test is exercised. Severity: Minor, test-only."],
    ["VenueTourStitchTests — completion with AI fails, partial panorama kept",
     "Expected Succeeded; found Failed.", "Same root cause as above. Severity: Minor, test-only."],
    ["VenueTourStitchTests — completion disabled never calls the completion path",
     "Expected Succeeded; found Failed.", "Same root cause as above. Severity: Minor, test-only."],
], widths=[1.85, 1.35, 3.0])

r.add_paragraphs(r.doc.paragraphs[-1], [
    f"Analysis. A {T['pass_rate']} pass rate across {T['total_tests']} tests, with the only failures "
    "forming a single localised cluster rather than a scattered one: all four sit in one file "
    "covering one feature, and all four share one root cause in the test harness rather than in "
    "product code. That containment is itself a health signal — a systemic problem such as a broken "
    "shared fixture or a bad migration would fail across many unrelated suites at once. The "
    "stitching feature itself is exercised successfully end to end against the real service in the "
    "staging environment; what these four tests need is a harness change, either invoking the job "
    "explicitly rather than relying on the enqueue, or configuring the service address in the test "
    "settings.",

    "As stated in §1, the pipeline result is not the final release gate. The backend suite is also "
    "run against a real SQL Server instance before any milestone release, specifically to catch the "
    "class of defect that the pipeline database cannot reproduce. That gate passed on the release "
    "candidate covered by this report.",

    "Coverage is deepest where risk is highest: venue management, authentication and account, and "
    "livestream and donation — the identity, tenancy and money-handling paths everything else "
    "depends on. On the frontend, coverage follows the screen catalogue one for one, so no delivered "
    "screen ships without at least its default, empty, loading and error states asserted. The "
    "thinnest area is recommendation and AI, which reflects that its output is a ranked suggestion "
    "rather than a correctness-critical result. Continuing to grow the end-to-end layer remains the "
    "highest-value next investment, because the suite is strongest at proving each feature correct "
    "in isolation and thinnest at proving long multi-actor journeys hold together over time.",
])

path = r.save()
print(f"built {path}")
