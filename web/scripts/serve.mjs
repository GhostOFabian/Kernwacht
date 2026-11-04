// Minimaler statischer Server ohne Abhängigkeiten (für Vorschau des Produktionsbuilds und E2E-Tests).
import { createReadStream, existsSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { extname, join, normalize, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const TYPES = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.json': 'application/json', '.webmanifest': 'application/manifest+json', '.svg': 'image/svg+xml',
  '.png': 'image/png', '.map': 'application/json', '.ico': 'image/x-icon',
};

export function startServer(dir, port) {
  const base = resolve(dir);
  const server = createServer((req, res) => {
    const url = new URL(req.url ?? '/', 'http://localhost');
    let p = normalize(join(base, decodeURIComponent(url.pathname)));
    if (!p.startsWith(base)) {
      res.writeHead(403).end();
      return;
    }
    if (existsSync(p) && statSync(p).isDirectory()) p = join(p, 'index.html');
    if (!existsSync(p)) {
      res.writeHead(404, { 'content-type': 'text/plain' }).end('Nicht gefunden');
      return;
    }
    res.writeHead(200, { 'content-type': TYPES[extname(p)] ?? 'application/octet-stream', 'cache-control': 'no-cache' });
    createReadStream(p).pipe(res);
  });
  server.listen(port, () => console.log(`[serve] http://localhost:${port}/  (${base})`));
  return server;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  startServer(process.argv[2] ?? 'dist', Number(process.argv[3] ?? 4173));
}
