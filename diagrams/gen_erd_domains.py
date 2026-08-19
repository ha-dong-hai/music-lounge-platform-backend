"""Per-domain Entity Relationship Diagrams — Crow's Foot, fixed coordinates.

Why the schema is split
-----------------------
The full schema is 68 entities and 95 relationships. In one picture, 95 connectors
cannot each be followed to its own pair of entities — criterion C1 in STANDARDS.md,
and the earlier auto-laid-out attempt proved it.

Splitting naively by domain would be worse: only 44 of the 95 relationships are
internal to a domain, so **51 would simply disappear**. Each relationship is therefore
assigned to the domain of its **child** — the entity that actually holds the foreign
key — and any parent living elsewhere is drawn as a dashed boundary entity. Across the
set every relationship appears exactly once: none duplicated, none lost.

Layout
------
Entities form a single column and **all routing happens in the open field to their
right**, where each relationship gets a vertical channel of its own. Two properties
then hold by construction rather than by luck:

  * no connector can cross an entity, because no entity sits in the routing field;
  * no two connectors can be drawn as one line, because entities never overlap
    vertically, so every connection point has a y of its own, and every channel has
    an x of its own.

Two layouts were tried and abandoned first. A layered one made relationships that
skipped a column climb past the entities above them. A two-column one put entities on
both sides of the channel, and whenever a left-hand and a right-hand entity happened
to land on the same y their stubs merged — and no constant column offset fixes that,
since the closest approach it can buy is about half a slot.

    Usage:  python diagrams/gen_erd_domains.py
"""

from __future__ import annotations

import os
import re
import sys
from collections import defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsl import Diagram, TEXT_PAD, wrapped_size  # noqa: E402

# Bounded contexts. The assertion in main() fails the build if an entity is added to
# the domain model and not placed here, so the grouping cannot fall behind the schema.
GROUPS: dict[str, list[str]] = {
    "Identity & Access": [
        "User", "LoungeStaff", "DeviceToken", "Notification", "LoginFailureLog",
        "LoginSpikeAlertState", "KnownAdminSnapshot", "UploadedFile"],
    "Venue": [
        "MusicLounge", "SeatingZone", "LoungeImage", "LoungeGalleryImage",
        "VenueAtmosphere", "CustomCriteria", "VenueTourScene", "VenueTourHotspot",
        "VenueTourStitchAttempt", "VenuePenalty"],
    "Show & Catalogue": [
        "LoungeShow", "Performance", "Performer", "PerformerGenre",
        "PerformerSocialLink", "EventCategory", "MusicGenre", "Mood",
        "LoungeShowGenre", "LoungeShowMood", "LoungeShowAtmosphere",
        "EventCustomValue", "LoungeShowRating", "AiPosterGeneration", "EventModeration"],
    "Ticketing": [
        "TicketTier", "TicketPrice", "Ticket", "TicketHold", "PhysicalTicketDetail",
        "LivestreamTicketDetail"],
    "Livestream": ["Livestream", "LivestreamChatMessage"],
    "Money": [
        "Payment", "Account", "LedgerEntry", "Settlement", "Donation", "BankAccount",
        "RefundRequest", "OwnerSubscription", "SubscriptionPackage"],
    "Food & Beverage": ["FnbMenu", "FnbMenuItem", "FnbOrder", "OrderItem"],
    "Personalisation": [
        "AiRecommendation", "UserBehaviourLog", "UserEventScore", "UserCustomPreference",
        "UserFavouriteGenre", "UserFavouriteMood", "UserFavouriteAtmosphere", "Follow",
        "ShowWishlist"],
    "Operations": [
        "Complaint", "SystemConfig", "SystemConfigHistory", "PushFailureLog",
        "PushFailureAlertState"],
}

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ENT_DIR = os.path.join(ROOT, "src", "MusicLounge.Domain", "Entities")
OUT_PNG = os.path.join(ROOT, "diagrams", "out")
OUT_DRAWIO = os.path.join(ROOT, "diagrams", "drawio")

NAV = re.compile(r"public\s+(?:virtual\s+)?([A-Z]\w*)(\?)?\s+(\w+)\s*\{\s*get")
FK = re.compile(r"public\s+(?:int|Guid)(\?)?\s+(\w+Id)\s*\{\s*get")

BW = 260
SLOT = 13            # vertical spacing between two connections on the same entity
ROW_GAP = 54
CHANNEL = 34         # horizontal spacing between two routing channels
LEFT = 60
TOP = 130

ONE, ZERO_ONE, MANY = "one_one", "zero_one", "zero_many"

MERGE = {"Livestream": "Ticketing"}
TITLE = {
    "Identity & Access": "Identity and Access", "Venue": "Venue",
    "Show & Catalogue": "Show and Catalogue", "Ticketing": "Ticketing and Livestream",
    "Money": "Money", "Food & Beverage": "Food and Beverage",
    "Personalisation": "Personalisation", "Operations": "Operations",
}


def read_relationships() -> list[tuple[str, str, bool, str]]:
    """(parent, child, parent_optional, fk_property) straight from the entity classes."""
    names = {f[:-3] for f in os.listdir(ENT_DIR) if f.endswith(".cs")}
    out = []
    for name in sorted(names):
        src = open(os.path.join(ENT_DIR, name + ".cs"), encoding="utf-8").read()
        fks = {m.group(2): bool(m.group(1)) for m in FK.finditer(src)}
        for m in NAV.finditer(src):
            target, prop = m.group(1), m.group(3)
            if target in names and target != name and prop + "Id" in fks:
                out.append((target, name, fks[prop + "Id"], prop))
    return out


def build(domain: str, members: list[str],
          rels: list[tuple[str, str, bool, str]]) -> Diagram:
    own = set(members)
    mine = [r for r in rels if r[1] in own]
    external = sorted({r[0] for r in mine if r[0] not in own})

    degree: dict[str, int] = defaultdict(int)
    for p, c, _, _ in mine:
        degree[p] += 1
        degree[c] += 1

    # Boundary entities lead, then the busiest, so the long stubs stay near the top.
    ordered = external + sorted(own, key=lambda n: (-degree[n], n))
    height_of = {n: max(58, 26 + degree[n] * SLOT) for n in ordered}

    n_channels = max(1, len(mine))
    right_x = LEFT + BW + 40 + n_channels * CHANNEL

    pos: dict[str, tuple[float, float]] = {}            # y, h — one column, x is LEFT
    y = TOP
    for name in ordered:
        pos[name] = (y, height_of[name])
        y += height_of[name] + ROW_GAP

    grid_bottom = max((p[0] + p[1] for p in pos.values()), default=TOP)
    legend_text = (
        "Crow's Foot: the inner mark is the minimum, the outer the maximum. "
        "|| exactly one · |o zero or one · }o zero or many. A nullable foreign key "
        "makes the minimum zero, so that end takes a circle.\n"
        "A dashed box is an entity owned by another domain, shown here so the "
        "relationship reaching into this domain is complete. Each relationship is "
        "drawn in the domain of the entity holding the foreign key, so across the "
        "whole set every one of the 95 appears exactly once.")
    width = right_x + LEFT
    note_w = width - 2 * LEFT
    _, lh = wrapped_size(legend_text, note_w - 2 * TEXT_PAD - 6, 13)
    legend_h = lh + 2 * TEXT_PAD + 20
    height = grid_bottom + 70 + legend_h + 40

    d = Diagram(f"erd-{domain.lower().replace(' & ', '-').replace(' ', '-')}",
                int(width), int(height))
    d.title(f"Entity Relationship Diagram — {TITLE.get(domain, domain)}")

    for name, (ny, h) in pos.items():
        if name in own:
            d.box(name, LEFT, ny, BW, h, name, font_size=14, bold=True)
        else:
            d.box_ext(name, LEFT, ny, BW, h, f"{name}\n(other domain)", font_size=13)

    # Each entity hands out its connection points top-down. Because entities never
    # overlap vertically, no two connection points anywhere on the page share a y.
    used: dict[str, int] = defaultdict(int)

    def anchor(name: str) -> float:
        ny, _h = pos[name]
        k = used[name]
        used[name] += 1
        return ny + 20 + k * SLOT

    ch_x = LEFT + BW + 40
    edge_x = LEFT + BW
    for i, (parent, child, optional, _prop) in enumerate(mine):
        py, cy_ = anchor(parent), anchor(child)
        gx = ch_x + i * CHANNEL
        d.edge([(edge_x, py), (gx, py), (gx, cy_), (edge_x, cy_)],
               start_arrow=ZERO_ONE if optional else ONE, end_arrow=MANY,
               attached=(parent, child))

    d.note("legend", LEFT, grid_bottom + 60, note_w, legend_h,
           legend_text, font_size=13)
    return d


def main() -> int:
    entities = {f[:-3] for f in os.listdir(ENT_DIR) if f.endswith(".cs")}
    grouped = {e for v in GROUPS.values() for e in v}
    assert not entities - grouped, f"entities missing from GROUPS: {sorted(entities - grouped)}"
    assert not grouped - entities, f"GROUPS names that are not entities: {sorted(grouped - entities)}"

    rels = read_relationships()
    groups: dict[str, list[str]] = {}
    for name, members in GROUPS.items():
        groups.setdefault(MERGE.get(name, name), []).extend(members)

    drawn = failed = 0
    for domain, members in groups.items():
        d = build(domain, members, rels)
        problems = d.validate()
        mine = len([r for r in rels if r[1] in set(members)])
        if problems:
            failed += 1
            print(f"{d.name}: {len(problems)} problem(s)")
            for p in problems[:6]:
                print("   ", p)
        else:
            d.save_png(OUT_PNG)
            d.save_drawio(OUT_DRAWIO)
            drawn += mine
            print(f"{d.name:32} clean  {d.width}x{d.height}  {mine} relationships")
    print(f"\n{drawn} of {len(rels)} relationships drawn across {len(groups)} diagrams")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
