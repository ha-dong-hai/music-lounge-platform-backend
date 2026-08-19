"""Context Diagram (DFD level 0), Yourdon / DeMarco notation, fixed coordinates.

Notation
--------
Yourdon / DeMarco draws a process as a **circle**, not an ellipse. The first attempt
used a 360x760 ellipse, which is a notation error rather than a matter of taste, so the
process here is a true circle: equal width and height.

Layout contract
---------------
External entities sit in two columns and every data flow is a **single straight
horizontal segment** at its own reserved y — no bends, no shared channels. A flow can
therefore only ever touch its own entity and the process, and each label sits in its own
white rectangle directly above its own line, so a label can never be read against the
wrong flow.

Entity boxes are deliberately taller than the span of the flows attached to them; when
they are not, the connectors meet the box exactly at its corners and read as passing
through it.

Attachment points are computed on the true circle boundary, so connectors meet the
bubble rather than stopping short of it or overrunning into it.

    Usage:  python diagrams/gen_context.py
"""

from __future__ import annotations

import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsl import Diagram  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_PNG = os.path.join(ROOT, "diagrams", "out")
OUT_DRAWIO = os.path.join(ROOT, "diagrams", "drawio")

W, H = 1900, 1046
CX, CY = 950, 500
R = 340                       # process radius — a circle, per Yourdon / DeMarco
LEFT_X, RIGHT_X = 60, 1640
BOX_W = 200
SLOT_L, SLOT_R = 58, 62       # flow pitch, left and right columns
BOX_H_2, BOX_H_1 = 84, 58     # box height for a two-flow and a one-flow entity


def edge_x(dy: float, side: str) -> float:
    """X of the circle boundary at vertical offset dy from the centre."""
    k = math.sqrt(max(0.0, R * R - dy * dy))
    return CX - k if side == "left" else CX + k


def build() -> Diagram:
    d = Diagram("context", W, H)
    d.title("Context Diagram (DFD Level 0) — MusicLounge")

    d.ellipse("proc", CX - R, CY - R, 2 * R, 2 * R,
              "0\n\nMusicLounge\nPlatform", font_size=18, bold=True)

    # ── left column: human external entities ────────────────────────────────
    left = [
        ("guest", "Guest", "search criteria", "show listings"),
        ("aud", "Audience", "ticket order, donation", "e-ticket and QR code"),
        ("own", "Owner", "venue and show details", "earnings report"),
        ("stf", "Staff", "walk-in sale, scanned QR", "ticket validity"),
        ("adm", "Admin", "moderation decision", "review queue"),
    ]
    first = CY - 4.5 * SLOT_L
    for i, (key, label, into, outof) in enumerate(left):
        y_in = first + (2 * i) * SLOT_L
        y_out = y_in + SLOT_L
        cy = (y_in + y_out) / 2
        d.box(key, LEFT_X, cy - BOX_H_2 / 2, BOX_W, BOX_H_2, label,
              font_size=15, bold=True)
        d.edge([(LEFT_X + BOX_W, y_in), (edge_x(y_in - CY, "left"), y_in)],
               label=into, attached=(key, "proc"))
        d.edge([(edge_x(y_out - CY, "left"), y_out), (LEFT_X + BOX_W, y_out)],
               label=outof, attached=(key, "proc"))

    # ── right column: machine external entities ─────────────────────────────
    right = [
        ("vnpay", "VNPay", ["payment request"], ["payment confirmation"]),
        ("twilio", "Twilio", ["SMS message"], []),
        ("mux", "Mux /\nCloudflare Stream", ["broadcast stream"], ["playback URL"]),
        ("ai", "Gemini / OpenAI", ["content for scoring"], ["risk score"]),
        ("fcm", "Firebase\nCloud Messaging", ["push message"], []),
    ]
    n = sum(len(a) + len(b) for _, _, a, b in right)
    slot = CY - (n - 1) / 2 * SLOT_R
    for key, label, out_labels, in_labels in right:
        ys = [slot + k * SLOT_R for k in range(len(out_labels) + len(in_labels))]
        slot = ys[-1] + SLOT_R
        cy = sum(ys) / len(ys)
        bh = BOX_H_2 if len(ys) > 1 else BOX_H_1
        d.box(key, RIGHT_X, cy - bh / 2, BOX_W, bh, label, font_size=15, bold=True)
        idx = 0
        for lbl in out_labels:          # platform -> external service
            y = ys[idx]; idx += 1
            d.edge([(edge_x(y - CY, "right"), y), (RIGHT_X, y)],
                   label=lbl, attached=(key, "proc"))
        for lbl in in_labels:           # external service -> platform
            y = ys[idx]; idx += 1
            d.edge([(RIGHT_X, y), (edge_x(y - CY, "right"), y)],
                   label=lbl, attached=(key, "proc"))

    # Written as sentences, not an indented key list: word wrap collapses leading
    # spaces, so an indented legend loses its alignment once rendered.
    d.note("legend", LEFT_X, H - 150, 720, 116,
           "Yourdon / DeMarco notation.\n"
           "The circle is the single process, numbered 0.\n"
           "Each rectangle is an external entity outside the system boundary.\n"
           "Each arrow is a data flow, labelled with the data that moves.\n"
           "No data stores appear at context level.", font_size=13)
    return d


def main() -> int:
    d = build()
    problems = d.validate()
    if problems:
        print(f"context: {len(problems)} geometry problem(s)")
        for p in problems:
            print("   ", p)
        return 1
    png = d.save_png(OUT_PNG)
    dio = d.save_drawio(OUT_DRAWIO)
    print(f"context: geometry clean — no overlaps, no clipped text\n  {png}\n  {dio}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
