"""Generate draw.io diagrams with explicit coordinates.

Graphviz/PlantUML choose their own layout, which is why connectors ended up crossing
components. Here every box is placed on a fixed grid and every connector is routed on
explicit waypoints, so nothing can overlap. Output is .drawio XML, which draw.io then
exports to PNG — and which stays editable by hand afterwards.

House style: black outlines on white, no fill colour, no shadow, orthogonal connectors.
"""
import html
import os

OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "drawio")

BOX = "rounded=0;whiteSpace=wrap;html=1;fillColor=none;strokeColor=#000000;fontColor=#000000;fontSize=13;"
PKG = ("shape=folder;tabWidth=170;tabHeight=26;tabPosition=left;rounded=0;html=1;"
       "fillColor=none;strokeColor=#000000;fontColor=#000000;fontSize=14;fontStyle=1;"
       "verticalAlign=top;align=left;spacingLeft=8;spacingTop=2;")
NOTE = ("shape=note;whiteSpace=wrap;html=1;size=14;fillColor=none;strokeColor=#000000;"
        "fontColor=#000000;fontSize=12;align=left;spacingLeft=6;")
# UML dependency: dashed line, open arrowhead.
DEP = ("edgeStyle=orthogonalEdgeStyle;rounded=0;html=1;dashed=1;endArrow=open;endFill=0;"
       "strokeColor=#000000;fontColor=#000000;fontSize=12;labelBackgroundColor=#FFFFFF;"
       "jumpStyle=none;exitPerimeter=0;entryPerimeter=0;")


class Drawing:
    def __init__(self, name, width, height):
        self.name = name
        self.width = width
        self.height = height
        self.cells = []
        self._n = 0

    def _id(self, prefix):
        self._n += 1
        return f"{prefix}{self._n}"

    def box(self, x, y, w, h, label, style=BOX, parent="1"):
        cid = self._id("n")
        self.cells.append(
            f'<mxCell id="{cid}" value="{html.escape(label)}" style="{style}" vertex="1" parent="{parent}">'
            f'<mxGeometry x="{x}" y="{y}" width="{w}" height="{h}" as="geometry"/></mxCell>')
        return cid

    def edge(self, x1, y1, x2, y2, label="", waypoints=(), style=DEP):
        cid = self._id("e")
        pts = "".join(f'<mxPoint x="{px}" y="{py}"/>' for px, py in waypoints)
        arr = f'<Array as="points">{pts}</Array>' if pts else ""
        self.cells.append(
            f'<mxCell id="{cid}" value="{html.escape(label)}" style="{style}" edge="1" parent="1">'
            f'<mxGeometry relative="1" as="geometry">'
            f'<mxPoint x="{x1}" y="{y1}" as="sourcePoint"/>'
            f'<mxPoint x="{x2}" y="{y2}" as="targetPoint"/>{arr}</mxGeometry></mxCell>')
        return cid

    def text(self, x, y, w, h, label, size=16, bold=True):
        style = (f"text;html=1;align=center;verticalAlign=middle;fontSize={size};"
                 f"fontStyle={'1' if bold else '0'};fontColor=#000000;")
        return self.box(x, y, w, h, label, style=style)

    def xml(self):
        body = "".join(self.cells)
        return (
            '<mxfile host="app.diagrams.net">'
            f'<diagram name="{html.escape(self.name)}">'
            f'<mxGraphModel dx="1000" dy="800" grid="0" gridSize="10" guides="1" tooltips="1" '
            f'connect="1" arrows="1" fold="1" page="1" pageScale="1" '
            f'pageWidth="{self.width}" pageHeight="{self.height}" math="0" shadow="0">'
            f'<root><mxCell id="0"/><mxCell id="1" parent="0"/>{body}</root>'
            '</mxGraphModel></diagram></mxfile>')

    def save(self):
        os.makedirs(OUT_DIR, exist_ok=True)
        path = os.path.abspath(os.path.join(OUT_DIR, f"{self.name}.drawio"))
        with open(path, "w", encoding="utf-8") as fh:
            fh.write(self.xml())
        print(f"  wrote {path}")
        return path


def layered_architecture():
    """Five layers as stacked bands; dependencies as vertical arrows down the centre,
    with the one layer-skipping dependency routed around the right-hand margin."""
    M = 50                      # left margin
    BAND_W = 1060
    CANVAS_W = BAND_W + M + 190  # extra right margin for the routed edge + note
    TITLE_H = 34
    COMP_H = 62
    PAD = 16
    BAND_H = TITLE_H + COMP_H + PAD * 2
    GAP = 78                    # vertical space between bands, where arrows live

    bands = [
        ("Client Surfaces", ["Audience Website", "Owner Dashboard", "Admin Console",
                             "Staff Mobile", "Audience F&B Mobile"]),
        ("MusicLounge.Api", ["Controllers (25)", "SignalR Hubs",
                             "Authorization Policies", "Middleware"]),
        ("MusicLounge.Infrastructure", ["EF Core DbContext + Repositories",
                                        "External Service Adapters", "Hangfire Jobs"]),
        ("MusicLounge.Application", ["Commands / Queries + Handlers", "Validators",
                                     "Pipeline Behaviors", "Abstractions (interfaces)"]),
        ("MusicLounge.Domain", ["Entities (68)", "Enums · Value Objects",
                                "Domain Events · Exceptions"]),
    ]

    top = 78
    total_h = top + len(bands) * BAND_H + (len(bands) - 1) * GAP + 60
    d = Drawing("architecture-layers", CANVAS_W, total_h)
    d.text(M, 24, BAND_W, 30,
           "Layered Architecture — an outer layer may depend on an inner one, never the reverse")

    ys = []
    for i, (title, comps) in enumerate(bands):
        y = top + i * (BAND_H + GAP)
        ys.append(y)
        d.box(M, y, BAND_W, BAND_H, f"«layer»  {title}", style=PKG)
        n = len(comps)
        cw = (BAND_W - PAD * (n + 1)) // n
        for j, c in enumerate(comps):
            d.box(M + PAD + j * (cw + PAD), y + TITLE_H + PAD, cw, COMP_H, c)

    cx = M + BAND_W // 2

    def spine(i, j, label):
        d.edge(cx, ys[i] + BAND_H, cx, ys[j], label)

    spine(0, 1, "«use»  REST + WebSocket")
    spine(1, 2, "«use»")
    spine(2, 3, "«use»  implements abstractions")
    spine(3, 4, "«use»")

    # Api depends on Application directly too — routed around the right margin so it
    # crosses nothing.
    rx = M + BAND_W + 60
    y_api = ys[1] + BAND_H // 2
    y_app = ys[3] + BAND_H // 2
    d.edge(M + BAND_W, y_api, M + BAND_W, y_app, "«use»",
           waypoints=[(rx, y_api), (rx, y_app)])

    d.box(M + BAND_W - 340, ys[4] + BAND_H + 18, 340, 56,
          "Domain references no other project and no external framework, "
          "so every business rule in it is unit-testable without a database or a host.",
          style=NOTE)
    return d.save()


def frontend_architecture():
    """The internal structure every client application shares, as stacked bands."""
    M = 50
    BAND_W = 1000
    CANVAS_W = BAND_W + M * 2
    TITLE_H = 34
    COMP_H = 62
    PAD = 16
    BAND_H = TITLE_H + COMP_H + PAD * 2
    GAP = 70

    bands = [
        ("Screens / Routes", ["One route per screen (lazy-loaded)",
                              "Route guards (role resolved from JWT)"]),
        ("Feature Modules", ["auth · venues · shows", "tickets · livestream · donations",
                             "f&b · subscription · admin"]),
        ("Shared Design System", ["Warm Luxury Lounge tokens",
                                  "Reusable components (forms, tables, modals, states)"]),
        ("State & Data Access", ["TanStack Query (server state)", "Zustand (client state)",
                                 "Typed API client (OpenAPI)", "SignalR client"]),
        ("Cross-Cutting", ["JWT storage & refresh", "Error to message mapping",
                           "Form validation", "Accessibility primitives"]),
    ]

    top = 78
    total_h = top + len(bands) * BAND_H + len(bands) * GAP + 110
    d = Drawing("frontend-architecture", CANVAS_W, total_h)
    d.text(M, 24, BAND_W, 30,
           "Frontend Architecture — structure shared by all five client surfaces")

    ys = []
    for i, (title, comps) in enumerate(bands):
        y = top + i * (BAND_H + GAP)
        ys.append(y)
        d.box(M, y, BAND_W, BAND_H, f"«layer»  {title}", style=PKG)
        n = len(comps)
        cw = (BAND_W - PAD * (n + 1)) // n
        for j, c in enumerate(comps):
            d.box(M + PAD + j * (cw + PAD), y + TITLE_H + PAD, cw, COMP_H, c)

    cx = M + BAND_W // 2
    for i in range(len(bands) - 1):
        d.edge(cx, ys[i] + BAND_H, cx, ys[i + 1], "«use»")

    api_y = ys[-1] + BAND_H + GAP
    d.box(M + BAND_W // 2 - 250, api_y, 500, 56,
          "MusicLounge REST + SignalR API  (over HTTPS)")
    d.edge(cx, ys[-1] + BAND_H, cx, api_y, "«use»")
    return d.save()


if __name__ == "__main__":
    print("Generating draw.io sources:")
    layered_architecture()
    frontend_architecture()
