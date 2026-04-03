from pathlib import Path
import markdown

md_path = Path(r"e:\Agentic RAG\documents\Prompt-Engineering-for-RAG.md")
html_path = Path(r"e:\Agentic RAG\documents\Prompt-Engineering-for-RAG.html")

md_text = md_path.read_text(encoding="utf-8")
md = markdown.Markdown(extensions=["fenced_code", "tables", "toc", "sane_lists"], extension_configs={"toc": {"permalink": False}})
body = md.convert(md_text)
toc = md.toc

if body.startswith("<h1"):
    end = body.find("</h1>")
    if end != -1:
        body = body[end + 5:].lstrip()

html = f'''<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Prompt Engineering for RAG</title>
  <style>
:root {{ --sidebar-w:300px; --bg:#eef2f8; --panel:#162038; --panel-hover:rgba(255,255,255,.08); --panel-active:rgba(87,132,255,.28); --text:#ecf1ff; --muted:#9bacd4; --accent:#4f7fff; --content-bg:#fff; --content-text:#1d2533; --heading:#12284d; --border:#d8dfef; --inline-code-bg:#f2f6ff; --code-bg:#101a2e; --code-text:#d8e2ff; --table-stripe:#f8faff; --radius:12px; }}
* {{ box-sizing:border-box; }}
html {{ scroll-behavior:smooth; }}
body {{ margin:0; font-family:"Segoe UI",system-ui,-apple-system,sans-serif; line-height:1.72; color:var(--content-text); background:radial-gradient(circle at top left,#f9fbff,var(--bg)); }}
#progress {{ position:fixed; top:0; left:0; height:3px; width:0; background:linear-gradient(90deg,#4f7fff,#19b8ff); z-index:999; transition:width .15s ease; }}
.layout {{ display:grid; grid-template-columns:var(--sidebar-w) 1fr; min-height:100vh; }}
.sidebar {{ position:sticky; top:0; height:100vh; overflow-y:auto; background:linear-gradient(180deg,#1a2745 0%,#101a2f 100%); border-right:1px solid rgba(255,255,255,.08); }}
.sidebar-header {{ padding:1.2rem 1rem .8rem; border-bottom:1px solid rgba(255,255,255,.1); }}
.sidebar-header .tag {{ margin:0; color:var(--muted); text-transform:uppercase; letter-spacing:.08em; font-size:.76rem; }}
.sidebar-header h2 {{ margin:.35rem 0 0; color:#fff; font-size:1.02rem; line-height:1.35; }}
.toc {{ padding:.65rem .55rem 1rem; }}
.toc ul {{ list-style:none; padding:0; margin:0; }}
.toc li {{ margin:2px 0; }}
.toc a {{ display:block; text-decoration:none; color:var(--text); padding:.35rem .62rem; border-radius:7px; font-size:.87rem; }}
.toc a:hover {{ background:var(--panel-hover); color:#fff; }}
.toc a.active {{ background:var(--panel-active); color:#fff; font-weight:600; }}
.toc ul ul a {{ color:var(--muted); font-size:.82rem; padding-left:1.35rem; }}
.content-wrap {{ padding:2rem 2rem 3rem; }}
.content {{ max-width:980px; margin:0 auto; background:var(--content-bg); border:1px solid #dce4f3; border-radius:16px; padding:2.1rem 2.4rem 2.6rem; box-shadow:0 12px 40px rgba(17,34,67,.08); }}
.page-title {{ margin:0; font-size:2rem; line-height:1.25; letter-spacing:-.02em; color:var(--heading); }}
.page-subtitle {{ margin:.45rem 0 1.4rem; color:#536283; font-size:1rem; }}
h1,h2,h3,h4 {{ color:var(--heading); line-height:1.3; scroll-margin-top:1rem; }}
h2 {{ font-size:1.45rem; margin-top:2.2rem; border-bottom:2px solid #dce7ff; padding-bottom:.38rem; }}
h3 {{ font-size:1.12rem; margin-top:1.55rem; }}
hr {{ border:none; border-top:1px solid var(--border); margin:1.6rem 0; }}
ul,ol {{ padding-left:1.55rem; }}
code {{ font-family:"Cascadia Code",Consolas,monospace; background:var(--inline-code-bg); border:1px solid #e4ebfa; border-radius:5px; padding:.1rem .32rem; font-size:.88em; }}
pre {{ background:var(--code-bg); color:var(--code-text); border:1px solid #273550; border-radius:var(--radius); padding:1rem 1.15rem; overflow-x:auto; font-size:.86rem; line-height:1.56; }}
pre code {{ background:transparent; border:none; color:inherit; padding:0; }}
table {{ width:100%; border-collapse:collapse; margin:1rem 0; border:1px solid var(--border); border-radius:10px; overflow:hidden; font-size:.92rem; }}
th,td {{ border:1px solid var(--border); padding:.56rem .7rem; text-align:left; vertical-align:top; }}
th {{ background:#ebf1ff; color:#17345f; font-weight:650; }}
tr:nth-child(even) {{ background:var(--table-stripe); }}
blockquote {{ margin:1rem 0; padding:.75rem 1rem; border-left:4px solid #f1bf3a; background:#fff8e9; border-radius:0 10px 10px 0; }}
@media (max-width:980px) {{ .layout {{ grid-template-columns:1fr; }} .sidebar {{ position:static; height:auto; max-height:42vh; }} .content-wrap {{ padding:1rem; }} .content {{ padding:1.2rem 1rem 1.6rem; }} }}
  </style>
</head>
<body>
<div id="progress"></div>
<div class="layout">
  <nav class="sidebar">
    <div class="sidebar-header"><p class="tag">AgenticRAG</p><h2>Prompt Engineering for RAG</h2></div>
    <div class="toc">{toc}</div>
  </nav>
  <main class="content-wrap"><article class="content"><h1 class="page-title">Prompt Engineering for RAG</h1><p class="page-subtitle">System prompts, few-shot examples, Chain-of-Thought control, and intent-based routing with practical examples.</p>{body}</article></main>
</div>
<script>
const progress=document.getElementById('progress');
window.addEventListener('scroll',()=>{{const d=document.documentElement; const pct=(d.scrollTop/(d.scrollHeight-d.clientHeight))*100; progress.style.width=Math.max(0,Math.min(100,pct))+'%';}});
const links=[...document.querySelectorAll('.toc a')];
const targets=links.map(a=>document.getElementById((a.getAttribute('href')||'').slice(1))).filter(Boolean);
const map=new Map(links.map(a=>[(a.getAttribute('href')||'').slice(1),a]));
const obs=new IntersectionObserver((entries)=>{{for(const e of entries){{if(e.isIntersecting){{links.forEach(l=>l.classList.remove('active')); const link=map.get(e.target.id); if(link) link.classList.add('active'); break;}}}}},{{rootMargin:'-28% 0px -60% 0px',threshold:0.1}});
targets.forEach(t=>obs.observe(t));
</script>
</body>
</html>'''

html_path.write_text(html, encoding="utf-8")
print(str(html_path))
