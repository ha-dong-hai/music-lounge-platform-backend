"""System Architecture — layered view, fixed coordinates.

This is deliberately NOT the deployment diagram. Deployment answers "what runs where,
over which protocol"; this answers "what depends on what". They were conflated in the
first pass of the slide deck, where the System Architecture slide pointed at the
deployment picture.

Notation
--------
Not a formal UML diagram type — it is a layered architecture view, which the UML
specification does not define. It borrows UML's package frame and «use» dependency
so the reading rules stay familiar, and says so on the diagram rather than implying a
standard it does not follow.

The one rule the picture has to make obvious is Clean Architecture's dependency rule:
every arrow points inward, towards Domain. Infrastructure sits outside and still
points inward, because Application declares the interfaces and Infrastructure
implements them — data flows out to the database while the dependency points in.

Every figure is measured: 25 controllers, 68 entities, 4 pipeline behaviours, 30
Hangfire job classes of which 22 are recurring.

    Usage:  python diagrams/gen_architecture.py
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsl import Diagram  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_PNG = os.path.join(ROOT, "diagrams", "out")
OUT_DRAWIO = os.path.join(ROOT, "diagrams", "drawio")

W = 1720
H = 1780                         # sized to the content below, not guessed
LX, LW = 60, 760                 # left stack
# The corridor between the two columns has to hold a dependency label; at 80px
# wide there was nowhere to put one that was not on a component.
RX, RW = 1000, 580               # infrastructure column
PAD, COMP_H, TAB = 18, 96, 30

DEP = dict(style="dashed", end_arrow="open")


def band(d: Diagram, key: str, x: float, y: float, w: float, title: str,
         comps: list[str], rows: int = 1) -> float:
    """Draw a package frame with its components laid out in `rows` rows."""
    per = (len(comps) + rows - 1) // rows
    cw = (w - PAD * (per + 1)) / per
    h = TAB + PAD + rows * COMP_H + (rows - 1) * PAD + PAD
    d.package(f"p_{key}", x, y, w, h, title)
    for i, c in enumerate(comps):
        r, col = divmod(i, per)
        d.box(f"{key}{i}", x + PAD + col * (cw + PAD),
              y + TAB + PAD + r * (COMP_H + PAD), cw, COMP_H, c, font_size=12)
    return h


def build() -> Diagram:
    d = Diagram("architecture-layers", W, H)
    d.title("System Architecture — layered view of the MusicLounge platform")

    y = 104
    h1 = band(d, "client", LX, y, LW, "Client surfaces",
              ["Audience Website", "Owner Dashboard", "Admin Console",
               "Staff Mobile app", "Audience F&B app"], rows=2)
    y_api = y + h1 + 74
    h2 = band(d, "api", LX, y_api, LW, "MusicLounge.Api",
              ["Controllers (25)", "SignalR hubs", "Authorization policies",
               "Middleware"])
    y_app = y_api + h2 + 74
    h3 = band(d, "app", LX, y_app, LW, "MusicLounge.Application",
              ["Commands and Queries with their handlers",
               "Pipeline behaviours (4)", "Validators", "Abstractions (interfaces)"])
    y_dom = y_app + h3 + 74
    h4 = band(d, "dom", LX, y_dom, LW, "MusicLounge.Domain",
              ["Entities (68)", "Enums and value objects", "Domain events"])

    # Infrastructure sits outside the stack, opposite Application and Domain.
    h5 = band(d, "infra", RX, y_api, RW, "MusicLounge.Infrastructure",
              ["EF Core DbContext and repositories", "External service adapters",
               "Hangfire jobs (30 classes, 22 recurring)"], rows=3)

    y_ext = max(y_dom + h4, y_api + h5) + 74
    band(d, "ext", LX, y_ext, RX + RW - LX, "Data stores and external services",
         ["Azure SQL Database", "Blob Storage and CDN", "VNPay", "Twilio",
          "Mux / Cloudflare Stream", "Gemini / OpenAI", "Firebase Cloud Messaging",
          "panorama-stitcher (Python)"], rows=2)

    cx = LX + LW / 2
    d.edge([(cx, y + h1), (cx, y_api)], label="«HTTPS» REST and WebSocket",
           attached=("p_client", "p_api"), **DEP)
    d.edge([(cx, y_api + h2), (cx, y_app)], label="«use» sends commands and queries",
           attached=("p_api", "p_app"), **DEP)
    d.edge([(cx, y_app + h3), (cx, y_dom)], label="«use»",
           attached=("p_app", "p_dom"), **DEP)

    # Infrastructure points inward: Application declares the interfaces, Infrastructure
    # implements them.
    d.edge([(RX, y_app + 44), (LX + LW, y_app + 44)],
           label="«use» implements\nthe abstractions",
           attached=("p_infra", "p_app"), **DEP)
    # Infrastructure also references Domain entities directly through EF Core.
    # Routed round the outside because the frame ends above the Domain row.
    d.edge([(RX + 120, y_api + h5), (RX + 120, y_dom + h4 - 20),
            (LX + LW, y_dom + h4 - 20)], label="«use»",
           attached=("p_infra", "p_dom"), label_pos=1, **DEP)
    d.edge([(LX + LW, y_api + 48), (RX, y_api + 48)], label="«use» composition root",
           attached=("p_api", "p_infra"), **DEP)

    d.edge([(RX + RW / 2, y_api + h5), (RX + RW / 2, y_ext)], label="integrates with",
           attached=("p_infra", "p_ext"), **DEP)

    d.note("n1", LX, y_ext + 300, RX + RW - LX, 126,
           "This is a layered architecture view, not a UML diagram type — UML does not "
           "define one. It borrows the package frame and the «use» dependency so the "
           "reading rules stay familiar.\n"
           "Every dependency points inward, towards Domain, which references nothing at "
           "all. Infrastructure is the exception that proves the rule: it sits outside "
           "and still points in, because Application declares the interfaces and "
           "Infrastructure implements them. Data flows outward to the database while "
           "the dependency points inward, which is what keeps the business rules "
           "testable without a database or a host.", font_size=13)
    return d


def main() -> int:
    d = build()
    problems = d.validate()
    if problems:
        print(f"architecture-layers: {len(problems)} geometry problem(s)")
        for p in problems:
            print("   ", p)
        return 1
    print("architecture-layers: geometry clean — no overlaps, no collinear runs, "
          "no clipped text")
    print(" ", d.save_png(OUT_PNG))
    print(" ", d.save_drawio(OUT_DRAWIO))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
