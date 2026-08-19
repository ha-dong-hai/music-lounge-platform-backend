"""Package Diagram — UML 2.5.1, fixed coordinates.

Notation
--------
* This diagram shows **packages only**. Component boxes belong in a component
  diagram; mixing the two notations, as the auto-laid-out version did, is a
  notation error.
* A dependency is a dashed line with an **open** arrowhead, keyworded. UML defines
  «import», «access» and «merge» for packages and «use» for a generic usage
  dependency; a bare prose label such as "depends on" is not notation.
* Nested packages are drawn inside their parent's frame, which is what makes the
  dependency direction meaningful: Api -> Infrastructure -> Application -> Domain,
  inward only.

    Usage:  python diagrams/gen_package.py
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsl import Diagram  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_PNG = os.path.join(ROOT, "diagrams", "out")
OUT_DRAWIO = os.path.join(ROOT, "diagrams", "drawio")

X, PW = 200, 900
TAB = 26          # package title tab height in the renderer
SUB_H = 76
PKG_H = TAB + 22 + SUB_H + 22
GAP = 118         # vertical space between layers, where the dependencies live

LAYERS = [
    ("api", "MusicLounge.Api", ["Controllers", "Middleware"]),
    ("infra", "MusicLounge.Infrastructure",
     ["Persistence", "Services", "Jobs", "Hubs"]),
    ("app", "MusicLounge.Application", ["Common", "Feature folders (29)"]),
    ("dom", "MusicLounge.Domain", ["Entities (68)", "Enums", "Domain Events"]),
]


def build() -> Diagram:
    top = 92
    total = top + len(LAYERS) * PKG_H + (len(LAYERS) - 1) * GAP
    W = X + PW + 200
    H = total + 210
    d = Diagram("package-application", W, H)
    d.title("Package Diagram — Clean Architecture layering")

    ys: dict[str, tuple[float, float]] = {}
    for i, (key, title, subs) in enumerate(LAYERS):
        y = top + i * (PKG_H + GAP)
        d.package(f"p_{key}", X, y, PW, PKG_H, title)
        n = len(subs)
        sw = (PW - 40 - 20 * (n - 1)) / n
        for j, s in enumerate(subs):
            d.package(f"{key}_{j}", X + 20 + j * (sw + 20), y + TAB + 22, sw, SUB_H,
                      s, font_size=13, decorative=False)
        ys[key] = (y, y + PKG_H)

    cx = X + PW / 2
    for a, b in (("api", "infra"), ("infra", "app"), ("app", "dom")):
        d.edge([(cx, ys[a][1]), (cx, ys[b][0])], label="«use»", style="dashed",
               end_arrow="open", attached=(f"p_{a}", f"p_{b}"), label_side="right")

    d.note("n1", X, ys["dom"][1] + 54, PW, 108,
           "Dependencies point inward only. Domain depends on nothing: plain C# with no "
           "EF Core, no ASP.NET and no external package reference, so every business "
           "rule in it is unit-testable without a database or a host.\n"
           "Application declares the interfaces it needs; Infrastructure implements "
           "them, which is what lets the arrow point inward while the data flows out.",
           font_size=13)
    return d


def main() -> int:
    d = build()
    problems = d.validate()
    if problems:
        print(f"package-application: {len(problems)} geometry problem(s)")
        for p in problems:
            print("   ", p)
        return 1
    print("package-application: geometry clean — no overlaps, no clipped text")
    print(" ", d.save_png(OUT_PNG))
    print(" ", d.save_drawio(OUT_DRAWIO))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
