"""Convert Azure-RAG-Complete-Book.md → a polished, navigable HTML book."""

import re
from pathlib import Path

import markdown


# ──────────────────────────────────────────────
# Post-processing helpers
# ──────────────────────────────────────────────

def _strip_pilcrows(html: str) -> str:
    """Remove the ugly ¶ permalink anchors injected by the toc extension."""
    return re.sub(r'<a class="headerlink"[^>]*>.*?</a>', "", html)


def _clean_toc(toc_html: str) -> str:
    """Remove TOC entries whose visible text is very long (interview-point
    blockquotes that accidentally became headings) or start with '>'."""
    def _drop_long(match: re.Match) -> str:
        raw = match.group(0)
        text = re.sub(r"<[^>]+>", "", raw)        # strip tags → plain text
        if len(text) > 120 or text.lstrip().startswith(">"):
            return ""
        return raw

    return re.sub(r"<li>.*?</li>", _drop_long, toc_html, flags=re.S)


def _add_chapter_dividers(html: str) -> str:
    """Insert a visible divider before every <h2> (chapter start)."""
    return html.replace(
        "<h2 id=",
        '<div class="chapter-break" aria-hidden="true"></div>\n<h2 id=',
    )


def _repair_mojibake(text: str) -> str:
    """Repair common UTF-8 mojibake that was decoded through Windows-1252.

    This preserves normal Unicode text and only applies the repair when the
    content clearly contains broken sequences such as â or Ã.
    """
    if not any(marker in text for marker in ("â", "Ã", "ðŸ", "â†", "â‰")):
        return text

    raw = bytearray()
    for char in text.lstrip("\ufeff"):
        codepoint = ord(char)
        if codepoint <= 255:
            raw.append(codepoint)
        else:
            raw.extend(char.encode("cp1252"))

    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return text


# ──────────────────────────────────────────────
# HTML shell
# ──────────────────────────────────────────────

def build_html(title: str, toc_html: str, content_html: str) -> str:
    return f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{title}</title>
  <style>
/* ── Reset & Variables ─────────────────────── */
:root {{
  --sidebar-w: 300px;
  --bg: #f0f2f5;
  --panel: #1b1f3b;
  --panel-hover: rgba(255,255,255,.07);
  --panel-active: rgba(99,140,255,.22);
  --text: #eaedf6;
  --muted: #9aa3c4;
  --accent: #638cff;
  --content-bg: #ffffff;
  --content-text: #1e2330;
  --code-bg: #f6f8fc;
  --border: #d6dce8;
  --heading-color: #1a2744;
  --link: #3e6fd9;
  --table-stripe: #f9fafd;
  --callout-bg: #eef4ff;
  --callout-border: #a0bfff;
  --interview-bg: #fff8e6;
  --interview-border: #ffc94d;
  --radius: 10px;
}}

*, *::before, *::after {{ box-sizing: border-box; }}
html {{ scroll-behavior: smooth; }}
body {{ margin: 0; font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
       background: var(--bg); color: var(--content-text); line-height: 1.72;
       font-size: 16px; }}

/* ── Progress bar ──────────────────────────── */
#progress {{ position: fixed; top: 0; left: 0; height: 3px; background: var(--accent);
             width: 0%; z-index: 999; transition: width .15s; }}

/* ── Layout ────────────────────────────────── */
.layout {{
  display: grid;
  grid-template-columns: var(--sidebar-w) 1fr;
  min-height: 100vh;
}}

/* ── Sidebar ───────────────────────────────── */
.sidebar {{
  position: sticky; top: 0; height: 100vh;
  overflow-y: auto; overflow-x: hidden;
  background: var(--panel);
  padding: 0;
  border-right: 1px solid rgba(255,255,255,.06);
  scrollbar-width: thin;
  scrollbar-color: rgba(255,255,255,.15) transparent;
}}
.sidebar::-webkit-scrollbar {{ width: 5px; }}
.sidebar::-webkit-scrollbar-thumb {{ background: rgba(255,255,255,.15); border-radius: 9px; }}

.sidebar-header {{
  padding: 1.25rem 1.1rem .6rem;
  border-bottom: 1px solid rgba(255,255,255,.08);
  margin-bottom: .5rem;
}}
.sidebar-header h2 {{
  margin: 0; font-size: .82rem;
  color: var(--muted); letter-spacing: .06em; text-transform: uppercase;
  font-weight: 600;
}}
.sidebar-header .book-title {{
  margin: .35rem 0 0; font-size: 1.05rem; color: #fff; font-weight: 700;
}}

.sidebar ul {{ list-style: none; margin: 0; padding: 0; }}
.sidebar > .toc > ul {{ padding: 0 .55rem .8rem; }}
.sidebar li {{ margin: 1px 0; }}
.sidebar a {{
  color: var(--text); text-decoration: none;
  display: block; padding: .32rem .65rem;
  border-radius: 6px; font-size: .88rem;
  transition: background .15s, color .15s;
  white-space: nowrap; overflow: hidden; text-overflow: ellipsis;
}}
.sidebar a:hover {{ background: var(--panel-hover); color: #fff; }}
.sidebar a.active {{ background: var(--panel-active); color: #fff; font-weight: 600; }}

.sidebar ul ul {{
  padding-left: .7rem;
  margin-left: .45rem;
  border-left: 1px solid rgba(255,255,255,.1);
}}
.sidebar ul ul a {{ font-size: .82rem; color: var(--muted); padding: .22rem .55rem; }}
.sidebar ul ul a:hover,
.sidebar ul ul a.active {{ color: #fff; }}

/* ── Main content ──────────────────────────── */
.content-wrap {{ padding: 2.5rem 2rem; overflow-y: auto; }}

.content {{
  max-width: 920px; margin: 0 auto;
  background: var(--content-bg);
  border-radius: var(--radius);
  padding: 2.8rem 3rem 3rem;
  box-shadow: 0 1px 4px rgba(0,0,0,.06), 0 12px 40px rgba(0,0,0,.07);
}}

/* ── Typography ────────────────────────────── */
h1, h2, h3, h4, h5, h6 {{
  color: var(--heading-color);
  scroll-margin-top: 1.4rem;
  line-height: 1.3;
}}
h1 {{ font-size: 2.1rem; margin: 0 0 .6rem; letter-spacing: -.02em; }}
h2 {{ font-size: 1.55rem; margin: 2.8rem 0 .7rem;
      padding-bottom: .45rem; border-bottom: 2px solid var(--accent); }}
h3 {{ font-size: 1.2rem; margin: 2rem 0 .5rem; }}
h4 {{ font-size: 1.05rem; margin: 1.4rem 0 .4rem; }}

p {{ margin: .6rem 0; }}
a {{ color: var(--link); }}
strong {{ font-weight: 650; }}
hr {{ border: none; border-top: 1px solid var(--border); margin: 2rem 0; }}

/* ── Lists ─────────────────────────────────── */
ul, ol {{ padding-left: 1.6rem; }}
li {{ margin: .25rem 0; }}
li > ul, li > ol {{ margin-top: .15rem; }}

/* ── Code ──────────────────────────────────── */
code {{
  font-family: 'Cascadia Code', Consolas, 'Courier New', monospace;
  font-size: .88em;
  background: var(--code-bg);
  padding: .12rem .35rem;
  border-radius: 5px;
  border: 1px solid #e9ecf5;
}}
pre {{
  background: #1e2340;
  color: #d4d8f0;
  padding: 1.1rem 1.2rem;
  border-radius: var(--radius);
  overflow-x: auto;
  font-size: .875rem;
  line-height: 1.55;
  margin: 1rem 0;
  border: 1px solid #2a2f52;
}}
pre code {{
  background: transparent;
  border: none;
  padding: 0;
  color: inherit;
  font-size: inherit;
}}

/* ── Tables ────────────────────────────────── */
table {{
  border-collapse: collapse;
  width: 100%; margin: 1.2rem 0;
  font-size: .92rem;
  border-radius: var(--radius);
  overflow: hidden;
  border: 1px solid var(--border);
}}
thead {{ background: #f2f4fb; }}
th {{
  text-align: left;
  padding: .65rem .75rem;
  font-weight: 650;
  color: var(--heading-color);
  border-bottom: 2px solid var(--border);
}}
td {{
  padding: .55rem .75rem;
  border-bottom: 1px solid #eef0f7;
  vertical-align: top;
}}
tbody tr:nth-child(even) {{ background: var(--table-stripe); }}
tbody tr:hover {{ background: #eef2fd; }}

/* ── Blockquotes / callouts ────────────────── */
blockquote {{
  margin: 1.2rem 0;
  padding: .85rem 1.1rem;
  border-left: 4px solid var(--callout-border);
  background: var(--callout-bg);
  border-radius: 0 var(--radius) var(--radius) 0;
  font-size: .95rem;
}}
blockquote p {{ margin: .3rem 0; }}

/* Interview-point callouts (contain 🎯) */
blockquote:has(strong:first-child) {{
  border-left-color: var(--interview-border);
  background: var(--interview-bg);
}}

/* ── Chapter dividers ──────────────────────── */
.chapter-break {{
  height: 0;
  margin: 3rem 0 .5rem;
  border: none;
  border-top: 3px dashed #e0e4ef;
}}
.content > .chapter-break:first-child {{ display: none; }}

/* ── Back-to-top ───────────────────────────── */
#top-btn {{
  position: fixed; bottom: 2rem; right: 2rem;
  width: 44px; height: 44px;
  border-radius: 50%; border: none;
  background: var(--accent); color: #fff;
  font-size: 1.3rem; cursor: pointer;
  box-shadow: 0 4px 14px rgba(99,140,255,.4);
  opacity: 0; pointer-events: none;
  transition: opacity .25s, transform .25s;
  display: flex; align-items: center; justify-content: center;
  z-index: 90;
}}
#top-btn.show {{ opacity: 1; pointer-events: auto; }}
#top-btn:hover {{ transform: translateY(-3px); }}

/* ── Mobile menu toggle ────────────────────── */
#menu-toggle {{
  display: none;
  position: fixed; top: .7rem; left: .7rem;
  z-index: 200;
  width: 40px; height: 40px;
  border-radius: 8px; border: none;
  background: var(--panel); color: #fff;
  font-size: 1.4rem; cursor: pointer;
  box-shadow: 0 2px 8px rgba(0,0,0,.25);
}}

/* ── Responsive ────────────────────────────── */
@media (max-width: 1100px) {{
  .layout {{ grid-template-columns: 1fr; }}
  .sidebar {{
    position: fixed; left: -320px; top: 0; width: 300px;
    height: 100vh; z-index: 150;
    transition: left .3s ease;
  }}
  .sidebar.open {{ left: 0; }}
  .overlay {{
    display: none;
    position: fixed; inset: 0; z-index: 140;
    background: rgba(0,0,0,.45);
  }}
  .overlay.show {{ display: block; }}
  #menu-toggle {{ display: flex; align-items: center; justify-content: center; }}
  .content-wrap {{ padding: 1.2rem; }}
  .content {{ padding: 1.5rem 1.3rem; }}
  h2 {{ font-size: 1.3rem; }}
}}

/* ── Print ─────────────────────────────────── */
@media print {{
  @page {{ size: A4; margin: 18mm 14mm 22mm 14mm;
    @bottom-right {{ content: "Page " counter(page) " of " counter(pages);
                     font-family: sans-serif; font-size: 9pt; color: #666; }}
  }}
  body {{ background: #fff; color: #000; font-size: 11pt; }}
  .layout {{ display: block; }}
  .sidebar, #top-btn, #menu-toggle, .overlay, #progress {{ display: none !important; }}
  .content-wrap {{ padding: 0; }}
  .content {{ max-width: 100%; box-shadow: none; border-radius: 0; padding: 0; }}
  a {{ color: #000; text-decoration: none; }}
  pre {{ background: #f5f5f5; color: #000; border: 1px solid #ccc; }}
  h2 {{ page-break-after: avoid; border-bottom-color: #000; }}
  table, pre, blockquote {{ page-break-inside: avoid; }}
  .chapter-break {{ page-break-before: always; border: none; height: 0; margin: 0; }}
}}
  </style>
</head>
<body>

<div id="progress"></div>

<button id="menu-toggle" aria-label="Open navigation">&#9776;</button>
<div class="overlay" id="overlay"></div>

<div class="layout">
  <aside class="sidebar" id="sidebar">
    <div class="sidebar-header">
      <h2>Contents</h2>
      <div class="book-title">{title}</div>
    </div>
    <div class="toc">
      {toc_html}
    </div>
  </aside>

  <main class="content-wrap">
    <article class="content">
      {content_html}
    </article>
  </main>
</div>

<button id="top-btn" aria-label="Back to top">&#8593;</button>

<script>
/* ── Progress bar ────────────────────────── */
(function(){{
  const bar = document.getElementById('progress');
  window.addEventListener('scroll', function(){{
    const h = document.documentElement;
    const pct = (h.scrollTop / (h.scrollHeight - h.clientHeight)) * 100;
    bar.style.width = Math.min(pct, 100) + '%';
  }});
}})();

/* ── Back-to-top ─────────────────────────── */
(function(){{
  const btn = document.getElementById('top-btn');
  window.addEventListener('scroll', function(){{
    btn.classList.toggle('show', window.scrollY > 400);
  }});
  btn.addEventListener('click', function(){{ window.scrollTo({{top:0,behavior:'smooth'}}); }});
}})();

/* ── Mobile menu ─────────────────────────── */
(function(){{
  const sb = document.getElementById('sidebar');
  const ov = document.getElementById('overlay');
  const btn = document.getElementById('menu-toggle');
  function toggle(){{ sb.classList.toggle('open'); ov.classList.toggle('show'); }}
  btn.addEventListener('click', toggle);
  ov.addEventListener('click', toggle);
  sb.addEventListener('click', function(e){{
    if(e.target.tagName === 'A') {{ sb.classList.remove('open'); ov.classList.remove('show'); }}
  }});
}})();

/* ── Scroll-spy (highlight active TOC link) */
(function(){{
  const links = document.querySelectorAll('.sidebar a[href^="#"]');
  const ids = Array.from(links).map(a => a.getAttribute('href').slice(1));
  const targets = ids.map(id => document.getElementById(id)).filter(Boolean);
  function onScroll(){{
    let active = null;
    for(let i = targets.length - 1; i >= 0; i--){{
      if(targets[i].getBoundingClientRect().top <= 120){{ active = ids[i]; break; }}
    }}
    links.forEach(a => {{
      a.classList.toggle('active', a.getAttribute('href') === '#' + active);
    }});
  }}
  window.addEventListener('scroll', onScroll, {{passive:true}});
  onScroll();
}})();
</script>
</body>
</html>
"""


def convert_file(source: Path, output: Path, title: str) -> None:
    text = source.read_text(encoding="utf-8")
    text = _repair_mojibake(text)

    converter = markdown.Markdown(
        extensions=[
            "extra",
            "tables",
            "fenced_code",
            "sane_lists",
            "toc",
        ],
        extension_configs={
            "toc": {
                "permalink": False,
                "toc_depth": "2-3",
            }
        },
        output_format="html5",
    )

    content_html = converter.convert(text)
    toc_html = converter.toc

    # Post-process
    content_html = _strip_pilcrows(content_html)
    content_html = _add_chapter_dividers(content_html)
    toc_html = _clean_toc(toc_html)

    html = build_html(title, toc_html, content_html)
    output.write_text(html, encoding="utf-8")
    print(f"Generated: {output}  ({output.stat().st_size:,} bytes)")


def main() -> None:
    import sys
    if len(sys.argv) > 1:
        src = Path(sys.argv[1])
        out = src.with_suffix(".html")
        title = src.stem.replace("-", " ").replace("_", " ")
        convert_file(src, out, title)
    else:
        # Default: convert all guides
        books = [
            (Path("Documents/Azure-RAG-Complete-Book.md"), "Azure RAG — Complete Production Guide"),
            (Path("Documents/Azure-RAG-Complete-Book-Part1.md"), "Azure RAG — Complete Book Part 1"),
            (Path("Documents/Azure-RAG-Complete-Book-Part2.md"), "Azure RAG — Complete Book Part 2"),
            (Path("Documents/Agentic-RAG-Complete-Guide.md"), "Agentic RAG — Complete Guide"),
        ]
        for src, title in books:
            if src.exists():
                convert_file(src, src.with_suffix(".html"), title)


if __name__ == "__main__":
    main()
