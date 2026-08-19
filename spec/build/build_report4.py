"""Report 4 — Software Design Document, built fresh from the pristine template."""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from docxkit import Report
import facts as F

DIA = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "diagrams", "out"))
S = F.SCALE

r = Report("Report4_Software Design Document.docx",
           "Report4_Software Design Document - MusicLounge.docx")

# ── PASS 1 ───────────────────────────────────────────────────────────────────
r.clear_regions(
    ("1. System Design", "1.1 System Architecture"),
    ("1.1 System Architecture", "1.2 Package Diagram"),
    ("1.2 Package Diagram", "2. Database Design"),
    ("2. Database Design", "3. Detailed Design"),
    ("3. Detailed Design", "3.1 <Feature/Function Name1>"),
    ("3.1 <Feature/Function Name1>", "3.1.1 Class Diagram"),
    ("3.1.1 Class Diagram", None),
)

# ── PASS 2 ───────────────────────────────────────────────────────────────────
r.record_of_changes([
    [F.DOC_DATE, "A", F.TEAM[0]["name"],
     f"Software Design Document for the complete platform: backend layering, frontend structure, "
     f"deployment on {F.STACK['cloud']}, the {S['entities']}-entity data model and the detailed "
     f"design of the principal flows."],
])

# §1.1 System Architecture
a = r.heading("1.1 System Architecture")
a = r.add_paragraphs(a, [
    "The backend follows Clean Architecture across four projects with a strict inward dependency "
    "rule — Api depends on Application and, at the composition root only, on Infrastructure; "
    "Infrastructure depends on Application; Application depends on Domain; and Domain depends on "
    "nothing. The rule is enforced at the project-reference level rather than by convention, so a "
    "violation fails the build instead of passing review.",

    "Domain holds entities, enumerations, domain events and exceptions as plain C# with no "
    "framework reference, which is what makes every business rule in it testable with no database "
    "and no host. Application holds all business logic as command and query handlers, one folder "
    f"per business area ({S['feature_folders']} of them), with cross-cutting concerns — validation, "
    "transaction boundary, logging — implemented once as pipeline behaviours rather than repeated "
    "in every handler. Infrastructure implements every interface Application declares but does not "
    "itself implement: the database context and repositories, an adapter per external service, and "
    f"the {S['job_classes']} background job classes. Api is deliberately the thinnest layer: "
    f"{S['controllers']} controllers exposing about {S['endpoints']} endpoints that authenticate and "
    "authorise the caller, map the request onto a command or query, and return the result — no "
    "business logic lives in a controller.",

    "This split exists for two concrete reasons rather than architectural fashion. First, Domain and "
    "Application can be tested with no database, no HTTP host and no external service running. "
    "Second, an external integration can be replaced without touching a single handler, because "
    "Application depends only on the interface and never on the concrete adapter — which is how the "
    "SMS provider was swapped without changing business code.",

    f"All {S['surfaces']} client surfaces sit outside the backend entirely and consume only the Api "
    "layer, over REST and WebSocket. No client ever reaches Infrastructure, Application or Domain "
    "directly.",
])
a = r.add_figure(a, os.path.join(DIA, "architecture-layers.png"),
                 "Layered architecture — an outer layer may depend on an inner one, never the reverse")

a = r.add_paragraphs(a, [
    "The five client applications are separate deliverables but not five unrelated codebases: they "
    "share one design system, one API client generated from the API's own contract, and one set of "
    "cross-cutting conventions, so a contract change propagates to every consumer as a compile "
    "error rather than a runtime failure in one of them. The three web surfaces are React with "
    "TypeScript; the two mobile surfaces are React Native sharing the same models and client.",

    "Within each surface the layering is the same. Screens and routes are the only layer that knows "
    "about navigation; a route guard resolves the caller's role and redirects before rendering, "
    "which is a usability measure and never the security boundary, since the API re-checks every "
    "request independently. Feature modules mirror the backend's folders one for one, so a developer "
    "tracing a defect moves between tiers without learning a second vocabulary. State is split "
    "deliberately: anything the API owns lives in the query library, which handles caching, retry "
    "and invalidation, and only genuinely client-owned state lives in a local store.",
])
a = r.add_figure(a, os.path.join(DIA, "frontend-architecture.png"),
                 "Frontend architecture — the structure shared by all five client surfaces")

a = r.add_paragraphs(a, [
    f"The platform is deployed to {F.STACK['cloud']}. The three web bundles are served as static "
    "assets from the edge, so a page load costs the API nothing. The API and its real-time hubs run "
    "on an application host with the background worker in the same process, behind a staging slot "
    "that is warmed and smoke-tested before being swapped into production — which makes a rollback "
    "a swap back rather than a redeploy. The imaging microservice runs separately because its "
    "workload is different in kind: bursty, processor-bound, and dependent on native binaries, so "
    "it scales on its own and a stitching failure cannot destabilise the API host.",

    "Uploaded media is stored in blob storage and served through a content delivery network, keeping "
    "image traffic off the API entirely. Every secret is held in a managed vault and injected at "
    "deploy time, so no secret exists in source control or inside a build artefact. Telemetry, "
    "background-job outcomes and alert rules are collected centrally.",
])
a = r.add_figure(a, os.path.join(DIA, "deployment.png"),
                 "Deployment architecture — nodes, the artefacts deployed on them, and the protocols between them")

# §1.2 Package Diagram
a = r.heading("1.2 Package Diagram")
a = r.add_after(a,
    "The table below describes every package in the four backend projects, using dotted "
    "Project.Package notation. The frontend's internal structure is covered above rather than "
    "repeated here.")
r.add_table(a, ["No", "Package", "Description"], [
    ["1", "Domain.Entities", f"The {S['entities']} entity classes that make up the business data model."],
    ["2", "Domain.Enums", "Closed sets of values — statuses, roles, payment and moderation states — persisted as strings so the database stays readable."],
    ["3", "Domain.Events", "Domain events raised by an entity when something business-significant happens, handled asynchronously."],
    ["4", "Domain.Exceptions", "Business exception types that map to specific HTTP responses at the boundary."],
    ["5", "Domain.Common", "Shared base types, principally the entity base with its identifier and audit fields."],
    ["6", "Application.<Feature>", f"One folder per business area ({S['feature_folders']} in total), each holding its commands, queries, handlers, validators and data-transfer objects."],
    ["7", "Application.Common.Abstractions", "The command and query marker interfaces the mediator dispatches on."],
    ["8", "Application.Common.Interfaces", "Every capability Application needs but does not implement: unit of work, repositories, payment, livestream, messaging, AI and imaging services."],
    ["9", "Application.Common.Behaviors", "Pipeline behaviours that wrap every handler: validation, transaction boundary and logging."],
    ["10", "Application.Common.Models", "Shared result and paging shapes returned across features."],
    ["11", "Infrastructure.Persistence", f"The database context exposing the entity sets, one configuration class per entity, the migration history, and the concrete repository and unit-of-work implementations."],
    ["12", "Infrastructure.Services", "One adapter per external system — payment, livestream, AI, push, SMS, email and imaging — each implementing an Application interface."],
    ["13", "Infrastructure.Jobs", f"The {S['job_classes']} background job classes, {S['recurring_jobs']} of which run on a recurring schedule."],
    ["14", "Infrastructure.Security", "Token issuing, password hashing, and the protection of sensitive stored fields."],
    ["15", "Api.Controllers", f"The {S['controllers']} controllers exposing roughly {S['endpoints']} endpoints."],
    ["16", "Api.Hubs", "Real-time hubs for livestream chat, access gating and the public donation ticker."],
    ["17", "Api.Authorization", "Policy definitions and the venue-scoping requirement handlers."],
    ["18", "Api.Middleware", "Global exception handling, request logging and security headers."],
], widths=[0.45, 1.75, 4.0])

# §2 Database Design
a = r.heading("2. Database Design")
a = r.add_paragraphs(a, [
    f"{S['entities']} entities are mapped to SQL Server tables entirely through Code-First "
    "migrations; there is no hand-written schema script, so the schema in any environment is "
    "whatever the committed migration history says it is.",

    "Conventions applied consistently across the schema. Identifiers are integer identity columns, "
    "except Ticket, whose key is a server-generated sequential globally unique identifier so a "
    "ticket cannot be found by guessing a number. Enumerations are stored as their string names "
    "rather than ordinals, so a value stays readable in the database and inserting a new member "
    "cannot silently renumber existing rows. Money is stored as a fixed-precision decimal, never a "
    "floating-point type. Timestamps are stored with their offset. Deletes are restricted by "
    "default; a cascade is only configured where the child genuinely has no meaning without its "
    "parent, which also avoids the multiple-cascade-path restriction on the real engine.",

    "The relationships between the core entities are shown in Report 3 §3.1.5. The remaining tables "
    "fall into four groups: taxonomy tags and the join tables that attach them to shows, performers "
    "and user preferences; financial records — accounts, ledger entries, payments, settlements, "
    "refunds and bank accounts; operational logs for sign-in failures, push failures and behaviour "
    "events, each with its own retention rule; and configuration, where every business-tunable "
    "number lives with an audit trail of changes.",
])

# §3 Detailed Design
a = r.heading("3. Detailed Design")
r.add_after(a,
    "Every feature repeats the same class structure, so it is documented once in §3.1 rather than "
    "redrawn per feature. The sections after it give the sequence of the flows where the "
    "interaction, not the class shape, is what carries the design risk.")

a = r.heading("3.1 <Feature/Function Name1>")
a.runs[0].text = "3.1 Common Structure — Command, Handler and Pipeline"
for extra in a.runs[1:]:
    extra._element.getparent().remove(extra._element)
r.add_after(a,
    "A controller action does nothing beyond authorisation and mapping: it builds a command or "
    "query and hands it to the mediator. A validator declared alongside the command is run by a "
    "pipeline behaviour before the handler is reached, so a handler never begins with a block of "
    "argument checking. The handler depends only on interfaces — the unit of work, and a specific "
    "repository where a query needs eager loading — so it can be exercised without a database. A "
    "transaction behaviour opens and commits around the handler, which is why a handler never "
    "manages a transaction itself and why a failure part-way cannot leave a half-written change.")

a = r.heading("3.1.1 Class Diagram")
a = r.add_figure(a, os.path.join(DIA, "class-cqrs.png"),
                 "Common CQRS class structure repeated by every feature")

SEQS = [
    ("3.2 Ticket Purchase", "seq-ticket-purchase.png",
     "Ticket purchase — hold, pay, and confirm through the gateway callback",
     "This is the flow with the most concurrency risk in the system, so it is designed around two "
     "decisions. Capacity is decremented when the hold is taken rather than when payment completes, "
     "which is what prevents the same seat being sold twice during the payment window. And the "
     "gateway's server-to-server callback, not the browser redirect, is treated as the authoritative "
     "confirmation — so the ticket still confirms if the customer closes the tab, and the handler "
     "must be idempotent because the gateway retries until it receives the response it expects."),
    ("3.3 Donation and Performer Payout", "seq-donation.png",
     "Donation — tip a performer, then owner acknowledgement and payout",
     "A tip is not a single transfer but a pipeline that can span days, and the design makes each "
     "stage explicit rather than hiding the delay. At confirmation the platform commission and tax "
     "are posted immediately and the performer's share rate is frozen onto the donation, so an "
     "administrator adjusting the rate afterwards cannot change what an existing donation already "
     "promised. The final payout is refused outright if the performer has no registered bank "
     "account, because recording a payout with no destination would leave the ledger claiming money "
     "moved somewhere that does not exist."),
    ("3.4 Content Moderation", "seq-moderation.png",
     "Show moderation — submit, AI scoring, and the Admin decision",
     "The design deliberately separates advice from authority. Scoring runs as a background job so "
     "a slow or unavailable AI service cannot block a submission, and its output is a score and a "
     "recommendation that an Admin may follow or ignore. If the service fails the item still enters "
     "the queue with a neutral score, because the failure mode that matters here is content never "
     "reaching review at all — not content reaching review without a score."),
]
for heading, image, caption, prose in SEQS:
    a = r.add_after(a, heading, "Heading 3")
    a = r.add_after(a, prose)
    a = r.add_figure(a, os.path.join(DIA, image), caption)

path = r.save()
print(f"built {path}")
