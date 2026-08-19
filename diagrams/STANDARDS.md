# Diagram standards — MusicLounge

## 0. Acceptance criteria — agree these before drawing anything

A diagram is only finished when **all three groups** pass. "Not to standard" and "not
readable" are both defects; passing group B alone is not a pass.

### A — Notation correctness (checked by eye against this document)
- A1 The notation family is named on the diagram and followed throughout.
- A2 Every symbol is the standard symbol for its meaning: a Node is a 3D cube, a DFD
  process is a circle, generalisation and realisation take a closed hollow triangle,
  a deployment communication path is a plain line with no arrowhead.
- A3 Relationship direction is correct (`«include»` base→included, `«extend»`
  extension→base, dependency client→supplier).
- A4 Stereotypes are ones the standard actually defines.
- A5 Content matches the code: names, cardinality, enum states, provider names.

### B — Geometry (proved mechanically by `dsl.validate()`)
- B1 No two shapes overlap.
- B2 No connector crosses a shape it is not attached to.
- B3 No label touches a shape, another connector, or another label.
- B4 **No two connectors run along each other.** Collinear overlapping segments render
  as one line, so neither can be traced — this is what made the first deployment
  diagram unreadable even though every other check passed. Connectors leaving the
  *same* point are exempt: one actor fanning out through a shared trunk is the
  standard comb idiom and each branch still ends on exactly one target.
- B5 Wrapped text fits inside its shape; nothing is clipped.
- B6 Everything is inside the canvas.

### C — Readability (checked by eye on the full-size image, never a crop)
- C1 **Every connector can be traced end to end**: you can see which two elements it
  joins without following it through a bundle.
- C2 Connectors take as few bends as possible; prefer a single straight segment.
- C3 Elements that are connected sit near each other; no long detour routes.
- C4 Spacing and alignment are consistent.
- C5 Whitespace is balanced — no large dead regions, no cramped clusters.
- C6 The aspect ratio suits the medium it will be placed in.

---


Every diagram in `diagrams/src/` follows a named, published notation. This file records
which standard applies to which diagram and the specific rules that were easy to get
wrong, so a reviewer can check the drawing against its own rulebook rather than taste.

Last audited: 2026-08-19.

---

## 1. Which standard governs which diagram

| Diagram | Notation | Authority |
|---|---|---|
| `uc-*.puml` | UML 2.5.1 Use Case Diagram | OMG UML 2.5.1, formal/2017-12-05 |
| `seq-*.puml` | UML 2.5.1 Sequence Diagram | same |
| `state-*.puml` | UML 2.5.1 State Machine Diagram | same |
| `activity-*.puml` | UML 2.5.1 Activity Diagram | same |
| `deployment.puml` | UML 2.5.1 Deployment Diagram | same |
| `package-application.puml` | UML 2.5.1 Package Diagram | same |
| `class-cqrs.puml` | UML 2.5.1 Class Diagram | same |
| `context.puml` | Data Flow Diagram, Yourdon/DeMarco notation | Structured Analysis (DeMarco 1979; Yourdon 1989) |
| `erd-*.puml` | Entity Relationship Diagram, Crow's Foot / Information Engineering notation | IE notation (Martin) |
| `flow-*.puml` | UML 2.5.1 Activity Diagram used as screen-flow | OMG UML 2.5.1 |

UML 2.5.1 is the current formal release: <https://www.omg.org/spec/UML/2.5.1/>. There is
no newer version — 2.5.1 has sat at the top of OMG's formal list since December 2017.

**Note on the context diagram.** UML does not define a "context diagram". Rather than
draw something UML-shaped and mislabel it, `context.puml` deliberately uses the DFD
convention from Structured Analysis, which is what a context diagram actually is.

**Note on ERD.** There is no ISO/OMG standard for Crow's Foot; it is Information
Engineering notation. The alternative with a formal standard behind it is IDEF1X
(FIPS PUB 184). Crow's Foot was chosen because it is what the rest of the SEP490
document set uses and what the reviewers will expect. The choice is stated on the
diagram itself so it is not ambiguous.

---

## 2. Rules that are easy to get wrong

### 2.1 Use case — `«include»` vs `«extend»` direction

This is the single most commonly inverted relationship in student UML, and it was
inverted in six places in the previous revision of these diagrams.

```
«include»   base  ──────▷  included      arrow points FROM the base TO the included
«extend»    extension ───▷  base         arrow points FROM the extension TO the base
```

- `«include»` means the included behaviour **always** runs as part of the base. If it
  can be skipped, it is not an include.
- `«extend»` means the extending behaviour runs **conditionally**, at an extension
  point in the base. The base is complete without it.
- Neither relationship may cross an actor boundary to pull in a use case another actor
  independently initiates. "Submit show for review" does not `«include»` the Admin's
  "Review a show" — the Admin starts that separately.

### 2.2 Sequence — combined fragment guards

PlantUML **adds the square brackets itself**. Writing them by hand corrupts the label.

```
WRONG   alt [payment completed within 15 minutes]   → renders "completed within 15 minutes"
                                                       (the word "payment" is silently eaten
                                                        and the text renders underlined)
RIGHT   alt payment completed within 15 minutes     → renders "[payment completed within 15 minutes]"
```

### 2.3 Deployment — communication paths carry no arrowhead

A communication path between two nodes is notated as an **association**: a plain solid
line. Directed arrows are dependencies and mean something else. Protocol goes in a
stereotype on the line.

Standard node stereotypes are `«device»` (hardware) and `«executionEnvironment»`
(a software runtime hosting artifacts). `«cloud»` is not standard UML; where a cloud
boundary is needed it is drawn as a plain grouping node with no invented stereotype.
A third-party SaaS is not a `«device»` — it is an external `«executionEnvironment»`
or, better, kept outside the deployment model entirely.

Artifacts are shown either nested inside their deployment target or attached with a
`«deploy»` dependency.

### 2.4 Package — dependency keywords

UML defines `«import»`, `«access»` and `«merge»` for package relationships, and `«use»`
for a generic usage dependency. A bare "depends on" text label is not notation. A
package diagram shows **packages**; component boxes belong in a component diagram.

### 2.5 ERD — cardinality must match the schema, not the intent

Crow's Foot end markers encode two things at once:

```
inner mark = minimum (optionality)      outer mark = maximum (cardinality)
||  exactly one          |o  zero or one
}|  one or many          }o  zero or many
```

A nullable foreign key in C# (`int?`) means the minimum is **zero**, so the marker must
be `o`, not `|`. Two relationships in the previous revision claimed `exactly one` against
a nullable FK (`Ticket.PaymentId`, `LedgerEntry.PaymentId`) and were wrong.

---

## 3. Rendering rules

### 3.1 Size limit — PlantUML clips silently

PlantUML's default maximum render size is 4096 px per side. Past that it **crops the
image and reports no error**. A diagram can lose entire packages off the right edge and
still "build successfully". `build.ps1` therefore passes:

```
java -DPLANTUML_LIMIT_SIZE=8192 -jar plantuml.jar ...
```

`validate.py` additionally fails the build if any rendered PNG lands within 2 % of the
limit, since that is the signature of a diagram that is about to start clipping.

### 3.2 Creole markup can corrupt labels

PlantUML applies lightweight markup inside labels. These sequences change the text:

| Sequence | Effect |
|---|---|
| `//text//` | italic — breaks any URL containing `//` |
| `**text**` | bold |
| `__text__` | underline |
| `~~text~~` | strikethrough |
| `[[target]]` | hyperlink |

Keep URLs out of `.puml` labels. `validate.py` scans for these sequences.

### 3.3 Fonts

`_style.puml` sets `defaultFontName "Segoe UI"`, which exists on Windows. Rendering on
Linux/CI without that font falls back silently and shifts text metrics, which can turn a
diagram that just fitted into one that clips. Render on Windows, or install the font,
or change the setting deliberately — do not let it fall back unnoticed.

### 3.4 House style

Strict black and white: no fills, no shadows, no colour, orthogonal connectors. The
SEP490 documents are graded on paper and must photocopy identically. See `_style.puml`.

---

## 4. Content accuracy rules

Diagrams state facts about the system, so the facts are checked mechanically rather
than trusted. `validate.py` enforces:

1. Every use case name in `uc-*.puml` exists **verbatim** in `spec/build/usecases.py`.
2. The union of all `uc-*.puml` covers **all 109** use cases — none dropped.
3. Every state in `state-*.puml` exists in the corresponding `Domain/Enums/*.cs`.
4. Every entity in `erd-full.puml` exists in `Domain/Entities/*.cs`, and all **68** appear.
5. No hand-written guard brackets, no Creole-breaking sequences, no near-limit renders.

This mirrors `spec/build/facts.py`, which already self-checks the report figures the
same way and for the same reason: the numbers drifted apart before it existed.

---

## 5. Known modelling notes

- **`TicketStatus.Refunded` is unreachable.** The enum declares it, but no code in
  `src/` or `tests/` ever assigns it; an approved refund leaves the ticket `Cancelled`.
  `state-ticket.puml` shows it as an unreachable state rather than inventing a
  transition into it. This is a finding about the code, not a drawing choice.
- **The show status enum is `LoungeShowStatus`**, not `ShowStatus`; `Published` is only
  ever set through the Admin review path in `ReviewShowCommandHandler`.
