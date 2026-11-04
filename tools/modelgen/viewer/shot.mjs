// node shot.mjs outdir name1 name2 ...  (GLBs in out/glb/<cat>/<name>.glb gemäß manifest)
import { chromium } from '/home/claude/kernwacht/node_modules/playwright/index.mjs';
import http from 'node:http'; import fs from 'node:fs'; import path from 'node:path';
const ROOT = '/home/claude/darkmodels';
const srv = http.createServer((q, r) => { const p = path.join(ROOT, decodeURIComponent(q.url.split('?')[0])); fs.readFile(p, (e, d) => { if (e) { r.writeHead(404); r.end(); return; } r.writeHead(200, { 'Content-Type': p.endsWith('.html') ? 'text/html' : p.endsWith('.js') ? 'text/javascript' : 'application/octet-stream' }); r.end(d); }); }).listen(4477);
const [outDir, ...files] = process.argv.slice(2);
fs.mkdirSync(outDir, { recursive: true });
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const views = JSON.parse(process.env.VIEWS || '[["Idle",0,0.6],["Walk",0.3,1.6],["Attack",0.45,-0.5],["Death",1,0.9]]');
const page = await browser.newPage({ viewport: { width: 340 * views.length, height: 380 } });
for (const f of files) {
  await page.goto(`http://localhost:4477/viewer/check.html?m=/${f}&v=${encodeURIComponent(JSON.stringify(views))}`);
  await page.waitForFunction(() => window.__ready, null, { timeout: 120000 });
  const err = await page.evaluate(() => window.__err);
  if (err) console.log('FEHLER', f, err);
  await page.screenshot({ path: `${outDir}/${path.basename(f, '.glb')}.png` });
  console.log('ok', f);
}
await browser.close(); srv.close();
