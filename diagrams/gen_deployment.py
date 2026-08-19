"""Deployment Diagram — UML 2.5.1, fixed coordinates.

Notation (criteria group A in STANDARDS.md)
-------------------------------------------
* A **Node** is a perspective three-dimensional cube, not a flat rectangle.
* A **communication path** is notated as an *association*: a plain solid line with
  **no arrowhead**. A directed arrow would mean a dependency.
* «device» is hardware; «executionEnvironment» is a software runtime hosting
  artifacts. A hosted third-party service is the latter, never the former.
* «cloud» is not standard UML, so the Azure boundary is a plain package frame.
* Artifacts are listed textually inside their deployment target, which the standard
  permits and which keeps the node count readable.

Layout (criteria group C)
-------------------------
App Service talks to more things than anything else, so it is drawn as one tall node
in the middle, with **every partner on its own row**: the six Azure services in a
column to its left, the five third-party services in a column to its right. Every
communication path is then a **single straight horizontal segment with zero bends**,
and its protocol label sits in the gap directly on that row, so no label can be read
against the wrong line.

Three earlier attempts failed here and are worth recording:
  * bundling all five third-party paths through one shared trunk drew them as a single
    stroke — caught by criterion B4;
  * fanning them out of a mid-height App Service made them cross each other repeatedly
    — legal geometry, unreadable picture;
  * a two-column Azure grid left two partners off-row, and their labels floated
    between two rows where either row could claim them.

    Usage:  python diagrams/gen_deployment.py
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsl import Diagram  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_PNG = os.path.join(ROOT, "diagrams", "out")
OUT_DRAWIO = os.path.join(ROOT, "diagrams", "drawio")

W, H = 1880, 1420
PATH = dict(end_arrow="none", start_arrow="none")   # communication path: no arrowhead

# One column of Azure services facing one tall App Service. With two columns, the
# partners that were not directly opposite needed dog-leg routes, and their protocol
# labels ended up floating between two rows where they could be read as belonging to
# either. One row per partner makes every path a single straight segment.
# Everything is shifted right of x=170 to leave a real left margin: the browser-to-CDN
# path runs down that margin, and its label needs somewhere to sit that is not on top
# of the service column.
COL_X, COL_W, COL_R = 200, 520, 720          # service column, right edge
NODE_H, PITCH = 110, 130
ROW_TOP = 420
APP_X, APP_W, APP_R = 830, 340, 1170
APP_Y, APP_H = 420, 760
MARGIN_X = 140                                # left-margin channel


def build() -> Diagram:
    d = Diagram("deployment", W, H)
    d.title("Deployment Diagram — MusicLounge on Microsoft Azure")

    # ── client tier ─────────────────────────────────────────────────────────
    d.package("p_client", 170, 76, 940, 224, "Client tier")
    d.node3d("browser", 200, 108, 400, 168,
             "«executionEnvironment»\nWeb browser\n\nAudience Website · Owner Dashboard\n"
             "Admin Console", font_size=12)
    d.node3d("mobile", 670, 108, 400, 168,
             "«device»\nAndroid / iOS handset\n\nStaff Mobile app\nAudience F&B app",
             font_size=12)

    # ── Azure: one service column, one tall App Service ─────────────────────
    d.package("p_azure", 170, 384, 1060, 830, "Microsoft Azure")
    col = [("swa", "«executionEnvironment»\nStatic Web Apps\n\nWeb client bundles"),
           ("aca", "«executionEnvironment»\nContainer Apps\n\npanorama-stitcher"),
           ("blob", "«device»\nBlob Storage and CDN"),
           ("kv", "«device»\nKey Vault"),
           ("appins", "«device»\nApplication Insights"),
           ("sql", "«executionEnvironment»\nSQL Database\n\nschema · Hangfire job store")]
    cy: dict[str, float] = {}
    for i, (key, lbl) in enumerate(col):
        y = ROW_TOP + i * PITCH
        d.node3d(key, COL_X, y, COL_W, NODE_H, lbl, font_size=12)
        cy[key] = y + NODE_H / 2
    d.node3d("appsvc", APP_X, APP_Y, APP_W, APP_H,
             "«executionEnvironment»\nApp Service (Linux)\n\nMusicLounge.Api\n"
             "SignalR hubs\nHangfire worker", font_size=12)

    # ── third-party services, each on the row of its own exit ───────────────
    d.package("p_ext", 1290, 384, 560, 800, "Third-party services")
    ext = [("vnpay", "«executionEnvironment»\nVNPay", 428),
           ("twilio", "«executionEnvironment»\nTwilio", 568),
           ("mux", "«executionEnvironment»\nMux / Cloudflare Stream", 708),
           ("gem", "«executionEnvironment»\nGemini / OpenAI", 848),
           ("fcm", "«executionEnvironment»\nFirebase Cloud Messaging", 988)]
    for key, lbl, ty in ext:
        d.node3d(key, 1330, ty, 480, 104, lbl, font_size=12)

    # ── client tier into Azure ──────────────────────────────────────────────
    d.edge([(330, 276), (330, ROW_TOP)], label="«HTTPS»",
           attached=("browser", "swa"), **PATH)
    d.edge([(550, 276), (550, 340), (950, 340), (950, APP_Y)],
           label="«HTTPS» REST + WebSocket", attached=("browser", "appsvc"),
           label_pos=1, **PATH)
    d.edge([(1050, 276), (1050, APP_Y)], label="«HTTPS»\nREST + WebSocket",
           attached=("mobile", "appsvc"), label_side="right", **PATH)
    d.edge([(COL_X, 200), (MARGIN_X, 200), (MARGIN_X, cy["blob"]), (COL_X, cy["blob"])],
           label="«HTTPS» media", attached=("browser", "blob"),
           label_pos=1, label_side="left", label_t=0.5, **PATH)

    # ── App Service straight across to every partner, one row each ──────────
    for key, proto in (("aca", "«HTTP»"), ("blob", "«HTTPS»"), ("kv", "«HTTPS»"),
                       ("appins", "«HTTPS»"), ("sql", "«TDS»")):
        d.edge([(APP_X, cy[key]), (COL_R, cy[key])], label=proto,
               attached=("appsvc", key), **PATH)

    # ── App Service out to each third-party service: one straight segment ───
    for key, _, ty in ext:
        d.edge([(APP_R, ty + 52), (1330, ty + 52)], attached=("appsvc", key), **PATH)

    d.note("n1", 170, 1250, 700, 128,
           "A communication path is notated as an association — a plain solid line. It "
           "carries no arrowhead, because a directed arrow would mean a dependency.\n"
           "App Service reaches Key Vault with a managed identity, so no secret is "
           "deployed with the application.", font_size=13)
    d.note("n2", 930, 1250, 880, 128,
           "SMS is delivered through Twilio. Every path from App Service to a "
           "third-party service is «HTTPS» and is drawn as one straight segment, so "
           "none needs a label to be followed. Third-party services are an external "
           "«executionEnvironment», not a «device»: they are software endpoints, not "
           "hardware this system is deployed onto.", font_size=13)
    return d


def main() -> int:
    d = build()
    problems = d.validate()
    if problems:
        print(f"deployment: {len(problems)} geometry problem(s)")
        for p in problems:
            print("   ", p)
        return 1
    print("deployment: geometry clean — no overlaps, no collinear runs, no clipped text")
    print(" ", d.save_png(OUT_PNG))
    print(" ", d.save_drawio(OUT_DRAWIO))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
