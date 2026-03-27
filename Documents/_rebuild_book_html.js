const fs = require('fs');
const { execSync } = require('child_process');
const path = require('path');

const dir = __dirname;
const mdFile = path.join(dir, 'Azure-RAG-Complete-Book.md');
const htmlFile = path.join(dir, 'Azure-RAG-Complete-Book.html');
const tmpFile = path.join(dir, '_book_standalone.html');

// Step 1: Run pandoc to produce standalone HTML with TOC
console.log('Running pandoc...');
execSync(
  `pandoc "${mdFile}" -f markdown -t html --toc --toc-depth=3 -s -o "${tmpFile}"`,
  { stdio: 'inherit' }
);

// Step 2: Read pandoc output
const pandocHtml = fs.readFileSync(tmpFile, 'utf8');

// Step 3: Read existing HTML for CSS and JS
const existingHtml = fs.readFileSync(htmlFile, 'utf8');

// Extract CSS from existing HTML
const styleMatch = existingHtml.match(/<style>([\s\S]*?)<\/style>/);
const css = styleMatch ? styleMatch[1] : '';

// Extract JS from existing HTML
const scriptMatch = existingHtml.match(/<script>([\s\S]*?)<\/script>/);
const js = scriptMatch ? scriptMatch[1] : '';

// Step 4: Extract TOC and body from pandoc output
const tocMatch = pandocHtml.match(/<nav[^>]*id="TOC"[^>]*>([\s\S]*?)<\/nav>/);
const toc = tocMatch ? tocMatch[1] : '';

const bodyMatch = pandocHtml.match(/<body>([\s\S]*?)<\/body>/);
let bodyContent = bodyMatch ? bodyMatch[1] : '';

// Remove the TOC nav from body content (it's embedded)
bodyContent = bodyContent.replace(/<nav[^>]*id="TOC"[^>]*>[\s\S]*?<\/nav>/, '');

// Remove pandoc header if present
bodyContent = bodyContent.replace(/<header[^>]*>[\s\S]*?<\/header>/, '');

// Add chapter-break dividers before each h2
bodyContent = bodyContent.replace(/<h2/g, '<div class="chapter-break"></div>\n<h2');

// Step 5: Assemble final HTML
const finalHtml = `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Azure RAG — Complete Production Guide</title>
  <style>
${css}
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
      <div class="book-title">Azure RAG Book</div>
    </div>
    <div class="toc">
      ${toc}
    </div>
  </aside>

  <div class="content-wrap">
    <div class="content">
      ${bodyContent}
    </div>
  </div>
</div>

<button id="top-btn" aria-label="Back to top">&#8593;</button>

<script>
${js}
</script>
</body>
</html>`;

fs.writeFileSync(htmlFile, finalHtml, 'utf8');
console.log(`HTML written: ${htmlFile} (${finalHtml.split('\n').length} lines)`);

// Cleanup
fs.unlinkSync(tmpFile);
console.log('Done.');
