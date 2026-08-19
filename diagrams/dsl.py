"""Fixed-coordinate diagram primitives with a geometric non-overlap proof.

Why this exists
---------------
PlantUML and Graphviz choose their own layout. On a dense graph that means edge
labels land on unrelated connectors and connectors cut through component boxes, and
no amount of spacing tuning removes the possibility — the layout engine simply is not
constrained to avoid it. A reader then cannot tell which label belongs to which line,
which is worse than an ugly diagram: it is a wrong diagram.

Here every shape, every connector waypoint and every label rectangle has an absolute
coordinate chosen by the author. Because the geometry is fully known, `validate()`
can *prove* the drawing is clean rather than leaving it to the eye:

    * no two shapes overlap
    * no connector segment passes through a shape it does not attach to
    * no label rectangle touches a shape or any connector other than its own
    * everything sits inside the canvas

Output is both .drawio XML (hand-editable afterwards, matching the convention already
set by gen_drawio.py) and a directly rendered PNG, so the result can be inspected
without installing the draw.io CLI.

House style: black on white, no fill, no shadow, orthogonal connectors only.
"""

from __future__ import annotations

import html
import os
from dataclasses import dataclass, field

from PIL import Image, ImageDraw, ImageFont

# ── canvas + text metrics ────────────────────────────────────────────────────
SCALE = 2  # PNG supersampling; .drawio coordinates stay at 1x
FONT_DIR = r"C:\Windows\Fonts"
FONT_REGULAR = os.path.join(FONT_DIR, "segoeui.ttf")
FONT_BOLD = os.path.join(FONT_DIR, "segoeuib.ttf")

_font_cache: dict[tuple[str, int], ImageFont.FreeTypeFont] = {}


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    key = (FONT_BOLD if bold else FONT_REGULAR, size)
    if key not in _font_cache:
        try:
            _font_cache[key] = ImageFont.truetype(key[0], size)
        except OSError:  # pragma: no cover - only on a machine without Segoe UI
            _font_cache[key] = ImageFont.load_default()
    return _font_cache[key]


_measure_img = Image.new("RGB", (8, 8))
_measure = ImageDraw.Draw(_measure_img)


LINE_GAP = 6
TEXT_PAD = 10          # inset from a shape's border to its text
ELLIPSE_TEXT_RATIO = 0.68   # an inscribed ellipse offers less width than its box


NODE_DEPTH = 16        # 3D offset on a UML Node glyph


def text_area(r, kind: str) -> float:
    """Usable text width inside a shape of this kind."""
    if kind == "ellipse":
        return r.w * ELLIPSE_TEXT_RATIO
    if kind == "node3d":            # text lives on the front face only
        return r.w - NODE_DEPTH - 2 * TEXT_PAD
    return r.w - 2 * TEXT_PAD


def text_height_area(r, kind: str) -> float:
    """Usable text height inside a shape of this kind."""
    if kind == "ellipse":
        return r.h * 0.72
    if kind == "node3d":
        return r.h - NODE_DEPTH - 2 * TEXT_PAD
    return r.h - 2 * TEXT_PAD


def text_size(s: str, size: int, bold: bool = False) -> tuple[int, int]:
    """Width and height of a possibly multi-line string, in diagram units."""
    f = font(size, bold)
    w = h = 0
    for line in s.split("\n"):
        box = _measure.textbbox((0, 0), line, font=f)
        w = max(w, box[2] - box[0])
        h += int((box[3] - box[1]) * 1.0) + LINE_GAP
    return w, max(h - LINE_GAP, 0)


def line_height(size: int, bold: bool = False) -> int:
    box = _measure.textbbox((0, 0), "Hg", font=font(size, bold))
    return (box[3] - box[1]) + LINE_GAP


def wrap(text: str, max_w: float, size: int, bold: bool = False) -> list[str]:
    """Greedy word wrap to max_w, honouring explicit newlines.

    Without this the renderer draws a single long line straight through the shape
    border and off the canvas — which is exactly how the first attempt produced
    notes whose last words were cut off.
    """
    out: list[str] = []
    for para in text.split("\n"):
        if not para.strip():
            out.append("")
            continue
        line = ""
        for word in para.split(" "):
            trial = f"{line} {word}".strip()
            if not line or text_size(trial, size, bold)[0] <= max_w:
                line = trial
            else:
                out.append(line)
                line = word
        out.append(line)
    return out


def wrapped_size(text: str, max_w: float, size: int, bold: bool = False) -> tuple[int, int]:
    lines = wrap(text, max_w, size, bold)
    w = max((text_size(l, size, bold)[0] for l in lines), default=0)
    return w, len(lines) * line_height(size, bold) - LINE_GAP


# ── geometry ─────────────────────────────────────────────────────────────────
@dataclass(frozen=True)
class Rect:
    x: float
    y: float
    w: float
    h: float

    @property
    def x2(self) -> float:
        return self.x + self.w

    @property
    def y2(self) -> float:
        return self.y + self.h

    @property
    def cx(self) -> float:
        return self.x + self.w / 2

    @property
    def cy(self) -> float:
        return self.y + self.h / 2

    def inflate(self, m: float) -> "Rect":
        return Rect(self.x - m, self.y - m, self.w + 2 * m, self.h + 2 * m)

    def overlaps(self, other: "Rect") -> bool:
        return not (self.x2 <= other.x or other.x2 <= self.x
                    or self.y2 <= other.y or other.y2 <= self.y)

    def contains_point(self, px: float, py: float) -> bool:
        return self.x <= px <= self.x2 and self.y <= py <= self.y2


def _seg_intersects_rect(p1: tuple[float, float], p2: tuple[float, float], r: Rect) -> bool:
    """True when an axis-aligned segment passes through the interior of r.

    Only orthogonal segments are produced by this module, so the test reduces to an
    interval overlap on each axis rather than a general segment/AABB clip.
    """
    (x1, y1), (x2, y2) = p1, p2
    if abs(y1 - y2) < 1e-9:  # horizontal
        lo, hi = sorted((x1, x2))
        return r.y < y1 < r.y2 and lo < r.x2 and r.x < hi
    if abs(x1 - x2) < 1e-9:  # vertical
        lo, hi = sorted((y1, y2))
        return r.x < x1 < r.x2 and lo < r.y2 and r.y < hi
    raise ValueError(f"non-orthogonal segment {p1}->{p2}")


def _collinear_run(a1, a2, b1, b2, tol: float = 2.0) -> float:
    """Length over which two orthogonal segments run along the same line.

    Two connectors drawn on top of each other render as a single stroke, so neither
    can be followed to its endpoints. That is a readability defect the eye cannot
    catch reliably on a dense diagram, so it is measured instead.
    """
    a_horiz = abs(a1[1] - a2[1]) < 1e-9
    b_horiz = abs(b1[1] - b2[1]) < 1e-9
    if a_horiz != b_horiz:
        return 0.0
    if a_horiz:
        if abs(a1[1] - b1[1]) > tol:
            return 0.0
        lo = max(min(a1[0], a2[0]), min(b1[0], b2[0]))
        hi = min(max(a1[0], a2[0]), max(b1[0], b2[0]))
    else:
        if abs(a1[0] - b1[0]) > tol:
            return 0.0
        lo = max(min(a1[1], a2[1]), min(b1[1], b2[1]))
        hi = min(max(a1[1], a2[1]), max(b1[1], b2[1]))
    return max(0.0, hi - lo)


def _segs_touch(a1, a2, b1, b2, tol: float = 0.5) -> bool:
    """True when two orthogonal segments cross or run along each other."""
    ax_lo, ax_hi = sorted((a1[0], a2[0]))
    ay_lo, ay_hi = sorted((a1[1], a2[1]))
    bx_lo, bx_hi = sorted((b1[0], b2[0]))
    by_lo, by_hi = sorted((b1[1], b2[1]))
    return (ax_lo - tol <= bx_hi and bx_lo - tol <= ax_hi
            and ay_lo - tol <= by_hi and by_lo - tol <= ay_hi)


# ── shapes ───────────────────────────────────────────────────────────────────
@dataclass
class Shape:
    kind: str            # box | ellipse | actor | note | package | title | legend
    rect: Rect
    label: str
    font_size: int = 13
    bold: bool = False
    align: str = "center"
    # Shapes marked decorative are excluded from the connector-crossing test:
    # a package frame is a container, so its children's edges legitimately cross it.
    decorative: bool = False

    def port(self, side: str, offset: float = 0.0) -> tuple[float, float]:
        """A point on the shape's boundary. `offset` slides along that side."""
        r = self.rect
        if side == "n":
            return (r.cx + offset, r.y)
        if side == "s":
            return (r.cx + offset, r.y2)
        if side == "w":
            return (r.x, r.cy + offset)
        if side == "e":
            return (r.x2, r.cy + offset)
        raise ValueError(side)


@dataclass
class Edge:
    points: list[tuple[float, float]]
    label: str = ""
    label_rect: Rect | None = None
    style: str = "solid"        # solid | dashed
    start_arrow: str = "none"   # none | open | filled | crow | one | zero_one | one_only
    end_arrow: str = "open"
    attached: tuple[str, ...] = ()   # ids of shapes this edge is allowed to touch


@dataclass
class Diagram:
    name: str
    width: int
    height: int
    shapes: dict[str, Shape] = field(default_factory=dict)
    edges: list[Edge] = field(default_factory=list)
    _n: int = 0

    # ── authoring ────────────────────────────────────────────────────────────
    def _id(self) -> str:
        self._n += 1
        return f"s{self._n}"

    def add(self, shape: Shape, key: str | None = None) -> str:
        key = key or self._id()
        if key in self.shapes:
            raise ValueError(f"duplicate shape id {key}")
        self.shapes[key] = shape
        return key

    def box(self, key, x, y, w, h, label, font_size=13, bold=False) -> Shape:
        s = Shape("box", Rect(x, y, w, h), label, font_size, bold)
        self.add(s, key)
        return s

    def ellipse(self, key, x, y, w, h, label, font_size=13, bold=False) -> Shape:
        s = Shape("ellipse", Rect(x, y, w, h), label, font_size, bold)
        self.add(s, key)
        return s

    # Stick figure metrics, shared by the renderer so the label can never land on
    # the legs: head 0..26, torso 26..48, arms at 34, legs 48..66, label at 72.
    ACTOR_H = 96
    ACTOR_ARM_DY = 34

    def actor(self, key, x, y, label, font_size=13) -> Shape:
        """Stick figure. x,y is the top-left of the glyph including its caption."""
        tw, _ = text_size(label, font_size)
        w = max(46, tw)
        s = Shape("actor", Rect(x, y, w, self.ACTOR_H), label, font_size)
        self.add(s, key)
        return s

    def node3d(self, key, x, y, w, h, label, font_size=13) -> Shape:
        """UML Node: a perspective three-dimensional view of a cube.

        The rect covers the whole glyph including the depth offset, so overlap
        checking accounts for the 3D extension rather than only the front face.
        """
        s = Shape("node3d", Rect(x, y, w, h), label, font_size)
        self.add(s, key)
        return s

    def uml_class(self, key, x, y, w, h, name, attributes="", operations="",
                  font_size=13) -> Shape:
        """UML Class: name / attributes / operations compartments, in that order."""
        label = "|".join((name, attributes, operations))
        s = Shape("class", Rect(x, y, w, h), label, font_size)
        self.add(s, key)
        return s

    def pseudostate(self, key, x, y, kind="initial", d=22) -> Shape:
        """UML pseudostate: `initial` is a filled disc, `final` a ring around one."""
        s = Shape("initial" if kind == "initial" else "final", Rect(x, y, d, d), "")
        self.add(s, key)
        return s

    def box_ext(self, key, x, y, w, h, label, font_size=13, bold=False) -> Shape:
        """An entity owned by another domain, drawn dashed as a boundary reference."""
        s = Shape("box_ext", Rect(x, y, w, h), label, font_size, bold)
        self.add(s, key)
        return s

    def label(self, key, x, y, w, h, text, font_size=13, bold=True,
              align="left") -> Shape:
        """Borderless text that still takes part in the overlap proof."""
        s = Shape("label", Rect(x, y, w, h), text, font_size, bold, align=align)
        self.add(s, key)
        return s

    def note(self, key, x, y, w, h, label, font_size=12) -> Shape:
        s = Shape("note", Rect(x, y, w, h), label, font_size, align="left")
        self.add(s, key)
        return s

    def package(self, key, x, y, w, h, label, font_size=14,
                decorative=True) -> Shape:
        """UML Package. Outer frames are decorative by default, so the connectors of
        their own children may legitimately cross the border. A nested package that
        holds no children should pass decorative=False so it is overlap-checked."""
        s = Shape("package", Rect(x, y, w, h), label, font_size, bold=True,
                  align="left", decorative=decorative)
        self.add(s, key)
        return s

    def title(self, text, y=18, font_size=17) -> Shape:
        # The rect is padded past the measured text so the renderer's word wrap has
        # no reason to break the title onto a second line, which would drop it onto
        # whatever sits below.
        tw, th = text_size(text, font_size, bold=True)
        w = tw + 2 * TEXT_PAD + 8
        s = Shape("title", Rect((self.width - w) / 2, y, w, th + 6), text,
                  font_size, bold=True, decorative=True)
        self.add(s, "__title__")
        return s

    def title_width(self, text, font_size=17) -> int:
        """Canvas width a title needs; callers size the canvas to at least this."""
        return text_size(text, font_size, bold=True)[0] + 2 * TEXT_PAD + 88

    def label_collides(self, r: Rect) -> bool:
        """True if a label placed at r would touch a shape, a connector or a label.

        Exposed so a generator can search for a free spot instead of hard-coding one:
        on a dense screen flow there is no single offset that works for every label,
        and picking the first non-colliding candidate is far more robust than tuning
        thirty of them by hand.
        """
        probe = r.inflate(-1)
        for s in self.shapes.values():
            if not s.decorative and probe.overlaps(s.rect):
                return True
        for e in self.edges:
            for a, b in zip(e.points, e.points[1:]):
                if _seg_intersects_rect(a, b, probe):
                    return True
            if e.label_rect and probe.overlaps(e.label_rect):
                return True
        return False

    def measure_label(self, text: str, cx: float, cy: float) -> Rect:
        """The rectangle a label of this text would occupy centred on (cx, cy)."""
        tw, th = text_size(text, 12)
        return Rect(cx - (tw + 8) / 2, cy - (th + 8) / 2, tw + 8, th + 8)

    def edge(self, points, label="", style="solid", start_arrow="none",
             end_arrow="open", attached=(), label_pos=None, label_side="above",
             label_t=0.5, label_rect=None):
        """Add an orthogonal connector.

        `points` must alternate axis-aligned segments. `label_pos` is the index of the
        segment the label sits on (default: the longest one); the label is placed in
        its own rectangle beside that segment so it never sits on top of the line.
        """
        pts = [(float(a), float(b)) for a, b in points]
        for a, b in zip(pts, pts[1:]):
            if abs(a[0] - b[0]) > 1e-9 and abs(a[1] - b[1]) > 1e-9:
                raise ValueError(f"{self.name}: non-orthogonal segment {a}->{b}")
        e = Edge(pts, label, None, style, start_arrow, end_arrow, tuple(attached))
        if label:
            e.label_rect = (label_rect if label_rect is not None
                            else self._place_label(pts, label, label_pos, label_side,
                                                   label_t))
        self.edges.append(e)
        return e

    def _place_label(self, pts, label, label_pos, label_side, label_t=0.5) -> Rect:
        segs = list(zip(pts, pts[1:]))
        if label_pos is None:
            label_pos = max(range(len(segs)),
                            key=lambda i: abs(segs[i][0][0] - segs[i][1][0])
                            + abs(segs[i][0][1] - segs[i][1][1]))
        (x1, y1), (x2, y2) = segs[label_pos]
        tw, th = text_size(label, 12)
        pad = 4
        w, h = tw + pad * 2, th + pad * 2
        # label_t slides the label along the chosen segment; the midpoint is often the
        # one place it collides with something, and moving it is cheaper than
        # redesigning the route.
        mx, my = x1 + (x2 - x1) * label_t, y1 + (y2 - y1) * label_t
        gap = 5
        if abs(y1 - y2) < 1e-9:  # horizontal segment -> label above or below
            y = my - h - gap if label_side in ("above", "left") else my + gap
            return Rect(mx - w / 2, y, w, h)
        x = mx - w - gap if label_side in ("left", "above") else mx + gap
        return Rect(x, my - h / 2, w, h)

    # ── proof ────────────────────────────────────────────────────────────────
    def validate(self, margin: float = 2.0) -> list[str]:
        problems: list[str] = []
        solid = {k: s for k, s in self.shapes.items() if not s.decorative}

        for k, s in self.shapes.items():
            r = s.rect
            if r.x < 0 or r.y < 0 or r.x2 > self.width or r.y2 > self.height:
                problems.append(f"{k}: outside the canvas ({r})")
            # Text that does not fit is drawn straight through the border, which is
            # how the first attempt shipped notes with their last words cut off.
            if s.kind in ("box", "box_ext", "ellipse", "note", "class",
                          "node3d") and s.label:
                # Only a class label uses "|" as a compartment separator. Substituting
                # it everywhere miscounted any text containing the character — the
                # Crow's Foot legend, whose "||" and "|o" are notation, measured as
                # several extra lines and was reported as overflowing when it was not.
                label = s.label.replace("|", "\n") if s.kind == "class" else s.label
                usable = (r.w - 2 * TEXT_PAD - 6) if s.kind == "note" else text_area(r, s.kind)
                _, th = wrapped_size(label, usable, s.font_size, s.bold)
                avail = text_height_area(r, s.kind)
                if th > avail:
                    problems.append(
                        f"{k}: text needs {th:.0f}px of height but the shape offers "
                        f"{avail:.0f}px — it would overflow the border")

        keys = list(solid)
        for i, a in enumerate(keys):
            for b in keys[i + 1:]:
                if solid[a].rect.inflate(-margin).overlaps(solid[b].rect.inflate(-margin)):
                    problems.append(f"shapes overlap: {a} x {b}")

        for n, e in enumerate(self.edges):
            for a, b in zip(e.points, e.points[1:]):
                for k, s in solid.items():
                    if k in e.attached:
                        continue
                    if _seg_intersects_rect(a, b, s.rect.inflate(-margin)):
                        problems.append(
                            f"edge {n}{' (' + e.label + ')' if e.label else ''} "
                            f"crosses shape {k}")

        # B4: no two connectors may run along each other.
        #
        # Edges that start from the same point are exempt: an actor fanning out to many
        # use cases through one shared trunk is the standard comb idiom, and each branch
        # still leaves the trunk at its own y and lands on exactly one target, so it can
        # be traced. What this catches is unrelated connectors laid on top of one
        # another, where the shared stroke belongs to neither.
        def _same_origin(a: Edge, b: Edge) -> bool:
            return (abs(a.points[0][0] - b.points[0][0]) < 1e-6
                    and abs(a.points[0][1] - b.points[0][1]) < 1e-6)

        segs = [(n, e, a, b)
                for n, e in enumerate(self.edges)
                for a, b in zip(e.points, e.points[1:])]
        seen: set[tuple[int, int]] = set()
        for i, (ni, ei, a1, a2) in enumerate(segs):
            for nj, ej, b1, b2 in segs[i + 1:]:
                if ni == nj or (ni, nj) in seen or _same_origin(ei, ej):
                    continue
                if _collinear_run(a1, a2, b1, b2) > 8:
                    seen.add((ni, nj))
                    def name(k, e):
                        return f"{k}" + (f" ({e.label})" if e.label else "")
                    problems.append(
                        f"connectors {name(ni, ei)} and {name(nj, ej)} run along each "
                        "other — drawn as one line, neither can be traced")

        for n, e in enumerate(self.edges):
            if not e.label_rect:
                continue
            lr = e.label_rect.inflate(-1)
            for k, s in self.shapes.items():
                if s.decorative:
                    continue
                if lr.overlaps(s.rect):
                    problems.append(f"label '{e.label}' overlaps shape {k}")
            for m, other in enumerate(self.edges):
                if m == n:
                    continue
                for a, b in zip(other.points, other.points[1:]):
                    if _seg_intersects_rect(a, b, lr):
                        problems.append(
                            f"label '{e.label}' sits on edge {m}"
                            f"{' (' + other.label + ')' if other.label else ''}")
                if other.label_rect and lr.overlaps(other.label_rect):
                    problems.append(f"label '{e.label}' overlaps label '{other.label}'")
        return sorted(set(problems))

    # ── output ───────────────────────────────────────────────────────────────
    def save_png(self, out_dir: str) -> str:
        S = SCALE
        img = Image.new("RGB", (self.width * S, self.height * S), "white")
        dr = ImageDraw.Draw(img)

        def R(r: Rect):
            return [r.x * S, r.y * S, r.x2 * S, r.y2 * S]

        def draw_text(r: Rect, s: Shape, label: str | None = None):
            label = s.label if label is None else label
            usable = text_area(r, s.kind)
            lines = wrap(label, usable, s.font_size, s.bold)
            lh = line_height(s.font_size, s.bold)
            total = len(lines) * lh - LINE_GAP
            y = r.cy * S - total * S / 2
            big = font(s.font_size * S, s.bold)
            for line in lines:
                bb = dr.textbbox((0, 0), line, font=big)
                x = (r.x + TEXT_PAD) * S if s.align == "left" else r.cx * S - (bb[2] - bb[0]) / 2
                dr.text((x, y), line, fill="black", font=big)
                y += lh * S

        for s in self.shapes.values():
            r = s.rect
            if s.kind == "box":
                dr.rectangle(R(r), outline="black", width=1 * S)
            elif s.kind in ("initial", "final"):
                dr.ellipse(R(r), outline="black", width=1 * S,
                           fill="black" if s.kind == "initial" else None)
                if s.kind == "final":
                    inner = r.inflate(-6)
                    dr.ellipse(R(inner), fill="black")
                continue
            elif s.kind == "box_ext":
                # Dashed outline: an entity that lives in another domain and is shown
                # here only so the relationship pointing into this domain is complete.
                for a_, b_ in (((r.x, r.y), (r.x2, r.y)), ((r.x2, r.y), (r.x2, r.y2)),
                               ((r.x2, r.y2), (r.x, r.y2)), ((r.x, r.y2), (r.x, r.y))):
                    self._dash((dr), (a_[0] * S, a_[1] * S), (b_[0] * S, b_[1] * S),
                               on=9, off=6)
                draw_text(r, s)
                continue
            elif s.kind == "ellipse":
                dr.ellipse(R(r), outline="black", width=1 * S)
            elif s.kind == "package":
                tabw = min(r.w * 0.42, max(90, text_size(s.label, s.font_size, True)[0] + 24))
                dr.rectangle([r.x * S, (r.y + 22) * S, r.x2 * S, r.y2 * S],
                             outline="black", width=1 * S)
                dr.rectangle([r.x * S, r.y * S, (r.x + tabw) * S, (r.y + 22) * S],
                             outline="black", width=1 * S)
                dr.text(((r.x + 8) * S, (r.y + 3) * S), s.label, fill="black",
                        font=font(s.font_size * S, True))
                continue
            elif s.kind == "node3d":
                D = 16
                fx, fy = r.x, r.y + D
                fx2, fy2 = r.x2 - D, r.y2
                dr.rectangle([fx * S, fy * S, fx2 * S, fy2 * S], outline="black", width=1 * S)
                for poly in ([(fx, fy), (fx + D, r.y), (r.x2, r.y), (fx2, fy)],
                             [(fx2, fy), (r.x2, r.y), (r.x2, fy2 - D), (fx2, fy2)]):
                    pts = [(px * S, py * S) for px, py in poly]
                    dr.line(pts + [pts[0]], fill="black", width=1 * S)
                draw_text(Rect(fx, fy, fx2 - fx, fy2 - fy), s)
                continue
            elif s.kind == "class":
                name, attrs, ops = (s.label.split("|") + ["", ""])[:3]
                counts = [max(1, len(name.split("\n"))),
                          len(attrs.split("\n")) if attrs else 0,
                          len(ops.split("\n")) if ops else 0]
                unit = r.h / max(1, sum(counts))
                dr.rectangle(R(r), outline="black", width=1 * S)
                y0 = r.y
                for idx, (block, cnt) in enumerate(zip((name, attrs, ops), counts)):
                    if cnt == 0:
                        continue
                    bh = unit * cnt
                    if idx > 0:
                        dr.line([r.x * S, y0 * S, r.x2 * S, y0 * S],
                                fill="black", width=1 * S)
                    sub = Shape("box", Rect(r.x, y0, r.w, bh), block, s.font_size,
                                bold=(idx == 0), align=("center" if idx == 0 else "left"))
                    draw_text(sub.rect, sub)
                    y0 += bh
                continue
            elif s.kind == "note":
                fold = 12
                dr.polygon([(r.x * S, r.y * S), ((r.x2 - fold) * S, r.y * S),
                            (r.x2 * S, (r.y + fold) * S), (r.x2 * S, r.y2 * S),
                            (r.x * S, r.y2 * S)], outline="black")
                dr.line([((r.x2 - fold) * S, r.y * S), ((r.x2 - fold) * S, (r.y + fold) * S),
                         (r.x2 * S, (r.y + fold) * S)], fill="black", width=1 * S)
                big = font(s.font_size * S)
                lh = line_height(s.font_size)
                y = (r.y + TEXT_PAD) * S
                for line in wrap(s.label, r.w - 2 * TEXT_PAD - 6, s.font_size):
                    dr.text(((r.x + TEXT_PAD) * S, y), line, fill="black", font=big)
                    y += lh * S
                continue
            elif s.kind == "actor":
                cx = r.cx * S
                hd = 13 * S
                dr.ellipse([cx - hd, r.y * S, cx + hd, r.y * S + 2 * hd],
                           outline="black", width=1 * S)
                body_top = r.y * S + 2 * hd
                dr.line([cx, body_top, cx, body_top + 22 * S], fill="black", width=1 * S)
                dr.line([cx - 17 * S, body_top + 8 * S, cx + 17 * S, body_top + 8 * S],
                        fill="black", width=1 * S)
                dr.line([cx, body_top + 22 * S, cx - 15 * S, body_top + 40 * S],
                        fill="black", width=1 * S)
                dr.line([cx, body_top + 22 * S, cx + 15 * S, body_top + 40 * S],
                        fill="black", width=1 * S)
                f = font(s.font_size * S)
                bb = dr.textbbox((0, 0), s.label, font=f)
                dr.text((cx - (bb[2] - bb[0]) / 2, (r.y + 72) * S), s.label,
                        fill="black", font=f)
                continue
            elif s.kind in ("title", "legend", "label"):
                if s.kind == "legend":
                    dr.rectangle(R(r), outline="black", width=1 * S)
                draw_text(r, s)
                continue
            draw_text(r, s)

        for e in self.edges:
            pts = [(x * S, y * S) for x, y in e.points]
            if e.style == "dashed":
                for a, b in zip(pts, pts[1:]):
                    self._dash(dr, a, b)
            else:
                dr.line(pts, fill="black", width=1 * S)
            self._arrow(dr, pts[1], pts[0], e.start_arrow, S)
            self._arrow(dr, pts[-2], pts[-1], e.end_arrow, S)
            if e.label_rect:
                lr = e.label_rect
                dr.rectangle(R(lr), fill="white")
                f = font(12 * S)
                y = (lr.y + 4) * S
                for line in e.label.split("\n"):
                    bb = dr.textbbox((0, 0), line, font=f)
                    dr.text((lr.cx * S - (bb[2] - bb[0]) / 2, y), line,
                            fill="black", font=f)
                    y += (text_size(line, 12)[1] + 6) * S

        os.makedirs(out_dir, exist_ok=True)
        path = os.path.join(out_dir, f"{self.name}.png")
        img.save(path, optimize=True)
        return path

    @staticmethod
    def _dash(dr, a, b, on=10, off=7):
        (x1, y1), (x2, y2) = a, b
        length = abs(x2 - x1) + abs(y2 - y1)
        if length == 0:
            return
        dx, dy = (x2 - x1) / length, (y2 - y1) / length
        pos = 0.0
        while pos < length:
            end = min(pos + on, length)
            dr.line([x1 + dx * pos, y1 + dy * pos, x1 + dx * end, y1 + dy * end],
                    fill="black", width=SCALE)
            pos = end + off

    @staticmethod
    def _arrow(dr, frm, to, kind, S):
        if kind == "none":
            return
        import math
        ang = math.atan2(to[1] - frm[1], to[0] - frm[0])
        L = 11 * S
        if kind in ("open", "filled"):
            spread = 0.42
            p1 = (to[0] - L * math.cos(ang - spread), to[1] - L * math.sin(ang - spread))
            p2 = (to[0] - L * math.cos(ang + spread), to[1] - L * math.sin(ang + spread))
            if kind == "filled":
                dr.polygon([to, p1, p2], fill="black")
            else:
                dr.line([p1, to], fill="black", width=S)
                dr.line([p2, to], fill="black", width=S)
            return

        perp0 = ang + math.pi / 2
        if kind == "triangle":
            # UML generalisation and realisation: a CLOSED, HOLLOW triangle.
            # White-filled so the connector does not show through it.
            T, HW = 17 * S, 10 * S
            base = (to[0] - T * math.cos(ang), to[1] - T * math.sin(ang))
            c1 = (base[0] - HW * math.cos(perp0), base[1] - HW * math.sin(perp0))
            c2 = (base[0] + HW * math.cos(perp0), base[1] + HW * math.sin(perp0))
            dr.polygon([to, c1, c2], fill="white", outline="black")
            for a_, b_ in ((to, c1), (to, c2), (c1, c2)):
                dr.line([a_, b_], fill="black", width=S)
            return
        if kind in ("diamond", "diamond_filled"):
            # Aggregation (hollow) and composition (filled).
            Ln, HW = 20 * S, 8 * S
            mid_ = (to[0] - Ln / 2 * math.cos(ang), to[1] - Ln / 2 * math.sin(ang))
            far = (to[0] - Ln * math.cos(ang), to[1] - Ln * math.sin(ang))
            c1 = (mid_[0] - HW * math.cos(perp0), mid_[1] - HW * math.sin(perp0))
            c2 = (mid_[0] + HW * math.cos(perp0), mid_[1] + HW * math.sin(perp0))
            poly = [to, c1, far, c2]
            dr.polygon(poly, fill=("black" if kind == "diamond_filled" else "white"),
                       outline="black")
            for a_, b_ in zip(poly, poly[1:] + poly[:1]):
                dr.line([a_, b_], fill="black", width=S)
            return
        # Crow's-foot family: marks sit just before the endpoint, perpendicular.
        perp = ang + math.pi / 2
        def at(d):
            return (to[0] - d * math.cos(ang), to[1] - d * math.sin(ang))
        def bar(d, half=8 * S):
            c = at(d)
            dr.line([(c[0] - half * math.cos(perp), c[1] - half * math.sin(perp)),
                     (c[0] + half * math.cos(perp), c[1] + half * math.sin(perp))],
                    fill="black", width=S)
        if kind in ("crow", "zero_many", "one_many"):
            root = at(13 * S)
            for s_ in (-8 * S, 0, 8 * S):
                tip = (to[0] + s_ * math.cos(perp), to[1] + s_ * math.sin(perp))
                dr.line([root, tip], fill="black", width=S)
            if kind == "zero_many":
                c = at(20 * S)
                rr = 5 * S
                dr.ellipse([c[0] - rr, c[1] - rr, c[0] + rr, c[1] + rr],
                           outline="black", width=S, fill="white")
            elif kind == "one_many":
                bar(20 * S)
        elif kind == "one":
            bar(6 * S)
        elif kind == "one_one":
            bar(6 * S); bar(13 * S)
        elif kind == "zero_one":
            bar(6 * S)
            c = at(15 * S)
            rr = 5 * S
            dr.ellipse([c[0] - rr, c[1] - rr, c[0] + rr, c[1] + rr],
                       outline="black", width=S, fill="white")

    def save_drawio(self, out_dir: str) -> str:
        BOX = ("rounded=0;whiteSpace=wrap;html=1;fillColor=none;strokeColor=#000000;"
               "fontColor=#000000;fontSize={fs};{extra}")
        cells = []
        n = 0
        for key, s in self.shapes.items():
            n += 1
            r = s.rect
            if s.kind == "ellipse":
                style = BOX.format(fs=s.font_size, extra="ellipse;")
            elif s.kind == "node3d":
                style = BOX.format(fs=s.font_size, extra="shape=cube;size=16;darkOpacity=0;")
            elif s.kind == "class":
                style = BOX.format(fs=s.font_size, extra="verticalAlign=top;align=center;")
            elif s.kind == "actor":
                style = ("shape=umlActor;verticalLabelPosition=bottom;html=1;"
                         "verticalAlign=top;outlineConnect=0;fillColor=none;"
                         f"strokeColor=#000000;fontColor=#000000;fontSize={s.font_size};")
            elif s.kind == "note":
                style = BOX.format(fs=s.font_size, extra="shape=note;size=14;align=left;")
            elif s.kind == "package":
                style = ("shape=folder;tabWidth=170;tabHeight=24;tabPosition=left;rounded=0;"
                         "html=1;fillColor=none;strokeColor=#000000;fontColor=#000000;"
                         f"fontSize={s.font_size};fontStyle=1;verticalAlign=top;align=left;"
                         "spacingLeft=8;")
            elif s.kind in ("title", "legend"):
                style = (f"text;html=1;align={'center' if s.kind == 'title' else 'left'};"
                         f"verticalAlign=middle;fontSize={s.font_size};"
                         f"fontStyle={'1' if s.bold else '0'};fontColor=#000000;")
            else:
                style = BOX.format(fs=s.font_size, extra="")
            cells.append(
                f'<mxCell id="{key}" value="{html.escape(s.label)}" style="{style}" '
                f'vertex="1" parent="1"><mxGeometry x="{r.x}" y="{r.y}" '
                f'width="{r.w}" height="{r.h}" as="geometry"/></mxCell>')
        for i, e in enumerate(self.edges):
            dash = "dashed=1;" if e.style == "dashed" else ""
            end = {"open": "endArrow=open;endFill=0;",
                   "filled": "endArrow=block;endFill=1;",
                   "triangle": "endArrow=block;endFill=0;",
                   "diamond": "endArrow=diamond;endFill=0;",
                   "diamond_filled": "endArrow=diamond;endFill=1;",
                   "none": "endArrow=none;"}.get(e.end_arrow, "endArrow=open;endFill=0;")
            style = (f"edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;{dash}{end}"
                     "strokeColor=#000000;fontColor=#000000;fontSize=12;"
                     "labelBackgroundColor=#FFFFFF;jumpStyle=none;")
            mid = e.points[1:-1]
            arr = ("<Array as=\"points\">"
                   + "".join(f'<mxPoint x="{x}" y="{y}"/>' for x, y in mid)
                   + "</Array>") if mid else ""
            cells.append(
                f'<mxCell id="e{i}" value="{html.escape(e.label)}" style="{style}" '
                f'edge="1" parent="1"><mxGeometry relative="1" as="geometry">'
                f'<mxPoint x="{e.points[0][0]}" y="{e.points[0][1]}" as="sourcePoint"/>'
                f'<mxPoint x="{e.points[-1][0]}" y="{e.points[-1][1]}" as="targetPoint"/>'
                f'{arr}</mxGeometry></mxCell>')
        xml = ('<mxfile host="app.diagrams.net">'
               f'<diagram name="{html.escape(self.name)}">'
               f'<mxGraphModel dx="1000" dy="800" grid="0" gridSize="10" guides="1" '
               f'tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" '
               f'pageWidth="{self.width}" pageHeight="{self.height}" math="0" shadow="0">'
               f'<root><mxCell id="0"/><mxCell id="1" parent="0"/>{"".join(cells)}</root>'
               '</mxGraphModel></diagram></mxfile>')
        os.makedirs(out_dir, exist_ok=True)
        path = os.path.join(out_dir, f"{self.name}.drawio")
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(xml)
        return path
