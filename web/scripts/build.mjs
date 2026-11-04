// Build-Skript (esbuild). Erzeugt einen statischen Produktionsbuild in dist/.
//   node scripts/build.mjs            → Produktionsbuild (minifiziert)
//   node scripts/build.mjs --dev      → unminifiziert mit Sourcemaps
//   node scripts/build.mjs --dev --serve → Entwicklungsserver mit Rebuild bei Änderungen
import * as esbuild from 'esbuild';
import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const dist = join(root, 'dist');
const dev = process.argv.includes('--dev');
const serve = process.argv.includes('--serve');
const pkg = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));

const options = {
  entryPoints: [join(root, 'src/main.ts')],
  bundle: true,
  format: 'esm',
  target: ['es2020', 'safari15', 'chrome90', 'firefox90'],
  outfile: join(dist, 'assets/main.js'),
  minify: !dev,
  sourcemap: dev ? 'inline' : true,
  legalComments: 'none',
  define: { __APP_VERSION__: JSON.stringify(pkg.version), __DEV__: String(dev) },
  logLevel: 'info',
};

function finalize() {
  const js = readFileSync(join(dist, 'assets/main.js'));
  const css = readFileSync(join(root, 'src/styles.css'));
  const hash = createHash('sha256').update(js).update(css).digest('hex').slice(0, 10);
  writeFileSync(join(dist, 'assets/styles.css'), css);
  cpSync(join(root, 'public'), dist, { recursive: true });
  const html = readFileSync(join(root, 'public/index.html'), 'utf8')
    .replaceAll('%HASH%', hash)
    .replaceAll('%VERSION%', pkg.version);
  writeFileSync(join(dist, 'index.html'), html);
  const sw = readFileSync(join(root, 'public/sw.js'), 'utf8').replaceAll('%HASH%', hash);
  writeFileSync(join(dist, 'sw.js'), sw);
  console.log(`[build] fertig – Version ${pkg.version}, Hash ${hash}${dev ? ' (dev)' : ''}`);
}

if (existsSync(dist)) rmSync(dist, { recursive: true, force: true });
mkdirSync(join(dist, 'assets'), { recursive: true });

if (serve) {
  const ctx = await esbuild.context({
    ...options,
    plugins: [{ name: 'finalize', setup(b) { b.onEnd((r) => { if (r.errors.length === 0) finalize(); }); } }],
  });
  await ctx.watch();
  const { startServer } = await import('./serve.mjs');
  startServer(dist, Number(process.env.PORT ?? 5173));
} else {
  await esbuild.build(options);
  finalize();
}
