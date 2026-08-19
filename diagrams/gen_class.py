"""Class Diagram — UML 2.5.1, fixed coordinates.

Notation (criteria group A in STANDARDS.md)
-------------------------------------------
* A Class is a box of **three compartments**: name, attributes, operations. Cramming
  all of it into one box, as the auto-laid-out version did, is a notation error.
* **Realisation** — a class implementing an interface — is a dashed line with a
  **closed hollow triangle** pointing at the interface. An open arrowhead would mean
  a dependency, which is a different relationship.
* **Dependency** («use») is a dashed line with an open arrowhead.
* **Association** — one class holding a reference to another — is a plain solid line.

Content is taken from the code, not from the pattern's textbook form: controllers
inject MediatR's `ISender`, and Application/DependencyInjection.cs registers **four**
pipeline behaviours in the order shown. Only TransactionBehavior is constrained to
`ICommand<TResponse>`, so a query never opens a transaction.

Layout (criteria group C): the pipeline runs straight down the middle, the request
objects sit in a left column with vertical links, and the data-access chain sits in a
bottom row with horizontal links, so every relationship is one straight segment.

    Usage:  python diagrams/gen_class.py
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsl import Diagram  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_PNG = os.path.join(ROOT, "diagrams", "out")
OUT_DRAWIO = os.path.join(ROOT, "diagrams", "drawio")

W, H = 1500, 1400

REAL = dict(style="dashed", end_arrow="triangle")   # realisation
DEP = dict(style="dashed", end_arrow="open")        # dependency
ASSOC = dict(style="solid", end_arrow="none")       # association


def build() -> Diagram:
    d = Diagram("class-cqrs", W, H)
    d.title("Class Diagram — CQRS request pipeline")

    # ── the pipeline, straight down the middle ──────────────────────────────
    d.uml_class("ctrl", 600, 120, 400, 110, "XxxController",
                "- sender : ISender", "+ Action() : IActionResult")
    d.uml_class("isender", 600, 300, 400, 100, "«interface»\nISender",
                "", "+ Send(request, ct) : TResponse")
    d.uml_class("ibehavior", 460, 480, 760, 100,
                "«interface»\nIPipelineBehavior<TRequest, TResponse>",
                "", "+ Handle(request, next, ct) : TResponse")
    d.uml_class("ihandler", 600, 860, 400, 100,
                "«interface»\nIRequestHandler<TRequest, TResponse>",
                "", "+ Handle(request, ct) : TResponse")
    d.uml_class("handler", 600, 1040, 400, 110, "XxxCommandHandler",
                "- uow : IUnitOfWork", "+ Handle(command, ct) : TResponse")

    # ── the four registered behaviours, in registration order ───────────────
    behaviours = [("b_log", "LoggingBehavior"), ("b_val", "ValidationBehavior"),
                  ("b_act", "ActiveUserBehavior"), ("b_txn", "TransactionBehavior")]
    for i, (key, name) in enumerate(behaviours):
        x = 470 + i * 190
        d.uml_class(key, x, 650, 180, 90, f"{i + 1}.\n{name}")
        d.edge([(x + 90, 650), (x + 90, 580)], attached=(key, "ibehavior"), **REAL)

    # ── the request objects, left column, vertical links ────────────────────
    d.uml_class("val", 80, 300, 340, 90, "XxxCommandValidator",
                "", "+ rules per field")
    d.uml_class("cmd", 80, 440, 340, 90, "XxxCommand", "«record»\n+ parameters")
    d.uml_class("ireq", 80, 580, 340, 90, "«interface»\nIRequest<TResponse>")

    d.edge([(250, 390), (250, 440)], label="validates", attached=("val", "cmd"), **DEP)
    d.edge([(250, 530), (250, 580)], attached=("cmd", "ireq"), **REAL)
    d.edge([(600, 175), (440, 175), (440, 485), (420, 485)], label="builds",
           attached=("ctrl", "cmd"), label_pos=0, **DEP)

    # ── the pipeline links ──────────────────────────────────────────────────
    d.edge([(800, 230), (800, 300)], label="uses", attached=("ctrl", "isender"), **ASSOC)
    d.edge([(800, 400), (800, 480)], label="«use»", attached=("isender", "ibehavior"),
           **DEP)
    # Routed around the right of the behaviour row rather than threaded through the
    # 10px gap between two of its boxes, where it read as touching both.
    d.edge([(1220, 530), (1300, 530), (1300, 910), (1000, 910)], label="«use»",
           attached=("ibehavior", "ihandler"), label_pos=1, label_side="right", **DEP)
    d.edge([(800, 1040), (800, 960)], attached=("handler", "ihandler"), **REAL)

    # ── data access, bottom row, horizontal links ───────────────────────────
    d.uml_class("uow", 200, 1240, 300, 90, "«interface»\nIUnitOfWork",
                "", "+ SaveChangesAsync(ct)")
    d.uml_class("irepo", 630, 1240, 300, 90, "«interface»\nIRepository<T, TKey>",
                "", "+ GetByIdAsync · Add · Update")
    d.uml_class("entity", 1060, 1240, 300, 90, "Entity")

    d.edge([(700, 1150), (700, 1190), (350, 1190), (350, 1240)], label="uses",
           attached=("handler", "uow"), label_pos=1, **ASSOC)
    d.edge([(500, 1285), (630, 1285)], label="exposes", attached=("uow", "irepo"),
           **ASSOC)
    d.edge([(930, 1285), (1060, 1285)], label="persists", attached=("irepo", "entity"),
           **ASSOC)

    # Parked in the open ground under the request column rather than at the foot of
    # the page, which both fills that gap and shortens the diagram.
    d.note("n1", 80, 880, 480, 262,
           "Behaviours run around every handler in registration order, outermost "
           "first: LoggingBehavior, ValidationBehavior, ActiveUserBehavior, "
           "TransactionBehavior. Only TransactionBehavior is constrained to "
           "ICommand<TResponse>, so a query never opens a transaction.\n"
           "A dashed line with a hollow triangle is a realisation; with an open "
           "arrowhead it is a dependency; a plain solid line is an association.",
           font_size=13)
    return d


def main() -> int:
    d = build()
    problems = d.validate()
    if problems:
        print(f"class-cqrs: {len(problems)} geometry problem(s)")
        for p in problems:
            print("   ", p)
        return 1
    print("class-cqrs: geometry clean — no overlaps, no collinear runs, no clipped text")
    print(" ", d.save_png(OUT_PNG))
    print(" ", d.save_drawio(OUT_DRAWIO))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
