"""System Architecture — technology/infrastructure view, fixed coordinates.

Genre
-----
This follows the block-and-protocol style the SEP490 template uses: named runtime
pieces, the actual technology inside each one, a nested frame for the host, and a
labelled link for every protocol that crosses a boundary. It is an informal
architecture picture, not a UML diagram type, and it says so on the drawing.

It is deliberately a *different* picture from two neighbours it is easy to confuse:

  architecture-layers   logical — what depends on what (Clean Architecture)
  deployment            UML 2.5.1 — nodes, artifacts, communication paths
  this one              technology — what is built with what, talking over what

Layout (criteria group C in STANDARDS.md)
-----------------------------------------
The App Service is the only thing that talks to everything, so it is one tall frame
in the middle with **one row per partner**: Azure services in a column on its left,
third-party SaaS in a column on its right. Every one of those links is then a single
straight horizontal segment carrying its own protocol label on its own row, so no
label can be read against the wrong line. This skeleton is inherited from
gen_deployment.py, where three other layouts were tried and failed.

Facts
-----
Every version is read from the repository, not remembered: net8.0, MediatR 12.4.1,
EF Core (SQL Server) 8.0.11, Hangfire.AspNetCore 1.8.17, Serilog.AspNetCore 8.0.3,
React 18.3, Vite 5.4, TypeScript 5.5, Tailwind 3.4. Counts: 25 controllers, 68
entities, 4 pipeline behaviours, 30 Hangfire job classes of which 22 are recurring.

    Usage:  python diagrams/gen_architecture_system.py
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsl import Diagram, TEXT_PAD, text_size, wrapped_size  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_PNG = os.path.join(ROOT, "diagrams", "out")
OUT_DRAWIO = os.path.join(ROOT, "diagrams", "drawio")

W = 2100

# A link between two running pieces is bidirectional: the client calls and the
# service answers. A single arrowhead would claim the traffic only ever goes one way.
LINK = dict(start_arrow="open", end_arrow="open")

MARGIN_X = 130                      # left channel, outside the Azure frame
AZ_X, AZ_W = 220, 1160              # Azure frame
COL_X, COL_W = 250, 470             # Azure service column
COL_R = COL_X + COL_W
APP_X, APP_W = 840, 490             # App Service frame
APP_R = APP_X + APP_W
APP_Y, APP_H = 430, 760
EXT_X, EXT_W = 1500, 560            # third-party frame
BOX_X, BOX_W = 1540, 480
ROW_TOP, NODE_H, PITCH = 440, 110, 132


def centred_actor(d: Diagram, key: str, cx: float, y: float, label: str):
    """Place a stick figure centred on cx, so its caption sits over the box below."""
    w = max(46, text_size(label, 13)[0])
    return d.actor(key, cx - w / 2, y, label)


def build() -> Diagram:
    d = Diagram("architecture-system", W, 100)
    d.title("System Architecture — MusicLounge runtime pieces and the protocols "
            "between them")

    # ── people and the devices they hold ────────────────────────────────────
    br_x, br_w = 280, 400
    mo_x, mo_w = 760, 400
    br_cx, mo_cx = br_x + br_w / 2, mo_x + mo_w / 2

    centred_actor(d, "a_web", br_cx, 76, "Audience · Owner · Admin")
    centred_actor(d, "a_mob", mo_cx, 76, "Staff · F&B customer")

    d.box("browser", br_x, 210, br_w, 104,
          "Web browser\nAudience Website · Owner Dashboard · Admin Console",
          font_size=12)
    d.box("mobile", mo_x, 210, mo_w, 104,
          "Android / iOS handset\nStaff Mobile app · Audience F&B app", font_size=12)

    d.edge([(br_cx, 172), (br_cx, 210)], attached=("a_web", "browser"), **LINK)
    d.edge([(mo_cx, 172), (mo_cx, 210)], attached=("a_mob", "mobile"), **LINK)

    # ── Microsoft Azure ─────────────────────────────────────────────────────
    d.package("p_azure", AZ_X, 380, AZ_W, 870, "Microsoft Azure")

    col = [
        ("swa", "Azure Static Web Apps\nReact 18.3 · Vite 5.4 · TypeScript 5.5 · "
                "Tailwind 3.4"),
        ("aca", "Azure Container Apps\npanorama-stitcher — Python · OpenCV · Hugin"),
        ("blob", "Azure Blob Storage + CDN\nposters · panoramas · QR images"),
        ("kv", "Azure Key Vault\nconnection strings · gateway keys"),
        ("ai", "Azure Application Insights\ntraces · metrics · Serilog 8.0 sink"),
        ("sql", "Azure SQL Database (S0)\n68-table schema · Hangfire job store"),
    ]
    cy: dict[str, float] = {}
    for i, (key, lbl) in enumerate(col):
        y = ROW_TOP + i * PITCH
        d.box(key, COL_X, y, COL_W, NODE_H, lbl, font_size=12)
        cy[key] = y + NODE_H / 2

    # The host frame, with its runtime stack nested inside it — the one structural
    # marker that makes this a technology picture rather than a box-and-line sketch.
    d.package("p_app", APP_X, APP_Y, APP_W, APP_H,
              "Azure App Service (Linux)")
    stack = [
        # The runtime version lives here, not in the frame title: the title tab is
        # nearly as wide as the frame, so a longer one left no top entry point that
        # an incoming connector could use without striking the text out.
        (490, 100, "MusicLounge.Api — ASP.NET Core 8 (net8.0)\n"
                   "25 controllers · JWT bearer auth"),
        (610, 100, "MediatR 12.4.1\n4 pipeline behaviours · FluentValidation"),
        (730, 100, "EF Core 8.0.11 (SQL Server)\n68 entities · migrations"),
        (850, 100, "SignalR hubs\nlivestream · donations · notifications"),
        (970, 100, "Hangfire 1.8.17 worker\n30 job classes, 22 recurring"),
        (1090, 80, "Serilog 8.0.3 structured logging"),
    ]
    for i, (y, h, lbl) in enumerate(stack):
        d.box(f"st{i}", APP_X + 30, y, 430, h, lbl, font_size=12)

    # ── third-party SaaS ────────────────────────────────────────────────────
    d.package("p_ext", EXT_X, 380, EXT_W, 710, "Third-party services")
    ext = [
        ("vnpay", 440, "VNPay\ncard and wallet payment · IPN callback"),
        ("twilio", 566, "Twilio\nProgrammable Messaging — OTP and alerts"),
        ("mux", 692, "Mux / Cloudflare Stream\nlive ingest · HLS playback"),
        ("gemini", 818, "Gemini / OpenAI\ncontent moderation · poster generation"),
        ("fcm", 944, "Firebase Cloud Messaging\npush to the mobile apps"),
    ]
    ey: dict[str, float] = {}
    for key, y, lbl in ext:
        d.box(key, BOX_X, y, BOX_W, 104, lbl, font_size=12)
        ey[key] = y + 52

    # ── devices into Azure ──────────────────────────────────────────────────
    d.edge([(br_x + 100, 314), (br_x + 100, ROW_TOP)], label="«HTTPS»",
           attached=("browser", "swa"), label_side="left", label_t=0.2, **LINK)
    d.edge([(br_x + 320, 314), (br_x + 320, 368), (1150, 368), (1150, APP_Y)],
           label="«HTTPS» REST + WebSocket", attached=("browser", "p_app"),
           label_pos=1, **LINK)
    d.edge([(mo_x + 360, 314), (mo_x + 360, 344), (1270, 344), (1270, APP_Y)],
           label="«HTTPS» REST + WebSocket", attached=("mobile", "p_app"),
           # Above, not below: the two devices carry the same protocol, so the two
           # labels read identically. Placed below, this one dropped into the band
           # between the two connectors and either could claim it.
           label_pos=1, label_side="above", **LINK)
    # Media is served straight from the CDN, not proxied through the API — routed
    # down the left margin so it never shares a lane with an API call.
    d.edge([(br_x, 262), (MARGIN_X, 262), (MARGIN_X, cy["blob"]), (COL_X, cy["blob"])],
           label="«HTTPS» media", attached=("browser", "blob"),
           label_pos=1, label_side="left", **LINK)

    # ── App Service across to every Azure partner, one straight row each ────
    for key, proto in (("aca", "«HTTP»"), ("blob", "«HTTPS»"), ("kv", "«HTTPS»"),
                       ("ai", "«HTTPS»"), ("sql", "«TDS» 1433")):
        d.edge([(APP_X, cy[key]), (COL_R, cy[key])], label=proto,
               attached=("p_app", key), **LINK)

    # ── App Service out to every third-party service, likewise ──────────────
    for key, _y, _lbl in ext:
        d.edge([(APP_R, ey[key]), (BOX_X, ey[key])], label="«HTTPS»",
               attached=("p_app", key), **LINK)

    # ── note, and a canvas sized to it rather than guessed ──────────────────
    note_text = (
        "This is a technology architecture view, not a UML diagram type. It shows "
        "what each running piece is built with and which protocol carries the traffic "
        "between them; the logical dependency order is a separate picture "
        "(architecture-layers), and the UML node-and-artifact view is a third "
        "(deployment).\n"
        "Every link is drawn with an arrowhead at both ends because each one is a "
        "request and a response, and each is a single straight segment carrying its "
        "own label, so no label can be read against the wrong line. App Service "
        "authenticates to Key Vault with a managed identity, so no secret ships with "
        "the application. Static Web Apps serves the compiled React bundle and the "
        "CDN serves media directly to the browser; neither is proxied through the API."
    )
    ny = 1290
    nw = W - 2 * MARGIN_X
    _, nh = wrapped_size(note_text, nw - 2 * TEXT_PAD - 6, 13)
    note_h = nh + 2 * TEXT_PAD + 12
    d.note("n1", MARGIN_X, ny, nw, note_h, note_text, font_size=13)
    d.height = int(ny + note_h + 40)
    return d


def main() -> int:
    d = build()
    problems = d.validate()
    if problems:
        print(f"architecture-system: {len(problems)} geometry problem(s)")
        for p in problems:
            print("   ", p)
        return 1
    print(f"architecture-system: geometry clean — no overlaps, no collinear runs, "
          f"no clipped text  ({d.width}x{d.height})")
    print(" ", d.save_png(OUT_PNG))
    print(" ", d.save_drawio(OUT_DRAWIO))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
