"""Mechanical conformance checks for every diagram under diagrams/src/.

A diagram states facts about the system, so the facts are checked rather than trusted.
This mirrors spec/build/facts.py, which self-checks the report figures for the same
reason: they drifted apart before it existed.

The checks split into two families.

  Content    — does the drawing agree with the code and the use case catalogue?
               Catches invented use case names, dropped use cases, invented enum
               states, and entities that no longer exist.

  Rendering  — will the drawing survive PlantUML intact?
               Catches the two failure modes that produce a *silently wrong* image:
               hand-written guard brackets that eat words, and renders large enough
               that PlantUML crops them without reporting an error.

    Usage:  python diagrams/validate.py          # after build.ps1 has rendered
            python diagrams/validate.py --src    # source-only, skip PNG checks
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "diagrams" / "src"
OUT = ROOT / "diagrams" / "out"
ENUMS = ROOT / "src" / "MusicLounge.Domain" / "Enums"
ENTITIES = ROOT / "src" / "MusicLounge.Domain" / "Entities"

# PlantUML crops without error past its limit; build.ps1 raises it to 8192. Anything
# within 2% of that is one label away from clipping, so it fails here too.
RENDER_LIMIT = 8192
NEAR_LIMIT = int(RENDER_LIMIT * 0.98)

failures: list[str] = []
notes: list[str] = []


def fail(check: str, detail: str) -> None:
    failures.append(f"[{check}] {detail}")


def read(p: Path) -> str:
    return p.read_text(encoding="utf-8")


# ── the use case catalogue is the single source of truth for names ───────────
sys.path.insert(0, str(ROOT / "spec" / "build"))
import usecases  # noqa: E402

CATALOGUE = {name for _, _, name, _, _ in usecases.numbered()}
TOTAL = usecases.TOTAL

RE_USECASE = re.compile(r'usecase\s+"([^"]+)"')
RE_STATE = re.compile(r'state\s+"([^"]+)"')
RE_ENTITY = re.compile(r'entity\s+"([^"]+)"')
RE_ENUM_MEMBER = re.compile(r"^\s*([A-Z][A-Za-z0-9]*)\s*(?:=\s*\d+\s*)?,?\s*(?://.*)?$")

# PlantUML applies these inside labels and silently rewrites the text.
CREOLE = {
    "//": "italic (also breaks any URL)",
    "**": "bold",
    "__": "underline",
    "~~": "strikethrough",
    "[[": "hyperlink",
}


def enum_members(enum_name: str) -> set[str]:
    """Member names of Domain/Enums/<enum_name>.cs."""
    path = ENUMS / f"{enum_name}.cs"
    if not path.exists():
        fail("enum-exists", f"{path.relative_to(ROOT)} not found")
        return set()
    members, inside = set(), False
    for line in read(path).splitlines():
        stripped = line.strip()
        if stripped.startswith("public enum"):
            inside = True
            continue
        if inside:
            if stripped.startswith("}"):
                break
            if not stripped or stripped.startswith(("//", "{")):
                continue
            m = RE_ENUM_MEMBER.match(stripped.split("//")[0].rstrip())
            if m:
                members.add(m.group(1))
    return members


# ── 1. every use case name is real, and all 109 are drawn ────────────────────
def check_use_cases() -> None:
    """Use case diagrams are generated at fixed coordinates, so the names live in
    gen_usecases.SPECS rather than in .puml source."""
    sys.path.insert(0, str(ROOT / "diagrams"))
    try:
        from gen_usecases import SPECS
    except Exception as exc:                       # pragma: no cover
        fail("uc-present", f"cannot import gen_usecases: {exc}")
        return

    drawn: set[str] = set()
    for spec in SPECS:
        for _group, ucs in spec["groups"]:
            for name in ucs:
                if name not in CATALOGUE:
                    fail("uc-name", f"{spec['name']}: '{name}' is not in usecases.py")
                drawn.add(name)

    for name in sorted(CATALOGUE - drawn):
        fail("uc-coverage", f"never drawn: '{name}'")
    notes.append(f"use cases {len(drawn & CATALOGUE)}/{TOTAL} across {len(SPECS)} diagrams")


# ── 1b. the domain ERDs together cover every entity and every relationship ───
def check_erd_domains() -> None:
    sys.path.insert(0, str(ROOT / "diagrams"))
    try:
        import gen_erd_domains as g
    except Exception as exc:                       # pragma: no cover
        fail("erd-present", f"cannot import gen_erd_domains: {exc}")
        return

    entities = {p.stem for p in ENTITIES.glob("*.cs")}
    grouped = {e for v in g.GROUPS.values() for e in v}
    for name in sorted(entities - grouped):
        fail("erd-coverage", f"entity {name} is in no domain group")
    for name in sorted(grouped - entities):
        fail("erd-name", f"'{name}' is grouped but is not an entity")

    rels = g.read_relationships()
    drawn = sum(len([r for r in rels if r[1] in set(members)])
                for members in g.GROUPS.values())
    if drawn != len(rels):
        fail("erd-relationships",
             f"domain diagrams draw {drawn} of {len(rels)} relationships")
    notes.append(f"ERD {len(entities)} entities, {len(rels)} relationships, "
                 f"{drawn} drawn across {len(g.GROUPS)} domain groups")


# ── 2. every state in a state machine exists in the real enum ────────────────
# Maps a state diagram to the enum it claims to model.
STATE_DIAGRAMS = {
    "state-show.puml": "LoungeShowStatus",
    "state-ticket.puml": "TicketStatus",
}


def check_states() -> None:
    """State machines are generated at fixed coordinates, so the states live in
    gen_states.MACHINES rather than in .puml source."""
    sys.path.insert(0, str(ROOT / "diagrams"))
    try:
        from gen_states import MACHINES
    except Exception as exc:                       # pragma: no cover
        fail("state-present", f"cannot import gen_states: {exc}")
        return

    for key, machine in MACHINES.items():
        enum_name = machine["enum"]
        real = enum_members(enum_name)
        if not real:
            continue
        drawn = set(machine["states"])
        for name in sorted(drawn - real):
            fail("state-name", f"state-{key}: '{name}' is not a member of {enum_name}")
        for name in sorted(real - drawn):
            fail("state-coverage", f"state-{key}: {enum_name}.{name} is never drawn")
        notes.append(f"state-{key}: {len(drawn & real)}/{len(real)} {enum_name} states")


# ── 4. guards must not carry hand-written brackets ───────────────────────────
# PlantUML adds the brackets itself; writing them by hand eats part of the label.
RE_BAD_GUARD = re.compile(r"^\s*(alt|else|opt|loop|par|break|critical)\s+\[", re.M)


def check_guards() -> None:
    for f in sorted(SRC.glob("*.puml")):
        for m in RE_BAD_GUARD.finditer(read(f)):
            line = read(f)[: m.start()].count("\n") + 1
            fail("guard", f"{f.name}:{line}: '{m.group(1)} [' — drop the brackets, "
                          "PlantUML adds them and the leading word is lost")


# ── 5. no Creole markup that silently rewrites a label ───────────────────────
def check_creole() -> None:
    for f in sorted(SRC.glob("*.puml")):
        for lineno, line in enumerate(read(f).splitlines(), 1):
            code = line.split("'")[0] if line.lstrip().startswith("'") else line
            if line.lstrip().startswith("'"):
                continue
            for token, effect in CREOLE.items():
                if token in code:
                    fail("creole", f"{f.name}:{lineno}: contains '{token}' → {effect}")


# ── 6. nothing rendered close enough to the limit to be cropped ──────────────
def check_renders() -> None:
    try:
        from PIL import Image
    except ImportError:
        notes.append("Pillow not installed — skipped render size check")
        return
    if not OUT.exists():
        fail("render-present", "diagrams/out does not exist; run build.ps1 first")
        return
    for src in sorted(SRC.glob("*.puml")):
        if src.name == "_style.puml":
            continue
        png = OUT / f"{src.stem}.png"
        if not png.exists():
            fail("render-present", f"{png.name} was never rendered")
            continue
        w, h = Image.open(png).size
        if max(w, h) >= NEAR_LIMIT:
            fail("render-size", f"{png.name}: {w}x{h} is at or near the {RENDER_LIMIT}px "
                                "limit — PlantUML crops silently past it")


def main() -> int:
    source_only = "--src" in sys.argv

    check_use_cases()
    check_erd_domains()
    check_states()
    check_guards()
    check_creole()
    if not source_only:
        check_renders()

    for n in notes:
        print(f"  {n}")

    if failures:
        print(f"\nFAILED — {len(failures)} problem(s):\n")
        for f in failures:
            print(f"  {f}")
        return 1

    print("\nOK — every diagram agrees with the code.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
