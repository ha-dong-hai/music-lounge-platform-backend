"""Render SLIDE-CONTENT.md into a single self-contained HTML page.

The Markdown file is the deliverable — it is what gets handed to whatever tool builds
the PowerPoint. This page exists so the team can read and review the same content
without a Markdown viewer, and it is *generated* rather than written by hand so the two
can never drift apart.

    Usage:  python spec/slides/build_page.py
"""

from __future__ import annotations

import pathlib
import re
import sys

import markdown

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parent.parent

# Each entry is one document this script can render. The Markdown file stays the
# deliverable; the page is generated so the two cannot drift apart.
DOCS = {
    "slides": dict(
        src=HERE / "SLIDE-CONTENT.md",
        out=HERE / "slide-content.html",
        title="MusicLounge Defence Deck",
        eyebrow="SEP490 · Group GSU26SE68 · build specification",
        sub="Slide-by-slide content for the capstone defence deck: exact on-slide text, "
            "the diagram to place on each slide, and a Vietnamese speaker note. "
            "Structured against the reference SEP490 template. Generated from "
            "<code>SLIDE-CONTENT.md</code> — edit that file, not this page.",
    ),
    "qa": dict(
        src=ROOT / "docs" / "28-defence-qa-backend.md",
        out=ROOT / "docs" / "28-defence-qa-backend.html",
        title="Backend Defence Q&A",
        eyebrow="SEP490 · MusicLounge · chuẩn bị phản biện",
        sub="45 câu hỏi hội đồng có thể hỏi về backend, từ cơ bản tới nâng cao, "
            "kèm lời giải thích diễn đạt để người không rành kỹ thuật cũng hiểu. "
            "Mọi con số lấy trực tiếp từ code.",
    ),
}

CSS = """
:root {
  color-scheme: light dark;
  --ground:      #fbfaf8;
  --surface:     #ffffff;
  --surface-alt: #f3f1ed;
  --ink:         #1c1a17;
  --ink-soft:    #57524a;
  --ink-faint:   #8a8378;
  --rule:        #ddd8cf;
  --accent:      #14595c;
  --accent-soft: #e4efee;
  --warn:        #8a5300;
  --warn-soft:   #fbf0dc;
  --warn-rule:   #e0b86a;
  --star:        #7a4b12;
  --weak:        #a8322b;
  --measure: 74ch;
}
@media (prefers-color-scheme: dark) {
  :root:not([data-theme="light"]) {
    --ground:      #16171a;
    --surface:     #1d1f23;
    --surface-alt: #24272c;
    --ink:         #e9e6e0;
    --ink-soft:    #b0aaa1;
    --ink-faint:   #7e7970;
    --rule:        #34383e;
    --accent:      #6fc4c2;
    --accent-soft: #1b3234;
    --warn:        #e3ae5c;
    --warn-soft:   #2e2519;
    --warn-rule:   #6b5226;
    --star:        #e0b06a;
    --weak:        #c9564a;
  }
}
:root[data-theme="dark"] {
  --ground:      #16171a;
  --surface:     #1d1f23;
  --surface-alt: #24272c;
  --ink:         #e9e6e0;
  --ink-soft:    #b0aaa1;
  --ink-faint:   #7e7970;
  --rule:        #34383e;
  --accent:      #6fc4c2;
  --accent-soft: #1b3234;
  --warn:        #e3ae5c;
  --warn-soft:   #2e2519;
  --warn-rule:   #6b5226;
  --star:        #e0b06a;
  --weak:        #c9564a;
}

* { box-sizing: border-box; }

body {
  margin: 0;
  background: var(--ground);
  color: var(--ink);
  font-family: "Source Sans 3", ui-sans-serif, system-ui, "Segoe UI", sans-serif;
  font-size: 16.5px;
  line-height: 1.62;
  -webkit-font-smoothing: antialiased;
}

.wrap {
  max-width: 60rem;
  margin: 0 auto;
  padding: 4rem 1.5rem 8rem;
}

/* ── masthead ───────────────────────────────────────────────────────────── */
header.mast {
  border-bottom: 2px solid var(--ink);
  padding-bottom: 1.6rem;
  margin-bottom: 3rem;
}
header.mast .eyebrow {
  font-size: .74rem;
  letter-spacing: .16em;
  text-transform: uppercase;
  color: var(--accent);
  font-weight: 600;
  margin: 0 0 .7rem;
}
header.mast h1 {
  font-family: "Source Serif 4", ui-serif, Georgia, serif;
  font-size: clamp(2rem, 4.6vw, 2.9rem);
  line-height: 1.12;
  font-weight: 600;
  letter-spacing: -.015em;
  text-wrap: balance;
  margin: 0 0 .6rem;
}
header.mast .sub { color: var(--ink-soft); max-width: var(--measure); margin: 0; }

/* ── headings ───────────────────────────────────────────────────────────── */
h1, h2, h3, h4 {
  font-family: "Source Serif 4", ui-serif, Georgia, serif;
  font-weight: 600;
  text-wrap: balance;
  letter-spacing: -.01em;
}
h1 { font-size: 1.9rem; margin: 4rem 0 1rem; }
h2 {
  font-size: 1.05rem;
  font-family: "Source Sans 3", ui-sans-serif, system-ui, sans-serif;
  font-weight: 700;
  letter-spacing: .12em;
  text-transform: uppercase;
  color: var(--accent);
  margin: 4.5rem 0 1.4rem;
  padding-bottom: .55rem;
  border-bottom: 1px solid var(--rule);
}
/* Each slide opens with a full-width bar, so a reader scrolling fast can find the
   slide boundaries without reading them. */
h3 {
  font-size: 1.16rem;
  margin: 2.6rem 0 1.2rem;
  padding: .85rem 1.15rem;
  background: var(--surface);
  border: 1px solid var(--rule);
  border-left: 4px solid var(--accent);
  border-radius: 5px;
}
h4 { font-size: 1rem; margin: 2rem 0 .5rem; color: var(--ink-soft); }

p, ul, ol { max-width: var(--measure); }
p { margin: 0 0 1rem; }
ul, ol { margin: 0 0 1rem; padding-left: 1.3rem; }
li { margin-bottom: .35rem; }
li::marker { color: var(--ink-faint); }

a { color: var(--accent); text-underline-offset: 2px; }
a:focus-visible, summary:focus-visible {
  outline: 2px solid var(--accent); outline-offset: 3px; border-radius: 3px;
}

strong { font-weight: 650; }
hr { border: none; border-top: 1px solid var(--rule); margin: 2.6rem 0; }

/* ── code & filenames ───────────────────────────────────────────────────── */
code {
  font-family: "JetBrains Mono", ui-monospace, "Cascadia Mono", Consolas, monospace;
  font-size: .855em;
  background: var(--surface-alt);
  border: 1px solid var(--rule);
  border-radius: 4px;
  padding: .1em .38em;
  word-break: break-word;
}
pre {
  background: var(--surface-alt);
  border: 1px solid var(--rule);
  border-radius: 6px;
  padding: 1rem 1.15rem;
  overflow-x: auto;
  line-height: 1.5;
}
pre code { background: none; border: none; padding: 0; font-size: .84rem; }

/* ── tables ─────────────────────────────────────────────────────────────── */
.tablewrap { overflow-x: auto; margin: 0 0 1.4rem; border-radius: 6px; }
table {
  border-collapse: collapse;
  width: 100%;
  min-width: 32rem;
  font-size: .92rem;
  font-variant-numeric: tabular-nums;
  background: var(--surface);
}
th, td {
  text-align: left;
  padding: .58rem .8rem;
  border-bottom: 1px solid var(--rule);
  vertical-align: top;
}
thead th {
  background: var(--surface-alt);
  font-size: .76rem;
  letter-spacing: .07em;
  text-transform: uppercase;
  color: var(--ink-soft);
  font-weight: 700;
  border-bottom: 1.5px solid var(--rule);
  white-space: nowrap;
}
tbody tr:last-child td { border-bottom: none; }
td code { font-size: .8em; }

/* ── callouts ───────────────────────────────────────────────────────────── */
blockquote {
  margin: 0 0 1.4rem;
  padding: .8rem 1.1rem;
  background: var(--accent-soft);
  border-left: 3px solid var(--accent);
  border-radius: 0 5px 5px 0;
  color: var(--ink-soft);
  font-size: .94rem;
  max-width: var(--measure);
}
blockquote p:last-child { margin-bottom: 0; }

.warn {
  background: var(--warn-soft);
  border: 1px solid var(--warn-rule);
  border-left: 3px solid var(--warn-rule);
  border-radius: 0 5px 5px 0;
  padding: .85rem 1.1rem;
  margin: 0 0 1.2rem;
  color: var(--warn);
  max-width: var(--measure);
}
.warn strong { color: var(--warn); }
.warn code { background: transparent; border-color: var(--warn-rule); }

/* ── badges inside slide headings ───────────────────────────────────────── */
.tag {
  display: inline-block;
  font-family: "Source Sans 3", ui-sans-serif, system-ui, sans-serif;
  font-size: .66rem;
  font-weight: 700;
  letter-spacing: .09em;
  text-transform: uppercase;
  padding: .17em .5em;
  border-radius: 3px;
  vertical-align: .18em;
  margin-left: .45rem;
  white-space: nowrap;
}
.tag-core   { background: var(--accent); color: var(--ground); }
.tag-backup { background: var(--surface-alt); color: var(--ink-soft); border: 1px solid var(--rule); }
.tag-weak   { background: var(--weak); color: var(--ground); }
h3.weak     { border-left-color: var(--weak); }
.tag-star   { color: var(--star); font-size: .95rem; margin-left: .3rem; letter-spacing: 0; }

/* ── speaker note ───────────────────────────────────────────────────────── */
.note-vi {
  border-left: 3px solid var(--rule);
  padding-left: .95rem;
  color: var(--ink-soft);
  font-size: .95rem;
}

@media (max-width: 640px) {
  .wrap { padding: 2.5rem 1rem 5rem; }
  body { font-size: 16px; }
  h3 { padding: .85rem .9rem .65rem; }
}
"""

JS = """
document.querySelectorAll('table').forEach(t => {
  if (t.parentElement.classList.contains('tablewrap')) return;
  const w = document.createElement('div');
  w.className = 'tablewrap';
  t.replaceWith(w); w.appendChild(t);
});
"""


def transform(html: str) -> str:
    """Turn the plain Markdown output into the structure the stylesheet expects."""
    # `[CORE]` / `[BACKUP]` inside a slide heading become badges.
    html = re.sub(r"<code>\[CORE\]</code>", '<span class="tag tag-core">core</span>', html)
    html = re.sub(r"<code>\[BACKUP\]</code>",
                  '<span class="tag tag-backup">backup</span>', html)
    html = html.replace("⭐", '<span class="tag-star" title="key slide">★</span>')

    # A paragraph that opens with the warning marker becomes a callout box.
    html = re.sub(r"<p>(⚠️.*?)</p>", r'<div class="warn"><p>\1</p></div>',
                  html, flags=re.S)

    # A heading carrying the red marker names a genuine weakness of the system.
    # Swap the emoji for a badge and flag the heading, so a reader skimming for the
    # hard questions finds them without reading every one.
    def mark(m):
        head = m.group(1)
        weak = '🔴' in head
        head = head.replace('🔴', "").strip()
        head = " ".join(head.split())   # the removed emoji left a double space
        if not weak:
            return "<h3 class=" + chr(34) + "qh" + chr(34) + ">" + head + "</h3>"
        badge = '<span class="tag tag-weak">điểm yếu</span>'
        return "<h3 class=" + chr(34) + "qh weak" + chr(34) + ">" + head + badge + "</h3>"

    html = re.sub(r"<h3>(.*?)</h3>", mark, html, flags=re.S)

    # The Vietnamese speaker note gets its own quieter treatment.
    html = re.sub(r"<p>(<strong>Speaker note \(VI\).*?)</p>",
                  r'<p class="note-vi">\1</p>', html, flags=re.S)
    return html


def main() -> int:
    key = sys.argv[1] if len(sys.argv) > 1 else "slides"
    if key not in DOCS:
        print(f"unknown document {key!r}; choose from {', '.join(DOCS)}")
        return 2
    doc = DOCS[key]
    SRC, OUT = doc["src"], doc["out"]
    md = SRC.read_text(encoding="utf-8")

    # The first heading becomes the masthead, so it is not left in the body.
    lines = md.splitlines()
    title = lines[0].lstrip("# ").strip()
    body_md = "\n".join(lines[1:])

    html = markdown.markdown(
        body_md, extensions=["tables", "fenced_code", "sane_lists", "attr_list"]
    )
    html = transform(html)

    page = f"""<title>{doc["title"]}</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?\
family=Source+Sans+3:ital,wght@0,400;0,600;0,700;1,400&\
family=Source+Serif+4:opsz,wght@8..60,500;8..60,600&\
family=JetBrains+Mono:wght@400;500&display=swap">
<style>{CSS}</style>
<div class="wrap">
<header class="mast">
  <p class="eyebrow">{doc["eyebrow"]}</p>
  <h1>{title.split("—", 1)[0].strip()}</h1>
  <p class="sub">{doc["sub"]}</p>
</header>
{html}
</div>
<script>{JS}</script>
"""
    OUT.write_text(page, encoding="utf-8")
    print(f"wrote {OUT}  ({len(page):,} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
