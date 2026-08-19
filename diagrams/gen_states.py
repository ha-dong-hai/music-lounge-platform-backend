"""State Machine Diagrams — UML 2.5.1, fixed coordinates.

Notation (criteria group A in STANDARDS.md)
-------------------------------------------
An initial pseudostate is a filled disc, a final state a ring around one, a state a
rounded box, a transition a solid arrow labelled with its trigger.

Why these are not PlantUML
--------------------------
They were. On the LoungeShow machine it routed a connector straight through the words
"submit for review (only from Draft)" and struck two "cancel" captions out the same
way — the caption was still there, just unreadable, which is worse than a missing one.

Content
-------
Every state is a real member of the corresponding enum, and every transition is taken
from the handler that performs it. Drawing from the enum alone is what would have
hidden the finding recorded at the foot of the Ticket machine: TicketStatus.Refunded
is declared but assigned nowhere in src or tests.

Layout (criteria group C): states run down one column, transitions are routed in the
field beside them with a channel each, and captions are placed by searching for the
first spot that touches nothing.

    Usage:  python diagrams/gen_states.py
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

BW = 260
SLOT = 17
ROW_GAP = 54
CHANNEL = 46
# The left margin holds the pseudostates AND the caption of each initial
# transition; at 120 the caption had nowhere to sit but on top of the state.
LEFT = 300
PSEUDO_X = 40                 # pseudostate column, well left of the states
TOP = 130

MACHINES = {
    "show": {
        "title": "State Machine — LoungeShow (LoungeShowStatus)",
        "enum": "LoungeShowStatus",
        "states": ["Draft", "Pending", "Published", "Ongoing", "Ended", "Cancelled"],
        "initial": [("Draft", "Owner creates a show")],
        "final": ["Ended", "Cancelled"],
        "trans": [
            ("Draft", "Pending", "submit for review (only from Draft)"),
            ("Pending", "Published", "Admin approves"),
            ("Pending", "Draft", "Admin rejects, Owner fixes and resubmits"),
            ("Published", "Ongoing", "start show or start broadcast"),
            ("Ongoing", "Ended", "end show, end broadcast, or livestream terminated"),
            ("Draft", "Cancelled", "cancel"),
            ("Pending", "Cancelled", "cancel"),
            ("Published", "Cancelled", "cancel, refunding confirmed tickets"),
            ("Ongoing", "Cancelled", "cancel"),
        ],
        "note": ("Published is reachable only through the Admin review path. Rejection "
                 "returns the show to Draft rather than to a distinct rejected state, "
                 "so the Owner corrects and resubmits the same show. Cancellation is "
                 "refused once the show is already Ended or Cancelled; every other "
                 "state may still be cancelled."),
    },
    "ticket": {
        "title": "State Machine — Ticket (TicketStatus)",
        "enum": "TicketStatus",
        "states": ["Pending", "Confirmed", "Used", "Cancelled", "Refunded"],
        "initial": [("Pending", "online purchase created"),
                    ("Confirmed", "walk-in sale at the counter,\npaid in cash")],
        "final": ["Used", "Cancelled"],
        "trans": [
            ("Pending", "Confirmed", "gateway callback confirms payment"),
            ("Pending", "Cancelled",
             "gateway reports failure, or CancelAbandonedPaymentsJob "
             "finds it unpaid after 30 minutes"),
            ("Confirmed", "Used", "checked in at the door"),
            ("Confirmed", "Cancelled",
             "cancelled by holder, show cancelled, format changed, "
             "or complaint resolved"),
        ],
        "note": ("Refunded is UNREACHABLE: the enum declares it, but nothing in src or "
                 "tests ever assigns it — an approved refund leaves the ticket "
                 "Cancelled and posts a reversing ledger entry. It is drawn because "
                 "the state exists in the schema, not because a transition reaches "
                 "it. Check-in additionally requires the show to be Ongoing, the "
                 "ticket to be a physical one, and no transfer to be in progress."),
    },
}


def build(key: str, m: dict) -> Diagram:
    states: list[str] = m["states"]
    trans: list[tuple[str, str, str]] = m["trans"]
    initial: list[tuple[str, str]] = m["initial"]
    final: list[str] = m["final"]

    degree: dict[str, int] = defaultdict(int)
    for a, b, _ in trans:
        degree[a] += 1
        degree[b] += 1
    for s, _ in initial:
        degree[s] += 1
    for s in final:
        degree[s] += 1

    height_of = {s: max(56, 24 + degree[s] * SLOT) for s in states}
    pos: dict[str, tuple[float, float]] = {}
    y = TOP
    for s in states:
        pos[s] = (y, height_of[s])
        y += height_of[s] + ROW_GAP
    grid_bottom = y - ROW_GAP

    n_ch = max(1, len(trans))
    field_right = LEFT + BW + 60 + n_ch * CHANNEL
    width = max(field_right + 620, 1400)
    note_w = width - PSEUDO_X - 60
    _, nh = wrapped_size(m["note"], note_w - 2 * TEXT_PAD - 6, 13)
    note_h = nh + 2 * TEXT_PAD + 20
    height = grid_bottom + 70 + note_h + 40

    d = Diagram(f"state-{key}", int(width), int(height))
    d.title(m["title"])

    for s in states:
        ny, h = pos[s]
        d.box(s, LEFT, ny, BW, h, s, font_size=15, bold=True)

    used: dict[str, int] = defaultdict(int)

    def anchor(name: str) -> float:
        ny, _h = pos[name]
        k = used[name]
        used[name] += 1
        return ny + 18 + k * SLOT

    # Initial pseudostates enter from the left margin.
    for n, (target, caption) in enumerate(initial):
        ay = anchor(target)
        d.pseudostate(f"__init{n}__", PSEUDO_X, ay - 11)
        d.edge([(PSEUDO_X + 22, ay), (LEFT, ay)], end_arrow="open",
               attached=(f"__init{n}__", target), label=caption,
               label_side="above")

    # Final states leave to the left margin as well, below the initial ones.
    for n, source in enumerate(final):
        ay = anchor(source)
        d.pseudostate(f"__final{n}__", PSEUDO_X, ay - 11, kind="final")
        d.edge([(LEFT, ay), (PSEUDO_X + 22, ay)], end_arrow="open",
               attached=(f"__final{n}__", source))

    right = LEFT + BW
    spans = []
    for i, (src, dst, caption) in enumerate(trans):
        gx = right + 60 + i * CHANNEL
        y1, y2 = anchor(src), anchor(dst)
        d.edge([(right, y1), (gx, y1), (gx, y2), (right, y2)],
               end_arrow="open", attached=(src, dst))
        spans.append((gx, min(y1, y2), max(y1, y2), caption))

    # Captions only once every connector exists, so none can settle on a channel that
    # had not been drawn yet.
    for e, (gx, lo, hi, caption) in zip(d.edges[len(initial) + len(final):], spans):
        placed = None
        for dx in (16, 70, 130, 200, 280, 370, 470):
            for t in (0.5, 0.34, 0.66, 0.2, 0.8, 0.08, 0.92):
                probe = d.measure_label(caption, 0, lo + (hi - lo) * t)
                cand = Rect(gx + dx, probe.y, probe.w, probe.h)
                if cand.x2 < width - 16 and not d.label_collides(cand):
                    placed = cand
                    break
            if placed:
                break
        if placed is None:
            raise RuntimeError(f"state-{key}: nowhere to put {caption!r}")
        e.label, e.label_rect = caption, placed

    d.note("note", PSEUDO_X, grid_bottom + 60, note_w, note_h, m["note"], font_size=13)
    return d


def main() -> int:
    failed = 0
    for key, m in MACHINES.items():
        d = build(key, m)
        problems = d.validate()
        if problems:
            failed += 1
            print(f"{d.name}: {len(problems)} problem(s)")
            for p in problems[:8]:
                print("   ", p)
        else:
            d.save_png(OUT_PNG)
            d.save_drawio(OUT_DRAWIO)
            print(f"{d.name:14} clean  {d.width}x{d.height}  "
                  f"{len(m['states'])} states, {len(m['trans'])} transitions")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
