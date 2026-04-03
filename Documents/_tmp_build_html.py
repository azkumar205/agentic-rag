from pathlib import Path
import markdown

md_path = Path(r"e:\Agentic RAG\documents\Prompt-Engineering-for-RAG.md")
out_path = Path(r"e:\Agentic RAG\documents\Prompt-Engineering-for-RAG.html")

src = md_path.read_text(encoding="utf-8")
md = markdown.Markdown(extensions=["fenced_code", "tables", "toc", "sane_lists"], extension_configs={"toc": {"permalink": False}})
body = md.convert(src)
toc = md.toc

if body.startswith("<h1"):
    i = body.find("</h1>")
    if i != -1:
        body = body[i+5:].lstrip()

template = """<!doctype html>
<html lang=\"en\">
<head>
<meta charset=\"utf-8\" />
<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />
<title>Prompt Engineering for RAG</title>
<style>
:root{--sidebar-w:290px;--bg:#eef3f8;--panel:#16213a;--panel-hover:rgba(255,255,255,.08);--panel-active:rgba(87,132,255,.28);--text:#ecf2ff;--muted:#9aabd2;--content-bg:#fff;--content-text:#1f2736;--heading:#132a4e;--border:#d7dfef;--inline:#f2f6ff;--code:#101b2f;--code-text:#d6e2ff;--stripe:#f8faff;}
*{box-sizing:border-box} html{scroll-behavior:smooth} body{margin:0;font-family:Segoe UI,system-ui,sans-serif;background:radial-gradient(circle at top left,#f9fbff,var(--bg));color:var(--content-text);line-height:1.72}
#progress{position:fixed;top:0;left:0;height:3px;width:0;background:linear-gradient(90deg,#4f7fff,#19b8ff);z-index:99}
.layout{display:grid;grid-template-columns:var(--sidebar-w) 1fr;min-height:100vh}
.sidebar{position:sticky;top:0;height:100vh;overflow-y:auto;background:linear-gradient(180deg,#1a2745,#101a2f);border-right:1px solid rgba(255,255,255,.08)}
.sidebar-header{padding:1.1rem 1rem .8rem;border-bottom:1px solid rgba(255,255,255,.1)} .sidebar-header p{margin:0;color:var(--muted);font-size:.76rem;letter-spacing:.08em;text-transform:uppercase} .sidebar-header h2{margin:.35rem 0 0;color:#fff;font-size:1rem;line-height:1.35}
.toc{padding:.65rem .55rem 1rem} .toc ul{list-style:none;margin:0;padding:0} .toc li{margin:2px 0} .toc a{display:block;color:var(--text);text-decoration:none;padding:.35rem .6rem;border-radius:7px;font-size:.87rem} .toc a:hover{background:var(--panel-hover)} .toc a.active{background:var(--panel-active);font-weight:600} .toc ul ul a{color:var(--muted);font-size:.82rem;padding-left:1.3rem}
.wrap{padding:2rem} .content{max-width:980px;margin:0 auto;background:var(--content-bg);border:1px solid #dde5f4;border-radius:16px;padding:2rem 2.3rem 2.5rem;box-shadow:0 12px 38px rgba(16,33,66,.08)}
.page-title{margin:0;color:var(--heading);font-size:2rem;letter-spacing:-.02em} .subtitle{margin:.45rem 0 1.3rem;color:#556380}
h1,h2,h3,h4{color:var(--heading);line-height:1.3;scroll-margin-top:1rem} h2{font-size:1.45rem;margin-top:2.2rem;border-bottom:2px solid #dbe7ff;padding-bottom:.36rem} h3{font-size:1.12rem;margin-top:1.5rem}
hr{border:none;border-top:1px solid var(--border);margin:1.5rem 0} ul,ol{padding-left:1.5rem}
code{font-family:Cascadia Code,Consolas,monospace;background:var(--inline);border:1px solid #e2eafb;border-radius:5px;padding:.1rem .32rem;font-size:.88em}
pre{background:var(--code);color:var(--code-text);border:1px solid #263450;border-radius:12px;padding:1rem 1.1rem;overflow-x:auto;font-size:.86rem;line-height:1.56} pre code{background:transparent;border:none;padding:0;color:inherit}
table{width:100%;border-collapse:collapse;margin:1rem 0;border:1px solid var(--border);font-size:.92rem} th,td{border:1px solid var(--border);padding:.56rem .7rem;text-align:left;vertical-align:top} th{background:#eaf1ff;color:#17345f} tr:nth-child(even){background:var(--stripe)}
blockquote{margin:1rem 0;padding:.75rem 1rem;border-left:4px solid #f2be34;background:#fff8e8}
@media (max-width:980px){.layout{grid-template-columns:1fr} .sidebar{position:static;height:auto;max-height:42vh} .wrap{padding:1rem} .content{padding:1.2rem 1rem 1.5rem}}
</style>
</head>
<body>
<div id=\"progress\"></div>
<div class=\"layout\"><nav class=\"sidebar\"><div class=\"sidebar-header\"><p>AgenticRAG</p><h2>Prompt Engineering for RAG</h2></div><div class=\"toc\">__TOC__</div></nav><main class=\"wrap\"><article class=\"content\"><h1 class=\"page-title\">Prompt Engineering for RAG</h1><p class=\"subtitle\">System prompts, few-shot examples, CoT, and intent-based routing.</p>__BODY__</article></main></div>
<script>
const p=document.getElementById('progress');window.addEventListener('scroll',()=>{const d=document.documentElement;const pct=(d.scrollTop/(d.scrollHeight-d.clientHeight))*100;p.style.width=Math.max(0,Math.min(100,pct))+'%';});
const links=[...document.querySelectorAll('.toc a')];const map=new Map(links.map(a=>[(a.getAttribute('href')||'').slice(1),a]));const targets=links.map(a=>document.getElementById((a.getAttribute('href')||'').slice(1))).filter(Boolean);const io=new IntersectionObserver((entries)=>{for(const e of entries){if(e.isIntersecting){links.forEach(l=>l.classList.remove('active'));const k=map.get(e.target.id);if(k)k.classList.add('active');break;}}},{rootMargin:'-30% 0px -60% 0px',threshold:.1});targets.forEach(t=>io.observe(t));
</script>
</body>
</html>
"""

html = template.replace("__TOC__", toc).replace("__BODY__", body)
out_path.write_text(html, encoding="utf-8")
print(out_path)
