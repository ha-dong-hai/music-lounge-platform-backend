"""UML 2.5.1 Use Case Diagrams, fixed coordinates, provably non-overlapping.

Layout contract
---------------
The defect in the auto-laid-out version was association lines cutting diagonally
across other use cases and across the group frames. Here the routing is a comb:

    actor ──┐
            │  one shared vertical trunk, left of every ellipse
            ├──────────▶ use case          each branch is a straight horizontal
            ├──────────▶ use case          run at that use case's own y, so it
            └──────────▶ use case          can only ever reach its own target

Use cases occupy a single column, so a branch at a given y meets exactly one
ellipse. «include» and «extend» dependencies are routed in a reserved channel to
the right of the column, again on their own y, and their labels sit beside the
vertical leg rather than on top of it.

Names are taken verbatim from spec/build/usecases.py; diagrams/validate.py fails the
build if any name here is not in that catalogue or if the ten diagrams together do
not cover all 109.

    Usage:  python diagrams/gen_usecases.py
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsl import (Diagram, ELLIPSE_TEXT_RATIO, TEXT_PAD, line_height,  # noqa: E402
                 text_size, wrapped_size)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_PNG = os.path.join(ROOT, "diagrams", "out")
OUT_DRAWIO = os.path.join(ROOT, "diagrams", "drawio")

# geometry. Every dimension that carries text is derived from the wrapped text
# rather than guessed, because a fixed size either clips the words or leaves the
# shape mostly empty — the first attempt did both.
ACTOR_X = 46
TRUNK_GAP = 120           # actor to the shared vertical trunk
BRANCH = 74               # trunk to the left edge of a use case
UC_W = 330                # ellipse width; height follows from the wrapped label
UC_MIN_H = 58
GAP_Y = 22                # clear space between two stacked use cases
GROUP_GAP = 30            # extra space before a group heading
HEAD_H = 24
TOP = 106                 # leaves the title clear of the system boundary frame
BOTTOM_PAD = 78           # must clear the boundary frame, which ends 46 below content
CHANNEL_GAP = 54          # first dependency channel, right of the column
# Each dependency gets its own vertical channel. The pitch must exceed the widest
# stereotype label ("<<extend>>" is ~78px at 12pt) plus a gap, or two adjacent
# labels collide — which the geometry check catches rather than letting it ship.
CHANNEL_PITCH = 96


def uc_height(name: str) -> float:
    _, th = wrapped_size(name, UC_W * ELLIPSE_TEXT_RATIO, 13)
    return max(UC_MIN_H, th + 34)


def build(spec: dict) -> Diagram:
    groups: list[tuple[str, list[str]]] = spec["groups"]
    rels: list[tuple[str, str, str]] = spec.get("relations", [])
    notes: list[str] = spec.get("notes", [])

    # ── horizontal frame, sized to the widest actor caption ─────────────────
    actor_w = max(text_size(a, 13)[0] for a in spec["actors"])
    trunk_x = ACTOR_X + max(46, actor_w) + TRUNK_GAP
    uc_x = trunk_x + BRANCH

    # ── vertical placement; each ellipse is as tall as its own wrapped label ─
    y = TOP
    rows: list[tuple[str, str, float, float]] = []   # (kind, text, y, h)
    uc_y: dict[str, float] = {}
    for gname, ucs in groups:
        if gname:
            y += GROUP_GAP
            rows.append(("head", gname, y, HEAD_H))
            y += HEAD_H + 8
        for uc in ucs:
            h = uc_height(uc)
            rows.append(("uc", uc, y, h))
            uc_y[uc] = y + h / 2
            y += h + GAP_Y
    content_bottom = y - GAP_Y

    n_channels = len(rels)
    right_edge = uc_x + UC_W + (CHANNEL_GAP + n_channels * CHANNEL_PITCH if n_channels else 40)
    width = max(right_edge + 60,
                Diagram(spec["name"], 10, 10).title_width(spec["title"]))

    note_w = width - uc_x - 60
    note_hs = [wrapped_size(n, note_w - 2 * TEXT_PAD - 6, 13)[1] + 2 * TEXT_PAD + 6
               for n in notes]
    height = content_bottom + BOTTOM_PAD + sum(h + 14 for h in note_hs) + 30

    d = Diagram(spec["name"], int(width), int(height))
    d.title(spec["title"])

    # system boundary — decorative, so branches may cross its border legitimately
    d.package("boundary", uc_x - 30, TOP - 44, right_edge - uc_x + 60,
              content_bottom - TOP + 88, spec.get("boundary", "MusicLounge Platform"))

    for kind, text, ry, rh in rows:
        if kind == "head":
            d.label(f"h_{text}", uc_x, ry, UC_W, rh, text, font_size=13,
                    bold=True, align="left")
        else:
            d.ellipse(f"uc_{text}", uc_x, ry, UC_W, rh, text, font_size=13)

    # ── actors ──────────────────────────────────────────────────────────────
    mid = (TOP + content_bottom) / 2
    actors = spec["actors"]
    primary = actors[-1]
    AH = Diagram.ACTOR_H
    if len(actors) == 2:                       # generalisation: parent above child
        parent_y = mid - AH - 70
        main_y = mid
        d.actor("a_parent", ACTOR_X, parent_y, actors[0])
        d.actor("a_main", ACTOR_X, main_y, primary)
        # Actor generalisation. UML 2.5.1 requires a CLOSED HOLLOW TRIANGLE pointing
        # at the more general actor — an open arrowhead would mean a dependency.
        d.edge([(ACTOR_X + 23, main_y), (ACTOR_X + 23, parent_y + AH)],
               end_arrow="triangle", attached=("a_main", "a_parent"))
    else:
        main_y = mid - AH / 2
        d.actor("a_main", ACTOR_X, main_y, primary)
    actor_cy = main_y + Diagram.ACTOR_ARM_DY

    d.edge([(ACTOR_X + max(46, actor_w), actor_cy), (trunk_x, actor_cy)],
           end_arrow="none", attached=("a_main",))
    for uc in uc_y:
        d.edge([(trunk_x, actor_cy), (trunk_x, uc_y[uc]), (uc_x, uc_y[uc])],
               end_arrow="open", attached=(f"uc_{uc}", "a_main"))

    # ── include / extend dependencies ───────────────────────────────────────
    # Two dependencies that share an endpoint would otherwise arrive on exactly the
    # same y and their last legs would be drawn as one line. Each gets its own
    # attachment offset on the shared use case so both remain traceable.
    from collections import defaultdict
    slots: dict[str, list[int]] = defaultdict(list)
    for i, (src, dst, _) in enumerate(rels):
        slots[src].append(i)
        slots[dst].append(i)

    def att_y(uc: str, i: int) -> float:
        group = slots[uc]
        k = group.index(i)
        return uc_y[uc] + (k - (len(group) - 1) / 2) * 11

    for i, (src, dst, kind) in enumerate(rels):
        cx = uc_x + UC_W + CHANNEL_GAP + i * CHANNEL_PITCH
        y1, y2 = att_y(src, i), att_y(dst, i)
        d.edge([(uc_x + UC_W, y1), (cx, y1), (cx, y2), (uc_x + UC_W, y2)],
               label=f"<<{kind}>>", style="dashed", end_arrow="open",
               attached=(f"uc_{src}", f"uc_{dst}"),
               label_pos=1, label_side="right")

    ny = content_bottom + BOTTOM_PAD
    for j, (n, nh) in enumerate(zip(notes, note_hs)):
        d.note(f"note{j}", uc_x - 30, ny, note_w, nh, n, font_size=13)
        ny += nh + 14
    return d


# ── the ten diagrams; names verbatim from usecases.py ───────────────────────
SPECS = [
    dict(name="uc-guest", title="Use Case Diagram — Guest", actors=["Guest"],
         groups=[("Discovery", ["Browse shows", "Search and filter shows",
                                "View show detail", "View venue detail",
                                "Explore 360° virtual tour", "View performer profile"]),
                 ("Donation transparency", ["View public donation ticker",
                                            "View donation transparency feed"]),
                 ("Complaints", ["File a complaint", "Look up a complaint"]),
                 ("Account", ["Register account", "Verify email",
                              "Sign in with Google", "Recover password"])],
         relations=[("Register account", "Verify email", "include"),
                    ("Search and filter shows", "Browse shows", "extend"),
                    ("Explore 360° virtual tour", "View venue detail", "extend")],
         notes=["Only Guest and Owner accounts self-register. Staff is provisioned by an "
                "Owner and Admin directly in the database."]),

    dict(name="uc-audience-account", title="Use Case Diagram — Audience (Account and Discovery)",
         actors=["Guest", "Audience"],
         groups=[("Account and identity", ["Sign in", "Sign out", "Verify phone number",
                                           "Edit profile", "Set AI preferences",
                                           "Export my data", "Erase my data"]),
                 ("Discovery and engagement", ["Follow a venue", "Add show to wishlist",
                                               "View personalised recommendations",
                                               "Rate a show"]),
                 ("Notifications", ["Receive notifications", "Register device for push"])],
         relations=[("View personalised recommendations", "Set AI preferences", "extend")],
         notes=["Audience specialises Guest, so every Guest use case is also available "
                "here and is not repeated — see uc-guest.",
                "Erasure anonymises the account in place; legally retained financial "
                "records survive."]),

    dict(name="uc-audience-transaction",
         title="Use Case Diagram — Audience (Ticketing, Livestream and F&B)",
         actors=["Audience"],
         groups=[("Ticketing", ["Hold tickets", "Pay for a ticket", "Cancel a hold",
                                "View my tickets", "View ticket QR", "Transfer a ticket",
                                "Accept a transferred ticket", "Cancel a ticket",
                                "Request a refund"]),
                 ("Livestream and donation", ["Watch a livestream", "Send chat message",
                                              "Donate to a performer", "Set donation privacy",
                                              "View my donations"]),
                 ("Food and beverage", ["Browse menu", "Place an F&B order",
                                        "Track my order"])],
         relations=[("Pay for a ticket", "Hold tickets", "extend"),
                    ("Cancel a hold", "Hold tickets", "extend"),
                    ("View ticket QR", "View my tickets", "extend"),
                    ("Request a refund", "Cancel a ticket", "extend"),
                    ("Donate to a performer", "Set donation privacy", "include")],
         notes=["Payment extends the hold rather than being included by it: a hold may "
                "simply expire, so payment is not guaranteed to run."]),

    dict(name="uc-owner-venue", title="Use Case Diagram — Owner (Venue Management)",
         actors=["Owner"],
         groups=[("Venue profile", ["Register a venue", "Edit venue profile",
                                    "Manage venue gallery", "Define custom criteria"]),
                 ("Seating and virtual tour", ["Create seating zone", "Arrange seating layout",
                                               "Upload panorama scene", "Auto-stitch panorama",
                                               "Place tour hotspot"]),
                 ("People and payout", ["Assign staff", "Deactivate staff",
                                        "Register bank account", "Upload identity document"])],
         relations=[("Auto-stitch panorama", "Upload panorama scene", "extend"),
                    ("Arrange seating layout", "Create seating zone", "extend")],
         notes=["A newly registered venue is Pending and stays non-public until an Admin "
                "approves it. A payout account must be verified by an Admin before any "
                "settlement is released to it."]),

    dict(name="uc-owner-show", title="Use Case Diagram — Owner (Show Management)",
         actors=["Owner"],
         groups=[("Show authoring", ["Create a show", "Build performer line-up",
                                     "Create performer profile", "Upload show poster",
                                     "Generate show poster with AI"]),
                 ("Pricing", ["Define ticket tier", "Define pricing window"]),
                 ("Publication", ["Declare legal permit", "Submit show for review"]),
                 ("Changes after publication", ["Reschedule a show", "Change show format",
                                                "Cancel a show"]),
                 ("Venue catering", ["Manage F&B menu"])],
         relations=[("Build performer line-up", "Create performer profile", "include"),
                    ("Define ticket tier", "Define pricing window", "include"),
                    ("Submit show for review", "Declare legal permit", "include"),
                    ("Generate show poster with AI", "Upload show poster", "extend")],
         notes=["Submission is refused unless the venue is approved, the subscription is "
                "active, and a ticket tier and legal permit are present. The Admin review "
                "that follows is a separate use case the Admin initiates."]),

    dict(name="uc-owner-finance",
         title="Use Case Diagram — Owner (Broadcast, Donation and Finance)",
         actors=["Owner"],
         groups=[("Broadcast", ["Start broadcast", "End broadcast", "View viewer count"]),
                 ("Donation handling", ["Acknowledge a donation", "Confirm performer payout"]),
                 ("Counter sales", ["Sell a walk-in ticket"]),
                 ("Finance", ["View earnings"]),
                 ("Subscription", ["View subscription plans", "Subscribe to a plan",
                                   "Renew a subscription", "Cancel a subscription"]),
                 ("Penalty", ["Appeal a penalty"])],
         relations=[("Confirm performer payout", "Acknowledge a donation", "extend"),
                    ("Subscribe to a plan", "View subscription plans", "extend")],
         notes=["Broadcast control and walk-in sales are shared with Staff — see uc-staff.",
                "A payout cannot be recorded unless the performer has a default bank "
                "account, and the share rate is frozen when the donation is confirmed."]),

    dict(name="uc-staff", title="Use Case Diagram — Staff", actors=["Staff"],
         groups=[("Box office", ["Sell a walk-in ticket", "Check in a ticket"]),
                 ("Broadcast", ["Start broadcast", "End broadcast", "View viewer count"]),
                 ("Food and beverage", ["Advance order status", "Cancel an order"]),
                 ("Account", ["Sign in", "Sign out", "Edit profile", "Export my data",
                              "Erase my data", "Receive notifications",
                              "Register device for push"])],
         notes=["Staff is assigned by an Owner to exactly one venue and never "
                "self-registers. A walk-in sale is taken in cash at the counter, so it "
                "does not pass through the payment gateway."]),

    dict(name="uc-admin-moderation",
         title="Use Case Diagram — Admin (Moderation and Compliance)", actors=["Admin"],
         groups=[("Content review", ["Approve or reject a venue", "Review a show",
                                     "Review a livestream", "Force-stop a livestream"]),
                 ("Enforcement", ["Issue a venue penalty", "Review an appeal"]),
                 ("Complaints", ["Resolve a complaint"])],
         notes=["Review is advised by an AI risk score but never decided by it: an "
                "unavailable scoring service yields a neutral score and the item still "
                "reaches the queue for a human decision. The moderation SLA is 24 hours.",
                "Admin accounts are provisioned directly in the database. There is "
                "deliberately no self-service registration path for this role."]),

    dict(name="uc-admin-platform",
         title="Use Case Diagram — Admin (Finance and Platform Administration)",
         actors=["Admin"],
         groups=[("Financial oversight", ["Process a refund request", "Reverse a donation",
                                          "Verify a bank account", "Check ledger integrity"]),
                 ("Catalogue", ["Manage subscription plans", "Manage taxonomy"]),
                 ("Back office", ["Manage users", "View platform analytics",
                                  "Monitor background jobs"])],
         notes=["Verifying a bank account gates settlement: an unverified payout account "
                "blocks the release of funds rather than failing the original charge.",
                "Checking ledger integrity confirms every journal balances, that is, "
                "total debits equal total credits."]),

    dict(name="uc-system", title="Use Case Diagram — System (Automated and Scheduled)",
         actors=["System"],
         groups=[("Payment and ledger", ["Process gateway callback", "Post ledger journal",
                                         "Release settlement tranche",
                                         "Reconcile with gateway"]),
                 ("Moderation support", ["Score content with AI"]),
                 ("Security monitoring", ["Detect login anomaly", "Detect admin drift"]),
                 ("Housekeeping", ["Prune stale device tokens"])],
         relations=[("Process gateway callback", "Post ledger journal", "include")],
         notes=["These use cases are initiated by a schedule or by an inbound gateway "
                "call, not by a person. They run as Hangfire jobs or webhook handlers.",
                "The server-to-server callback is the authoritative payment confirmation, "
                "not the browser redirect, and the handler is idempotent because the "
                "gateway retries."]),
]


def main() -> int:
    failed = 0
    for spec in SPECS:
        d = build(spec)
        problems = d.validate()
        if problems:
            failed += 1
            print(f"{spec['name']}: {len(problems)} geometry problem(s)")
            for p in problems[:8]:
                print("   ", p)
        else:
            d.save_png(OUT_PNG)
            d.save_drawio(OUT_DRAWIO)
            print(f"{spec['name']:26} clean  {d.width}x{d.height}")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
