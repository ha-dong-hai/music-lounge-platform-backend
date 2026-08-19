"""Core Entity Relationship Diagram — Crow's Foot notation, fixed coordinates.

Notation (criteria group A in STANDARDS.md)
-------------------------------------------
Crow's Foot / Information Engineering. Each line end carries two marks:

    inner mark = minimum (optionality)      outer mark = maximum (cardinality)
    ||  exactly one        |o  zero or one
    }|  one or many        }o  zero or many

A nullable foreign key in the C# entity means the minimum is **zero**, so that end
takes a circle, never a bar. Every cardinality below was read off the entity class in
src/MusicLounge.Domain/Entities rather than inferred from the business intent — two
of them (Ticket.PaymentId, LedgerEntry.PaymentId) were wrong in the first version for
exactly that reason.

Layout (criteria group C): entities are placed in four bands so that most
relationships are a single horizontal segment between neighbours, and the few that
cross bands each get their own routing channel.

    Usage:  python diagrams/gen_erd_core.py
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsl import Diagram  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_PNG = os.path.join(ROOT, "diagrams", "out")
OUT_DRAWIO = os.path.join(ROOT, "diagrams", "drawio")

W, H = 1980, 1280
BW, BH = 230, 64
COLS = (60, 380, 700, 1020, 1340, 1660)
BANDS = (140, 400, 660, 920)

ONE = "one_one"          # ||  exactly one
ZERO_ONE = "zero_one"    # |o  zero or one
MANY = "zero_many"       # }o  zero or many


def build() -> Diagram:
    d = Diagram("erd-core", W, H)
    d.title("Core Entity Relationship Diagram (Crow's Foot notation)")

    place = {
        # band 0 — venue
        "user": (0, 0, "User"), "lounge": (1, 0, "MusicLounge"),
        "staff": (2, 0, "LoungeStaff"), "zone": (3, 0, "SeatingZone"),
        "penalty": (4, 0, "VenuePenalty"),
        # band 1 — show
        "cmpl": (0, 1, "Complaint"), "perf": (1, 1, "Performance"),
        "show": (2, 1, "LoungeShow"), "live": (3, 1, "Livestream"),
        "tier": (4, 1, "TicketTier"),
        # band 2 — ticketing
        "hold": (1, 2, "TicketHold"), "price": (2, 2, "TicketPrice"),
        "ticket": (3, 2, "Ticket"), "pay": (4, 2, "Payment"),
        # band 3 — money
        "artist": (0, 3, "Performer"), "donate": (1, 3, "Donation"),
        "bank": (2, 3, "BankAccount"), "settle": (3, 3, "Settlement"),
        "ledger": (4, 3, "LedgerEntry"), "acct": (5, 3, "Account"),
    }
    box: dict[str, tuple[float, float, float, float]] = {}
    for key, (c, b, label) in place.items():
        x, y = COLS[c], BANDS[b]
        d.box(key, x, y, BW, BH, label, font_size=15, bold=True)
        box[key] = (x, y, x + BW, y + BH)

    def L(k):     # left edge x
        return box[k][0]

    def R(k):     # right edge x
        return box[k][2]

    def T(k):
        return box[k][1]

    def B(k):
        return box[k][3]

    def CY(k):
        return (box[k][1] + box[k][3]) / 2

    def rel(pts, a, b, start=ONE, end=MANY, **kw):
        d.edge(pts, start_arrow=start, end_arrow=end, attached=(a, b), **kw)

    # ── venue band: neighbours join straight across ─────────────────────────
    rel([(R("user"), CY("user")), (L("lounge"), CY("lounge"))], "user", "lounge")
    rel([(R("lounge"), CY("lounge")), (L("staff"), CY("staff"))], "lounge", "staff")

    # Three relationships that skip a column run in their own lane above the band.
    rel([(175, T("user")), (175, 118), (815, 118), (815, T("staff"))],
        "user", "staff")
    rel([(495, T("lounge")), (495, 96), (1135, 96), (1135, T("zone"))],
        "lounge", "zone")
    rel([(535, T("lounge")), (535, 74), (1455, 74), (1455, T("penalty"))],
        "lounge", "penalty")

    # ── venue down into show ────────────────────────────────────────────────
    rel([(495, B("lounge")), (495, 300), (815, 300), (815, T("show"))],
        "lounge", "show")
    rel([(175, B("user")), (175, T("cmpl"))], "user", "cmpl", start=ZERO_ONE)

    # ── show band ───────────────────────────────────────────────────────────
    rel([(L("show"), CY("show")), (R("perf"), CY("perf"))], "show", "perf")
    rel([(R("show"), CY("show")), (L("live"), CY("live"))], "show", "live",
        end=ZERO_ONE)
    rel([(855, T("show")), (855, 360), (1455, 360), (1455, T("tier"))],
        "show", "tier")
    rel([(1135, B("zone")), (1135, 330), (1495, 330), (1495, T("tier"))],
        "zone", "tier", start=ZERO_ONE)
    # Performer sits in the money band; it reaches Performance up its own channel.
    rel([(R("artist"), CY("artist")), (310, CY("artist")), (310, 420), (L("perf"), 420)],
        "artist", "perf")

    # ── ticketing band ──────────────────────────────────────────────────────
    rel([(1455, B("tier")), (1455, 560), (815, 560), (815, T("price"))],
        "tier", "price")
    rel([(R("price"), CY("price")), (L("ticket"), CY("ticket"))], "price", "ticket")
    rel([(L("price"), CY("price")), (R("hold"), CY("hold"))], "price", "hold")
    rel([(R("user"), 190), (335, 190), (335, 600), (1135, 600), (1135, T("ticket"))],
        "user", "ticket", start=ZERO_ONE)
    rel([(L("pay"), CY("pay")), (R("ticket"), CY("ticket"))], "pay", "ticket",
        start=ZERO_ONE)

    # ── money band ──────────────────────────────────────────────────────────
    rel([(1455, B("pay")), (1455, T("ledger"))], "pay", "ledger", start=ZERO_ONE)
    rel([(L("acct"), CY("acct")), (R("ledger"), CY("ledger"))], "acct", "ledger")
    rel([(1385, B("pay")), (1385, 850), (1135, 850), (1135, T("settle"))],
        "pay", "settle")
    rel([(R("bank"), CY("bank")), (L("settle"), CY("settle"))], "bank", "settle",
        start=ZERO_ONE)
    rel([(L("bank"), CY("bank")), (R("donate"), CY("donate"))], "bank", "donate",
        start=ZERO_ONE)
    rel([(L("perf"), 445), (360, 445), (360, 880), (495, 880), (495, T("donate"))],
        "perf", "donate")

    d.note("n1", 60, 1030, 1180, 180,
           "Reading the marks: the inner mark is the minimum, the outer the maximum. "
           "Ticket.PaymentId and LedgerEntry.PaymentId are nullable, so both take "
           "\"zero or one\" on the Payment side — a walk-in ticket paid in cash and a "
           "platform-internal journal line each exist without a gateway payment. "
           "TicketTier.ZoneId is nullable too: an online-only tier has no seating zone.\n"
           "BankAccount.OwnerId is polymorphic and carries no foreign key. It holds "
           "either a MusicLounge id or a Performer id, chosen by OwnerType, so no "
           "relationship line is drawn to those two tables: the database does not "
           "enforce one.", font_size=13)
    d.note("n2", 1290, 1030, 630, 180,
           "Only core cross-domain entities appear here; the complete set of 68 is in "
           "erd-full.\n"
           "Every cardinality was read off the entity class, not inferred from intent. "
           "That is how two of them were found to be wrong in the first version: both "
           "claimed \"exactly one\" against a nullable foreign key.", font_size=13)
    return d


def main() -> int:
    d = build()
    problems = d.validate()
    if problems:
        print(f"erd-core: {len(problems)} geometry problem(s)")
        for p in problems:
            print("   ", p)
        return 1
    print("erd-core: geometry clean — no overlaps, no collinear runs, no clipped text")
    print(" ", d.save_png(OUT_PNG))
    print(" ", d.save_drawio(OUT_DRAWIO))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
