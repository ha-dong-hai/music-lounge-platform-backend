"""Screen Flow diagrams — UML 2.5.1 State Machine, fixed coordinates.

Notation (criteria group A in STANDARDS.md)
-------------------------------------------
A screen flow is modelled as a state machine: each screen is a state and each
navigation is a transition labelled with the action that triggers it. A state machine
rather than an activity diagram, because a screen is a condition the interface rests
in, not a step that runs to completion. A self-transition is a navigation that leaves
the user on the same screen.

Why these are not PlantUML
--------------------------
They were, and it went badly: on the Owner flow, PlantUML stacked two states on top of
each other so their captions printed over one another as
"confiDonatiopnsyAwaitingtPaidoute", dropped explanatory notes across live connectors,
and crossed two transition captions. A screen flow has too many transitions per state
for an auto-layout engine to keep clear.

Layout (criteria group C)
-------------------------
Screens run down one column; every transition is routed in the open field beside them,
each with a vertical channel of its own. Because no screen sits in that field, no
connector can cross a screen, and because screens never overlap vertically, every
connection point has a y of its own. Labels are then placed by searching for the first
position that touches nothing — with thirty transitions there is no single offset that
works for all of them.

    Usage:  python diagrams/gen_flows.py
"""

from __future__ import annotations

import os
import sys
from collections import defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsl import Diagram, Rect, TEXT_PAD, wrapped_size  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_PNG = os.path.join(ROOT, "diagrams", "out")
OUT_DRAWIO = os.path.join(ROOT, "diagrams", "drawio")

BW = 300
SLOT = 15
ROW_GAP = 46
CHANNEL = 40
LOOP = 34            # how far a self-transition bulges out
LEFT = 60
TOP = 130

FLOWS = {
    "audience": {
        "title": "Screen Flow — Audience",
        "entry": "SignUp",
        "screens": {
            "SignUp": "Sign Up", "OTP": "Verify Email OTP", "SignIn": "Sign In",
            "Reset": "Reset Password", "ShowList": "Show List and Search",
            "ShowDetail": "Show Detail", "VenueDetail": "Venue Detail",
            "Tour": "360 Virtual Tour", "Recos": "Recommendations",
            "Hold": "Select Tickets and Hold", "PayResult": "Payment Result",
            "MyTickets": "My Tickets", "TicketDetail": "Ticket Detail and QR",
            "Live": "Livestream Room", "Donate": "Donate Form",
            "MyDonations": "My Donations", "Menu": "F&B Menu",
            "MyOrders": "My F&B Orders", "Rate": "Rate Show",
            "Complaint": "File Complaint", "Notif": "Notifications",
            "Profile": "Profile and Privacy",
        },
        "trans": [
            ("SignUp", "OTP", "submit registration"),
            ("OTP", "ShowList", "correct code"),
            ("OTP", "OTP", "wrong or expired code, resend"),
            ("SignIn", "ShowList", "sign in"),
            ("SignIn", "Reset", "forgot password"),
            ("Reset", "SignIn", "password set"),
            ("ShowList", "ShowDetail", "pick a show"),
            ("ShowList", "Recos", "open recommendations"),
            ("Recos", "ShowDetail", "pick a suggestion"),
            ("ShowDetail", "VenueDetail", "view the venue"),
            ("VenueDetail", "Tour", "explore the tour"),
            ("ShowDetail", "Hold", "buy tickets"),
            ("Hold", "Hold", "cancel, seats released"),
            ("Hold", "PayResult", "pay through the gateway"),
            ("PayResult", "TicketDetail", "payment succeeded"),
            ("PayResult", "Hold", "payment failed, hold again"),
            ("MyTickets", "TicketDetail", "open a ticket"),
            ("TicketDetail", "MyTickets", "transfer or cancel"),
            ("ShowDetail", "Live", "show Ongoing, ticket held"),
            ("Live", "Donate", "tip a performer"),
            ("Donate", "PayResult", "pay through the gateway"),
            ("PayResult", "Live", "back to the stream"),
            ("PayResult", "MyDonations", "view donation history"),
            ("VenueDetail", "Menu", "browse the menu"),
            ("ShowDetail", "Menu", "browse the menu"),
            ("Menu", "MyOrders", "place an order"),
            ("ShowDetail", "Rate", "show has Ended"),
            ("ShowDetail", "Complaint", "report a problem"),
        ],
        "note": ("Notifications and Profile are reachable from the account menu at any "
                 "point, so they carry no incoming transition rather than repeating an "
                 "edge from every screen. My F&B Orders changes as Staff advance the "
                 "order; there is no push, so the screen must be reopened to see it."),
    },
    "owner": {
        "title": "Screen Flow — Owner",
        "entry": "SignUp",
        "screens": {
            "SignUp": "Sign Up as Owner", "OTP": "Verify Email OTP",
            "Venues": "My Venues", "VenueEdit": "Create or Edit Venue",
            "Bank": "Bank Accounts", "Zones": "Seating Zone Editor",
            "Staff": "Staff Management", "Tour": "360 Tour Manager",
            "Gallery": "Venue Gallery and Criteria", "Menu": "F&B Menu Manager",
            "Orders": "F&B Order Board", "Plans": "Subscription Plans",
            "MySub": "My Subscription", "PayResult": "Payment Result",
            "Shows": "My Shows", "ShowEdit": "Create or Edit Show",
            "ShowPanel": "Show Control Panel", "Tiers": "Ticket Tier Manager",
            "Poster": "Show Poster", "Legal": "Legal and Royalty Declaration",
            "LiveOps": "Livestream Operation",
            "DonateAck": "Donations Awaiting Acknowledgement",
            "DonatePay": "Donations Awaiting Payout", "Earnings": "Earnings Overview",
            "Analytics": "Venue Analytics", "Penalty": "My Penalties and Appeals",
        },
        "trans": [
            ("SignUp", "OTP", "submit registration"),
            ("OTP", "Venues", "correct code"),
            ("Venues", "Bank", "register a payout account before trading"),
            ("Venues", "VenueEdit", "create a venue"),
            ("VenueEdit", "Venues", "submit, venue becomes Pending"),
            ("Venues", "Zones", "venue approved"),
            ("Zones", "Staff", "assign staff"),
            ("Staff", "Tour", "build the virtual tour"),
            ("Tour", "Gallery", "add gallery and criteria"),
            ("Gallery", "Menu", "set up the menu"),
            ("Menu", "Orders", "work the counter"),
            ("Venues", "Plans", "subscribe"),
            ("Plans", "MySub", "choose a plan"),
            ("MySub", "PayResult", "pay through the gateway"),
            ("PayResult", "MySub", "subscription Active"),
            ("MySub", "Shows", "subscription unlocks shows"),
            ("Shows", "ShowEdit", "create a show"),
            ("ShowEdit", "ShowPanel", "saved as Draft"),
            ("ShowPanel", "Tiers", "define ticket tiers"),
            ("ShowPanel", "Poster", "generate or upload a poster"),
            ("ShowPanel", "Legal", "declare permit and royalty"),
            ("ShowPanel", "ShowPanel", "submit for review, reschedule, cancel"),
            ("ShowPanel", "ShowEdit", "rejected, returns to Draft"),
            ("ShowPanel", "LiveOps", "create a livestream"),
            ("LiveOps", "LiveOps", "start and end the broadcast"),
            ("LiveOps", "ShowPanel", "broadcast ended"),
            ("DonateAck", "DonatePay", "acknowledge within 24 hours"),
            ("DonatePay", "Bank", "performer has no bank account"),
            ("DonatePay", "DonatePay", "confirm the payout with evidence"),
            ("Earnings", "Analytics", "drill into one venue"),
            ("Earnings", "Bank", "settlement blocked, no verified account"),
        ],
        "note": ("Donations Awaiting Acknowledgement is reached passively when a "
                 "donation clears, not by navigation; if the Owner does not "
                 "acknowledge within 24 hours the system confirms it for them. "
                 "A rejected venue cannot be resubmitted — the Owner creates a new one."),
    },
    "admin": {
        "title": "Screen Flow — Admin",
        "entry": "SignIn",
        "screens": {
            "SignIn": "Sign In", "VenueQueue": "Venue Approval Queue",
            "ModQueue": "Show and Livestream Moderation", "LiveRoom": "Livestream Room",
            "Penalty": "Issue Venue Penalty", "Appeal": "Appeal Review",
            "Refunds": "Refund Requests", "DonateRefund": "Donation Refunds",
            "BankVerify": "Bank Account Verification",
            "Complaints": "Complaint Handling", "Users": "User Management",
            "Taxonomy": "Taxonomy Management", "Plans": "Subscription Plan Management",
            "Inbox": "Notifications Inbox", "Ledger": "Ledger Integrity Check",
            "Analytics": "Platform Analytics", "Jobs": "Background Job Dashboard",
        },
        "trans": [
            ("SignIn", "VenueQueue", "open the approval queue"),
            ("SignIn", "ModQueue", "open the moderation queue"),
            ("SignIn", "Inbox", "open notifications"),
            ("VenueQueue", "VenueQueue", "approve, or reject with a reason"),
            ("ModQueue", "ModQueue", "approve, or reject with a review note"),
            ("LiveRoom", "LiveRoom", "force-stop, disconnecting every viewer"),
            ("Penalty", "Appeal", "the Owner appeals"),
            ("Appeal", "Appeal", "uphold or overturn"),
            ("Complaints", "Refunds", "resolved as refund, target is a ticket"),
            ("Complaints", "DonateRefund", "resolved as refund, target is a donation"),
            ("Complaints", "Complaints", "take down content, or record the outcome"),
            ("Refunds", "Refunds", "approve in full or in part, or reject"),
            ("DonateRefund", "DonateRefund", "reverse before the performer is paid"),
            ("BankVerify", "BankVerify", "verify, releasing blocked settlements"),
            ("Users", "Users", "search, lock or unlock an account"),
            ("Taxonomy", "Taxonomy", "create, edit or delete a tag"),
            ("Plans", "Plans", "create or edit a plan"),
            ("Inbox", "Ledger", "open a ledger alert"),
            ("Inbox", "Analytics", "review platform activity"),
            ("Inbox", "Jobs", "inspect a failed job"),
            ("Jobs", "Ledger", "force a reconciliation run, then recheck"),
        ],
        "note": ("There is no Admin registration screen: Admin accounts are "
                 "provisioned directly in the database. Bank Account Verification is "
                 "reached passively — the Owner registers the account on their own "
                 "screen and it arrives in this queue. Only an appeal in the Appealed "
                 "state can be actioned; past the SLA the system overturns it "
                 "automatically, under the same lock so the two cannot collide."),
    },
}


def build(key: str, flow: dict) -> Diagram:
    screens: dict[str, str] = flow["screens"]
    trans: list[tuple[str, str, str]] = flow["trans"]

    degree: dict[str, int] = defaultdict(int)
    for a, b, _ in trans:
        degree[a] += 1
        if b != a:
            degree[b] += 1

    order = list(screens)                     # declaration order reads as the journey
    height_of = {n: max(56, 24 + degree[n] * SLOT) for n in order}

    pos: dict[str, tuple[float, float]] = {}
    y = TOP
    for name in order:
        pos[name] = (y, height_of[name])
        y += height_of[name] + ROW_GAP
    grid_bottom = y - ROW_GAP

    n_ch = max(1, len(trans))
    field_right = LEFT + BW + 60 + n_ch * CHANNEL
    width = max(field_right + 140, 1500)
    note_w = width - 2 * LEFT

    # The key: one numbered line per transition, split into two columns.
    key_lines = [f"{i}.  {screens[a]}  →  {screens[b]}  :  {t}"
                 for i, (a, b, t) in enumerate(trans, start=1)]
    half = (len(key_lines) + 1) // 2
    key_cols = ["\n".join(key_lines[:half]), "\n".join(key_lines[half:])]
    key_w = (note_w - 20) / 2
    key_h = max(wrapped_size(c, key_w - 2 * TEXT_PAD - 6, 12)[1] for c in key_cols) \
        + 2 * TEXT_PAD + 26

    _, nh = wrapped_size(flow["note"], note_w - 2 * TEXT_PAD - 6, 13)
    note_h = nh + 2 * TEXT_PAD + 20
    height = grid_bottom + 60 + key_h + 24 + note_h + 40

    d = Diagram(f"flow-{key}", int(width), int(height))
    d.title(flow["title"])

    for name, (ny, h) in pos.items():
        d.box(name, LEFT, ny, BW, h, screens[name], font_size=14, bold=True)

    # Initial pseudostate: a filled disc, drawn as a small ellipse left of the entry.
    ey, _ = pos[flow["entry"]]
    d.ellipse("__init__", LEFT - 40, ey + 12, 20, 20, "")
    d.edge([(LEFT - 20, ey + 22), (LEFT, ey + 22)], end_arrow="open",
           attached=("__init__", flow["entry"]))

    used: dict[str, int] = defaultdict(int)

    def anchor(name: str) -> float:
        ny, _h = pos[name]
        k = used[name]
        used[name] += 1
        return ny + 18 + k * SLOT

    # Pass 1: every connector, unlabelled. Labels are placed only once all connectors
    # exist — placing them as we go let an early label settle on a channel that had
    # not been drawn yet.
    right = LEFT + BW
    spans: list[tuple[float, float, float, str]] = []
    for i, (src, dst, text) in enumerate(trans):
        gx = right + 60 + i * CHANNEL
        y1, y2 = anchor(src), anchor(dst)      # a self-transition takes two slots
        d.edge([(right, y1), (gx, y1), (gx, y2), (right, y2)],
               end_arrow="open", attached=(src, dst))
        spans.append((gx, min(y1, y2), max(y1, y2), text))

    # Pass 2: number each connector rather than spelling the action out on it. With
    # around thirty transitions the channels sit 40px apart while the captions run to
    # 250px, so a caption always covers several neighbouring channels — there is no
    # position for it that is not ambiguous. A numeral always fits, and the key below
    # carries the wording in full.
    for n, (e, (gx, lo, hi, _text)) in enumerate(zip(d.edges[1:], spans), start=1):
        tag = str(n)
        placed = None
        for dx in (10, 26, 44, 64, 88):
            for t in (0.5, 0.34, 0.66, 0.2, 0.8, 0.1, 0.9, 0.04, 0.96):
                cy = lo + (hi - lo) * t
                probe = d.measure_label(tag, 0, cy)
                cand = Rect(gx + dx, probe.y, probe.w, probe.h)
                if cand.x2 < width - 16 and not d.label_collides(cand):
                    placed = cand
                    break
            if placed:
                break
        if placed is None:
            raise RuntimeError(f"flow-{key}: nowhere to put the tag for transition {n}")
        e.label, e.label_rect = tag, placed

    key_y = grid_bottom + 60
    d.label("keyhead", LEFT, key_y, note_w, 22,
            "Transitions — the number on each connector", font_size=13, bold=True)
    for ci, col in enumerate(key_cols):
        d.note(f"key{ci}", LEFT + ci * (key_w + 20), key_y + 26, key_w,
               key_h - 26, col, font_size=12)

    d.note("note", LEFT, key_y + key_h + 24, note_w, note_h, flow["note"], font_size=13)
    return d


def main() -> int:
    failed = 0
    for key, flow in FLOWS.items():
        d = build(key, flow)
        problems = d.validate()
        if problems:
            failed += 1
            print(f"{d.name}: {len(problems)} problem(s)")
            for p in problems[:8]:
                print("   ", p)
        else:
            d.save_png(OUT_PNG)
            d.save_drawio(OUT_DRAWIO)
            print(f"{d.name:16} clean  {d.width}x{d.height}  "
                  f"{len(flow['screens'])} screens, {len(flow['trans'])} transitions")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
