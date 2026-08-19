# Stitch Brief v2 — Create Show wizard (3 views), free-form luxury direction

Rewritten 2026-08-17 after the v1 designs were judged too constrained/boxy and the 3 steps felt
disconnected. This brief replaces the v1 Create-Show prompts.

## Verified Stitch capability map (empirical, not from blog paraphrase)

Checked by reading Stitch's own generated output in the canonical project
(`projects/14642646959899472200`), which is ground truth over any documentation summary.

| Capability | Verdict | Evidence |
|---|---|---|
| Animated WebGL shader background | **YES — native feature** | The project's "Shader" screen contains a `<!-- STITCH_SHADER_START:ANIMATION_62 -->` block: raw WebGL, `u_time` / `u_resolution` / `u_mouse` uniforms (ShaderToy convention), `requestAnimationFrame` loop, `ResizeObserver` sizing — and the GLSL already quotes this project's palette (`#fff9ed`, `#dfd9cf`, `#c97b4a`) with the comment "Warm Luxury Palette colors from DS_1". |
| Tailwind state-transition micro-interactions | **YES** | Richest screen emits `transition-all`, `transition-colors`, `transition-transform`, `transition-opacity`, `duration-200/300/500/700/1000`, `ease-in-out`, `group-hover:scale-*`, `group-hover:rotate-*`, `group-hover:w-full`, `active:scale-95`, `backdrop-blur-md`, `animate-pulse`, `sticky`. |
| **Three.js** | **NO** | Stitch loads exactly one external script — `cdn.tailwindcss.com`. No 3D library is ever imported. Asking for "Three.js" by name gets ignored or downgraded to a raw shader. Real 3D (geometry/camera/lights/GLTF) must be built React-side with `@react-three/fiber`. |
| Custom `@keyframes` / scroll-driven animation | **NO** | Zero `@keyframes` or custom `animation:` declarations across all inspected screens. Motion is hover/state transitions + the shader loop only. |
| Cross-screen / view transitions | **NO — structural** | Stitch generates one standalone screen per call and has no concept of routing between screens. The "smooth transition between views" requirement is a React-side job (this codebase already depends on `animejs`). |

### Prompt-length ceiling

The official Stitch Prompt Guide thread reports that prompts past **~5,000 characters** cause Stitch
to "consistently omit some components." Each prompt below is deliberately kept in the
2,500–4,000 char band: rich enough for a strong one-shot, short enough to not drop sections.

### Process rule (unchanged, mandatory)

One complete prompt per screen via `generate_screen_from_text`. **Never** `edit_screens` to patch a
result — regenerate fresh with a better prompt instead. See `feedback_stitch_one_shot_prompting`.
Use `modelId: GEMINI_3_1_PRO` for these (highest quality tier available).

### Design-system handling — the deliberate call

These prompts **keep passing the project's `designSystem` asset** (colour + type tokens, which are
already mirrored in `tailwind.config.ts`) while explicitly telling Stitch to treat it as *a palette
to compose freely with, not a layout template*. Rationale: the "gò bó" complaint is about rigid boxy
composition, not about the warm palette — and the palette is what keeps all 3 steps looking like one
product and keeps the port to React token-exact. If a fully unconstrained look is wanted instead,
omit `designSystem` entirely and accept that new colours will need adding to `tailwind.config.ts`.

### Grounding for the visual direction

Not self-invented (per `feedback_ui_use_existing_templates_not_self_design`). Current luxury-web
consensus used here: "quiet luxury" warm/earthy muted palettes, serif display headlines, asymmetric
editorial/magazine composition over uniform card grids, grain texture, and depth via WebGL rather
than flat imagery. Anti-AI-slop guards from `feedback_deai_slop_checklist` are written into each
prompt as explicit negative constraints (no uniform radii, no repeated equal-size card grid, no
icon-in-a-circle rows).

### Cross-view continuity strategy

All 3 prompts share three anchors so the steps read as one continuous space (and so the React
transition I add later has something to animate *between* rather than a hard cut):

1. The **same** full-bleed animated shader background, described identically in all 3.
2. A **left-edge vertical progress rail** instead of a horizontal stepper — it stays put across
   steps, so only the content column changes between views.
3. Same grain overlay, same Playfair/Inter pairing, same nav.

Composition then differs per step so they're a progression, not three copies of one card.

---

## PROMPT 1 of 3 — Step 1 "Thông tin cơ bản"

```
Design a desktop web screen for MusicLounge, a premium live-music lounge platform — the first step of a 3-step "create a show" flow, for venue owners. Aim for the feel of a high-end architecture or fashion publication, not a SaaS dashboard: editorial, asymmetric, quiet-luxury, meticulous in small detail. Use the project's design system as a colour and type palette to compose freely with — NOT as a layout template.

BACKGROUND: a full-bleed animated WebGL shader canvas fixed behind all content, driven by u_time and subtly reactive to u_mouse. Very slow, liquid, organic movement — warm cream (#fff9ed) drifting into soft sand (#dfd9cf), with a faint amber (#c97b4a) bloom at roughly 5% intensity that follows the cursor at a lag. It must read as ambient candlelight in a dark lounge, never as a busy gradient. Over it, a subtle film-grain / noise texture overlay at very low opacity.

LAYOUT — deliberately asymmetric, NOT a centered card:
- Slim top navigation bar, transparent with backdrop-blur-md, sticky: wordmark left in Playfair Display, sparse nav links, owner avatar right.
- A fixed VERTICAL progress rail pinned to the left edge, vertically centered: three small numbered markers stacked with a thin connecting line running through them, labelled "Thông tin cơ bản" / "Lịch trình & Sức chứa" / "Line-up nghệ sĩ". Marker 1 is active: filled amber with a soft outer glow ring. Markers 2 and 3 are hairline outlines, dimmed. Labels are tiny uppercase letter-spaced Inter, set vertically alongside the rail.
- The content sits in an off-centre column, wider on the right, generous negative space on the left. Do not wrap everything in one bordered box.

CONTENT, top to bottom in the content column:
1. A very large Playfair Display headline "Đêm diễn của bạn bắt đầu từ đây", with a small uppercase eyebrow above it reading "BƯỚC 01 — THÔNG TIN CƠ BẢN", and one line of muted Inter beneath: "Mỗi chi tiết nhỏ đều góp phần tạo nên một đêm nhạc đáng nhớ."
2. A venue selector — styled as a wide understated line-input with only a bottom hairline border (no full box), label "PHÒNG TRÀ" as a tiny uppercase floating label above, current value "The Velvet Room — Hoàn Kiếm, Hà Nội" in larger Playfair, and a small caret at the far right.
3. A show-name field in the same bottom-hairline-only style, label "TÊN SHOW", with the example value "Jazz Dưới Ánh Nến" typed in.
4. A description textarea, same hairline treatment, label "MÔ TẢ", 4 rows, with real placeholder text: "Giới thiệu về đêm diễn, không gian, cảm hứng...".
5. Format choice as TWO LARGE UNEQUAL editorial plates side by side, not equal cards: the left/selected one noticeably wider and taller, containing a soft-focus warm photograph of an intimate candlelit lounge interior as its background with a dark warm scrim over it, the title "Trực tiếp" in Playfair reversed out in cream, a one-line caption "Tổ chức tại phòng trà", and a small amber check badge in its corner. The right/unselected one is narrower, no photo, just a cream surface with a hairline border, title "Trực tuyến" and caption "Livestream độc quyền" in muted tone. Give the two plates DIFFERENT corner radii — the selected one softer, the other nearly square.
6. Genre selection as a loose, irregular scatter of small pill tags (not a neat grid, varying widths): "Jazz", "Acoustic", "Bolero", "Indie", "Soul", "Piano Lounge" — with "Jazz" and "Acoustic" active in filled amber, the rest hairline outline.
7. Action row: a quiet text-only "Hủy" on the left, and on the right a primary "Tiếp tục" button in deep amber with a right-arrow that shifts further right on hover.

MICRO-INTERACTIONS — specify visibly in the markup, every one matters:
- Every input's bottom hairline animates from dim to full amber and thickens on focus, transition-all duration-300 ease-in-out.
- The format plates lift on hover: hover:-translate-y-1 with a deepening soft shadow, duration-500, and the photographic plate's image scales very slightly (group-hover:scale-105) while its scrim lightens — image and scrim on separate transition durations so it feels layered, not uniform.
- Genre pills fill in from their left edge on hover using a group-hover width reveal, duration-300.
- The primary button's arrow translates on hover; the button itself uses active:scale-95.
- Nav links get an underline that grows from left to full width on hover (group-hover:w-full), duration-300.

AVOID: uniform corner radii everywhere, a grid of identical equal-size cards, generic icon-in-a-circle list rows, plastic-looking stock photography, any hard drop shadow. Warm off-white ground, deep espresso ink, amber accent used sparingly as the only saturated colour. All copy in Vietnamese exactly as written above.
```

---

## PROMPT 2 of 3 — Step 2 "Lịch trình & Sức chứa"

```
Design a desktop web screen for MusicLounge, a premium live-music lounge platform — the second step of a 3-step "create a show" flow, for venue owners. Editorial, asymmetric, quiet-luxury, meticulously detailed; the feel of a high-end architecture publication, not a SaaS dashboard. Use the project's design system as a colour and type palette to compose freely with — NOT as a layout template. This screen must feel like the SAME continuous space as the other steps in this flow: identical background treatment, identical left progress rail, identical nav.

BACKGROUND: a full-bleed animated WebGL shader canvas fixed behind all content, driven by u_time and subtly reactive to u_mouse — very slow, liquid, organic movement, warm cream (#fff9ed) drifting into soft sand (#dfd9cf) with a faint amber (#c97b4a) bloom around 5% intensity lagging behind the cursor. Ambient candlelight, never a busy gradient. A low-opacity film-grain overlay on top.

LAYOUT:
- Same slim sticky transparent nav with backdrop-blur-md as the rest of the flow.
- Same fixed VERTICAL progress rail on the left edge: three stacked numbered markers on a thin line, labels "Thông tin cơ bản" / "Lịch trình & Sức chứa" / "Line-up nghệ sĩ" in tiny uppercase letter-spaced Inter. Marker 1 is now a completed amber checkmark, marker 2 is active (filled amber with soft glow ring), marker 3 is a dimmed hairline outline. The connecting line is filled amber from 1 to 2, hairline from 2 to 3.
- Off-centre content column, generous negative space, nothing wrapped in one big bordered box.

CONTENT:
1. Uppercase eyebrow "BƯỚC 02 — LỊCH TRÌNH & SỨC CHỨA", a very large Playfair Display headline "Khi nào màn nhung mở?", and one muted Inter line: "Chọn thời điểm và quy mô cho đêm diễn."
2. A horizontal TIME RIBBON as the centrepiece — a wide, elegant horizontal band representing the evening, not a plain input pair. Show an hour scale from 18:00 to 01:00 as tiny tick marks with sparse labels along a hairline axis. On it, a single filled amber segment spans 20:00 to 23:00 with rounded ends, labelled "3 giờ" centred inside it. Two draggable circular handles sit at the segment's ends, each with a small floating readout bubble above: the left reads "Thứ Sáu, 22/08 · 20:00" and the right "23:00". Below the ribbon, two small understated bottom-hairline-only line-inputs echo the exact values as editable text: label "BẮT ĐẦU" with value "22/08/2026 20:00", and label "KẾT THÚC" with value "22/08/2026 23:00" plus a tiny italic helper "Để trống nếu chưa xác định".
3. A hairline divider, then a "SỨC CHỨA" section rendered as ONE large occupancy visual, not a number box: a wide horizontal bank of small seat glyphs arranged in slightly irregular rows like a real room's seating plan, where roughly 80 of 120 seats are filled solid amber and the remainder are hairline outlines. Beside it, set very large in Playfair, the number "120" with a small uppercase caption "KHÁCH TỐI ĐA" beneath, and a discreet stepper (− / +) to its right. Add one small muted line: "Số lượng khách tối đa dựa trên mặt bằng thực tế."
4. Action row: outlined "Quay lại" with a left-arrow on the left; primary deep-amber "Tiếp tục" with a right-arrow on the right.

MICRO-INTERACTIONS — render them visibly, every detail counts:
- The time ribbon's handles grow and gain a soft amber glow ring on hover, duration-300; the amber segment brightens slightly at the same time on a longer duration-500 so the two respond at different speeds.
- Individual seat glyphs scale up subtly and warm toward amber on hover, duration-200, so sweeping the cursor across the seating plan feels alive.
- The large "120" figure gets a brief scale pulse when the stepper is pressed; stepper buttons use active:scale-95.
- Line-inputs' bottom hairline animates dim-to-amber and thickens on focus, transition-all duration-300 ease-in-out.
- Buttons: arrows translate outward on hover, whole button uses active:scale-95, shadow deepens on duration-500.
- Nav underlines grow from left to full width on hover.

AVOID: uniform corner radii, a grid of identical cards, plain spinner number inputs as the main capacity control, generic icon-in-a-circle rows, hard drop shadows. Warm off-white ground, deep espresso ink, amber as the only saturated accent, used sparingly. All copy in Vietnamese exactly as written.
```

---

## PROMPT 3 of 3 — Step 3 "Line-up nghệ sĩ"

```
Design a desktop web screen for MusicLounge, a premium live-music lounge platform — the final step of a 3-step "create a show" flow, where a venue owner builds the performer running order. Editorial, asymmetric, quiet-luxury, meticulously detailed; a high-end publication feel, not a SaaS dashboard. Use the project's design system as a colour and type palette to compose freely with — NOT as a layout template. This must read as the SAME continuous space as the flow's other steps: identical background, identical left progress rail, identical nav.

BACKGROUND: a full-bleed animated WebGL shader canvas fixed behind all content, driven by u_time and subtly reactive to u_mouse — slow, liquid, organic, warm cream (#fff9ed) into soft sand (#dfd9cf) with a faint amber (#c97b4a) bloom near 5% lagging behind the cursor. Ambient candlelight, not a busy gradient. Low-opacity film-grain overlay above it.

LAYOUT:
- Same slim sticky transparent nav with backdrop-blur-md.
- Same fixed VERTICAL progress rail on the left edge, three stacked markers on a thin line with tiny uppercase labels "Thông tin cơ bản" / "Lịch trình & Sức chứa" / "Line-up nghệ sĩ". Markers 1 and 2 are completed amber checkmarks, marker 3 is active — filled amber with a soft glow ring. Connecting line filled amber all the way through.
- Two unequal columns: a NARROW left column for finding performers, a WIDER right column for the running order. Nothing wrapped in a single big bordered box.

CONTENT:
1. Uppercase eyebrow "BƯỚC 03 — LINE-UP NGHỆ SĨ", large Playfair Display headline "Ai sẽ lên sân khấu?", one muted Inter line: "Thêm nghệ sĩ và sắp xếp thứ tự biểu diễn. Bạn có thể bỏ qua và thêm sau."
2. LEFT COLUMN — a floating command-palette-style search panel, elevated on a soft warm shadow with backdrop-blur: a bottom-hairline-only search field with a thin magnifier glyph and the typed query "Minh", then an open result list beneath it. Each result is a horizontal row: a circular soft-focus portrait, the performer name in Playfair, and their genre in tiny uppercase muted Inter — "Minh Anh Trio / JAZZ", "Lan Phạm / ACOUSTIC", "The Velvet Keys / PIANO LOUNGE". Rows are separated by hairlines only, no boxes. At the bottom, set apart by a hairline, a quieter amber action row: a thin plus glyph and "Tạo nghệ sĩ mới «Minh»".
3. RIGHT COLUMN — the running order rendered as a vertical STAGE SPINE, not a list of identical cards: a continuous thin vertical amber line runs down the column, and each performer card docks onto it via a small filled node. Above it a header row: "DANH SÁCH BIỂU DIỄN" in tiny uppercase letter-spaced Inter on the left, and on the right a small pill reading "2 nghệ sĩ". Beneath, a tiny muted italic line: "Kéo thả để sắp xếp thứ tự biểu diễn."
   Show two docked cards of DELIBERATELY DIFFERENT visual weight:
   - First, larger and more prominent: a six-dot drag grip at its left edge, a circular portrait, the name "Minh Anh Trio" in Playfair, a small filled-amber chip "NGHỆ SĨ CHÍNH", a set-time shown as large Playfair numerals "20:00" with a tiny uppercase "GIỜ DIỄN" caption above, a slim toggle switch in its ON state labelled "Nhận donation", and a thin trash glyph far right.
   - Second, visibly quieter and slightly narrower: same anatomy but a hairline-outline chip reading "KHÁCH MỜI", time "21:30", the donation toggle in its OFF state, and a softer background.
   Give the two cards different corner radii and different background weights so they are clearly not a repeated component.
4. Action row: outlined "Quay lại" with a left-arrow; primary deep-amber "Tạo show" with a check glyph.

MICRO-INTERACTIONS — render every one visibly:
- Search result rows: the portrait scales slightly and the name shifts to amber on hover (group-hover), on two different durations — 200 for colour, 500 for the image — so the row feels layered rather than switching all at once.
- Docked cards lift on hover with hover:-translate-y-0.5 and a deepening soft shadow, duration-500, while their spine node grows and gains a glow ring on duration-300.
- The drag grip fades from faint to full opacity on card hover and shows a grab cursor; while dragging, the card should read as tilted slightly with a stronger shadow.
- Toggle switches slide their knob with a duration-300 ease-in-out and the track crossfades cream-to-amber.
- The trash glyph warms to a muted red with a soft circular background appearing behind it on hover, duration-200.
- Buttons: arrow/check glyphs translate on hover, active:scale-95, shadow deepens over duration-500.
- Nav underlines grow left-to-full-width on hover.

AVOID: uniform corner radii, a stack of visually identical cards, generic icon-in-a-circle rows, plastic stock photography, hard drop shadows. Warm off-white ground, deep espresso ink, amber the only saturated accent and used sparingly. All copy in Vietnamese exactly as written.
```

---

## What still has to happen React-side (Stitch structurally cannot do these)

1. **Transitions between the 3 views** — the "chuyển cảnh mượt mà" requirement. Stitch has no
   routing concept. Implement in `OwnerCreateShow.tsx` with `animejs` (already a dependency, already
   used there for the entrance animation): cross-fade + slight x-translate of the content column on
   step change, while the shader background and the left progress rail stay mounted and only the
   rail's active marker animates. Keeping the background persistent across steps is exactly why all
   3 prompts specify it identically.
2. **Real 3D**, if genuinely wanted beyond a shader plane — `@react-three/fiber` + `three`, added at
   port time. Natural domain fit would be a 3D preview of the lounge room itself (ties into the
   existing 360° tour / `Model3DUrl` work), not decoration on a form.
3. The shader block Stitch emits is raw WebGL in an IIFE; at port time it becomes a small
   `<ShaderBackground />` React component with the canvas in a ref and the RAF loop cleaned up on
   unmount.
