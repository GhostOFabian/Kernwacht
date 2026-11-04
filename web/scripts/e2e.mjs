// End-to-End-Test des PRODUKTIONSBUILDS (dist/) mit Playwright/Chromium.
// Prüft Desktop (Maus/Tastatur) und Smartphone (Touch, Quer-/Hochformat).
//   npm run build && npm run e2e         Screenshots: test-results/ (oder E2E_OUT)
import assert from 'node:assert/strict';
import { existsSync, mkdirSync } from 'node:fs';
import { chromium } from 'playwright';
import { startServer } from './serve.mjs';

const OUT = process.env.E2E_OUT ?? 'test-results';
mkdirSync(OUT, { recursive: true });
if (!existsSync('dist/index.html')) throw new Error('dist/ fehlt – zuerst "npm run build" ausführen.');
const PORT = 4321;
const server = startServer('dist', PORT);
const URL = `http://localhost:${PORT}/`;
const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const results = [];
let failed = 0;

async function step(name, fn) {
  const t = Date.now();
  try {
    await fn();
    results.push(`✔ ${name} (${Date.now() - t} ms)`);
    if (process.env.E2E_DEBUG) console.log(`   [dbg] Konsolenmeldungen ${globalThis.__msgCount ?? 0}, RSS ${process.memoryUsage().rss >> 20} MB`);
    console.log(results[results.length - 1]);
  } catch (err) {
    failed++;
    results.push(`✖ ${name}: ${err instanceof Error ? err.message.split('\n').slice(0, 4).join(' ') : err}`);
    console.log(results[results.length - 1]);
  }
}

async function newPage(opts) {
  const ctx = await browser.newContext(opts);
  const page = await ctx.newPage();
  // Turmlimit (3.3) für die vielen Bau-Schritte abschalten; Schritt 3.3 prüft es gezielt
  await page.addInitScript(() => { window.__KW_E2E_NOLIMIT = true; });
  const errors = [];
  page.on('pageerror', (e) => errors.push(e.message));
  page.on('console', (m) => {
    globalThis.__msgCount = (globalThis.__msgCount ?? 0) + 1;
    if (m.type() !== 'error') return;
    // Externe Schriftarten sind in der Testumgebung nicht erreichbar – das ist kein App-Fehler.
    const url = m.location()?.url ?? '';
    if (/Failed to load resource/.test(m.text()) && (!url || !url.startsWith(URL))) return;
    errors.push(m.text());
  });
  await page.goto(URL);
  await page.waitForSelector('[data-testid=menu]');
  // Software-Rendering (SwiftShader) ist langsam: niedrige Qualitätsstufe für flüssige Tests.
  await page.evaluate(() => { const app = window.__kernwacht; app.save.settings.quality = 'low'; app.applySettings(); app.resize(); });
  return { ctx, page, errors };
}

/** Bildschirmkoordinaten (Seite) der Mitte einer Zelle. */
async function cellPoint(page, x, y) {
  return page.evaluate(([cx, cy]) => {
    const app = window.__kernwacht;
    const m = app.session.sim.s.map;
    const top = [1, 3, 4].includes(m.cells[cy * m.width + cx]) ? -0.04 : m.heights[cy * m.width + cx] * 0.24;
    const p = app.view.project(cx + 0.5, top, cy + 0.5);
    const r = app.canvas.getBoundingClientRect();
    return { x: r.left + p.x, y: r.top + p.y };
  }, [x, y]);
}

/** Sucht eine freie, sichtbare, bebaubare Zelle nahe der Bildmitte. */
async function findBuildCell(page, def = 'ranger') {
  return page.evaluate((d) => {
    const app = window.__kernwacht;
    const sim = app.session.sim;
    const m = sim.s.map;
    const r = app.canvas.getBoundingClientRect();
    let best = null;
    let bd = 1e9;
    for (let y = 1; y < m.height - 1; y++) for (let x = 1; x < m.width - 1; x++) {
      if (!sim.canBuild(d, x, y).ok) continue;
      const p = app.view.project(x + 0.5, m.heights[y * m.width + x] * 0.24, y + 0.5);
      if (p.x < 60 || p.y < 60 || p.x > r.width - 60 || p.y > r.height - 60) continue;
      const pick = app.view.pickCell(p.x, p.y, m);
      if (!pick || pick.x !== x || pick.y !== y) continue;
      const dd = (p.x - r.width / 2) ** 2 + (p.y - r.height / 2) ** 2;
      if (dd < bd) { bd = dd; best = { x, y }; }
    }
    return best;
  }, def);
}

const state = (page) => page.evaluate(() => {
  const s = window.__kernwacht.session?.sim.s;
  return s ? { gold: s.gold, lives: s.lives, wave: s.wave, phase: s.phase, towers: s.towers.length, tick: s.tick, paused: s.paused, speed: s.speed, mods: s.modifiers.length } : null;
});

/** Simulation schnell vorspulen (Testhilfe – nutzt die normalen Simulationsschritte). */
const fastForward = (page, until, max = 30 * 600) => page.evaluate(([u, mx]) => {
  const sim = window.__kernwacht.session.sim;
  for (let i = 0; i < mx; i++) {
    if (u === 'build' && sim.s.phase !== 'combat') break;
    if (u === 'over' && sim.isOver()) break;
    sim.step();
  }
  return sim.s.phase;
}, [until, max]);

// ------------------------------------------------------------------ Desktop
// E2E_FRESH_ONLY=1 überspringt die lange Desktop- und die Smartphone-Sitzung (schneller Teil-Lauf).
if (!process.env.E2E_FRESH_ONLY) {
  const { ctx, page, errors } = await newPage({ viewport: { width: 1280, height: 760 } });
  await page.evaluate(() => { window.__kernwacht.save.settings.tutorialSeen = true; });

  await step('Hauptmenü lädt ohne Fehler', async () => {
    await page.screenshot({ path: `${OUT}/desktop-01-menu.png` });
    assert.equal(errors.length, 0, errors.join(' | '));
  });

  await step('3.9: Hauptmenü bietet nur noch Helden-Endlos', async () => {
    for (const id of ['play', 'campaign', 'endless', 'daily']) assert.ok((await page.$(`[data-testid=${id}]`)) === null, `${id} entfernt`);
    assert.ok((await page.$('[data-testid=editor]')) !== null, '3.10: Karteneditor wieder da');
    assert.ok((await page.$('[data-testid=hero-endless]')) !== null, 'Helden-Endlos vorhanden');
  });

  // ---------------------------------------------------------------- Helden-Endlos
  const he = (fn, arg) => page.evaluate(fn, arg);
  await step('Helden-Endlos: Startbildschirm, Heldenwahl, Lauf startet mit Countdown und Heldenleiste', async () => {
    await page.evaluate(() => window.__kernwacht.go('menu'));
    await page.click('[data-testid=hero-endless]');
    await page.waitForSelector('[data-testid=champ-picks]');
    assert.equal(await page.isDisabled('[data-testid=champ-vela]'), true, 'Vela ist anfangs gesperrt');
    await page.click('[data-testid=champ-brann]');
    await page.screenshot({ path: `${OUT}/desktop-15-hero-setup.png` });
    await page.click('[data-testid=start-hero]');
    await page.waitForSelector('[data-testid=champion]');
    const info = await he(() => { const s = window.__kernwacht.session.sim.s; return { mode: s.mode, champ: s.he.champion.id, timer: s.he.autoTimer, heroes: s.heroes.length }; });
    assert.equal(info.mode, 'heroEndless');
    assert.equal(info.champ, 'brann');
    assert.equal(info.heroes, 0);
    assert.ok(info.timer > 10 && info.timer <= 15, `Countdown ${info.timer}`);
    assert.match(await page.textContent('[data-testid=start-wave]'), /Welle 1 · \d+ s/);
    assert.equal((await page.$$('[data-testid^=skill-]')).length, 4);
    assert.equal((await page.$$('[data-testid^=res-]')).length, 4);
    await page.screenshot({ path: `${OUT}/desktop-16-hero-endless.png` });
  });

  await step('Helden-Endlos: Rechtsklick bewegt den Helden über die Lane (Pathfinding)', async () => {
    const target = await he(() => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      const m = sim.s.map;
      const c = sim.s.he.champion;
      const r = app.canvas.getBoundingClientRect();
      let best = null;
      for (let y = 1; y < m.height - 1; y++) for (let x = 1; x < m.width - 1; x++) {
        if (m.cells[y * m.width + x] !== 1) continue;
        const d = Math.hypot(x + 0.5 - c.x, y + 0.5 - c.y);
        if (d < 3 || d > 7) continue;
        const p = app.view.project(x + 0.5, -0.04, y + 0.5);
        if (p.x < 80 || p.y < 80 || p.x > r.width - 80 || p.y > r.height - 80) continue;
        // Nicht unter HUD-Elementen (Heldenleiste, Minikarte …) klicken
        if (document.elementFromPoint(r.left + p.x, r.top + p.y) !== app.canvas) continue;
        const pick = app.view.pickCell(p.x, p.y, m);
        if (pick && pick.x === x && pick.y === y) { best = { x, y }; break; }
      }
      return best;
    });
    assert.ok(target, 'sichtbare Lane-Zelle gefunden');
    const p = await cellPoint(page, target.x, target.y);
    await page.mouse.click(p.x, p.y, { button: 'right' });
    const len = await he(() => window.__kernwacht.session.sim.s.he.champion.path.length);
    assert.ok(len > 0, 'Weg berechnet');
    const pos = await he(() => { const sim = window.__kernwacht.session.sim; for (let i = 0; i < 300; i++) sim.step(); const c = sim.s.he.champion; return { x: Math.floor(c.x), y: Math.floor(c.y) }; });
    assert.deepEqual(pos, target);
  });

  await step('Helden-Endlos: Fähigkeiten per Taste (selbst) und mit Zielwahl (Klick)', async () => {
    await page.keyboard.press('a');
    const cd0 = await he(() => window.__kernwacht.session.sim.s.he.champion.skillCd[0]);
    assert.ok(cd0 > 0, 'Wirbel ausgelöst');
    // Erfahrung als echte Beute am Helden ablegen → Stufenaufstieg schaltet Sturmangriff (Stufe 2) frei
    await he(() => {
      const sim = window.__kernwacht.session.sim;
      const c = sim.s.he.champion;
      sim.s.he.pickups.push({ uid: sim.uid(), kind: 'xp', x: c.x, y: c.y, amount: 400, ref: '', t: 30 });
      sim.step();
      c.mana = 999;
    });
    assert.ok(await he(() => window.__kernwacht.session.sim.s.he.champion.level) >= 2, 'Stufenaufstieg durch EP-Beute');
    await page.keyboard.press('s');
    await page.waitForFunction(() => window.__kernwacht.session.targeting >= 0, null, { timeout: 5000 });
    const cell = await he(() => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      const c = sim.s.he.champion;
      const m = sim.s.map;
      for (const [dx, dy] of [[2, 0], [-2, 0], [0, 2], [0, -2], [2, 2], [-2, -2]]) {
        const x = Math.floor(c.x) + dx; const y = Math.floor(c.y) + dy;
        if (x < 0 || y < 0 || x >= m.width || y >= m.height || m.cells[y * m.width + x] === 2 || (sim.s.he.shop.x === x && sim.s.he.shop.y === y)) continue;
        return { x, y };
      }
      return null;
    });
    const cp = await cellPoint(page, cell.x, cell.y);
    await page.mouse.click(cp.x, cp.y);
    const cd1 = await he(() => window.__kernwacht.session.sim.s.he.champion.skillCd[1]);
    const dbg1 = cd1 > 0 ? null : await he((pt) => {
      const app = window.__kernwacht; const s = app.session; const r = app.canvas.getBoundingClientRect(); const c = s.sim.s.he.champion;
      const el = document.elementFromPoint(pt.x, pt.y);
      return { tgt: s.targeting, lvl: c.level, mana: c.mana, champ: [c.x, c.y], cd: c.skillCd, pick: app.view.pickCell(pt.x - r.left, pt.y - r.top, s.sim.s.map), el: el ? el.className || el.tagName : null, modal: s.hud.hasModal(), paused: s.sim.s.paused, keys: app.save.settings.keys };
    }, cp);
    assert.ok(cd1 > 0, `Sturmangriff mit Zielwahl ausgelöst ${JSON.stringify({ cell, cp, dbg1 })}`);
  });

  await step('Helden-Endlos: Laden in Reichweite, Kauf mit Ressourcenprüfung, Trank per Umschalt+1', async () => {
    await he(() => {
      const sim = window.__kernwacht.session.sim;
      const hs = sim.s.he;
      sim.s.gold = 30;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
        const x = hs.shop.x + dx; const y = hs.shop.y + dy;
        if (sim.s.map.cells[y * sim.s.map.width + x] !== 2) { hs.champion.x = x + 0.5; hs.champion.y = y + 0.5; hs.champion.hx = hs.champion.x; hs.champion.hy = hs.champion.y; hs.champion.path = []; break; }
      }
    });
    await page.keyboard.press('x');
    await page.waitForSelector('[data-testid=he-window]:not(.hidden)');
    await page.click('[data-testid=tab-potions]');
    assert.equal(await page.isDisabled('[data-testid=buy-p-heal]'), true, 'zu wenig Gold');
    await he(() => { window.__kernwacht.session.sim.s.gold = 500; });
    await page.waitForFunction(() => !document.querySelector('[data-testid=buy-p-heal]')?.disabled);
    await page.click('[data-testid=buy-p-heal]');
    await page.screenshot({ path: `${OUT}/desktop-17-shop.png` });
    const st = await he(() => { const s = window.__kernwacht.session.sim.s; return { gold: s.gold, heal: s.he.champion.potions.find((p) => p.potion === 'heal')?.count ?? 0 }; });
    assert.equal(st.gold, 460);
    assert.equal(st.heal, 1);
    await page.keyboard.press('Escape');
    await he(() => { window.__kernwacht.session.sim.s.he.champion.hp = 50; });
    await page.keyboard.press('Shift+Digit1');
    const after = await he(() => { const c = window.__kernwacht.session.sim.s.he.champion; return { hp: c.hp, n: c.potions.length }; });
    assert.ok(after.hp > 50, 'Heiltrank wirkt');
    assert.equal(after.n, 0, 'Stapel verbraucht');
  });

  await step('Helden-Endlos: Schmiede stellt Item aus Fragmenten her, Anlegen und Skillpunkt im Charakterbogen', async () => {
    await he(() => { const hs = window.__kernwacht.session.sim.s.he; hs.fragments.leather = 3; hs.fragmentDelta.leather = 3; });
    await page.keyboard.press('x');
    await page.waitForSelector('[data-testid=he-window]:not(.hidden)');
    await page.click('[data-testid=tab-forge]');
    await page.click('[data-testid="craft-frag:leather"]');
    assert.ok(await he(() => window.__kernwacht.session.sim.s.he.champion.inventory.some((i) => i.item === 'leather')));
    await page.keyboard.press('Tab');
    await page.waitForSelector('[data-testid=tab-gear]');
    await page.click('[data-testid=tab-gear]');
    await page.click('[data-testid=item-leather]');
    await page.click('[data-testid=equip]');
    assert.ok(await he(() => { const c = window.__kernwacht.session.sim.s.he.champion; return c.equipment[1] !== null; }), 'Rüstung angelegt');
    await page.click('[data-testid=tab-tree]');
    const pts = await he(() => window.__kernwacht.session.sim.s.he.champion.skillPoints);
    assert.ok(pts > 0, 'Skillpunkte durch Stufenaufstieg');
    await page.click('.node.can');
    assert.equal(await he(() => window.__kernwacht.session.sim.s.he.champion.skillPoints), pts - 1);
    await page.screenshot({ path: `${OUT}/desktop-18-sheet.png` });
    await page.keyboard.press('Escape');
  });

  await step('Helden-Endlos: Welle startet automatisch, Pause hält Countdown und Simulation an', async () => {
    await page.keyboard.press('p');
    await page.waitForSelector('[data-testid=pause-menu]');
    const t0 = await he(() => { const s = window.__kernwacht.session.sim.s; return { tick: s.tick, timer: s.he.autoTimer }; });
    await page.waitForTimeout(800);
    const t1 = await he(() => { const s = window.__kernwacht.session.sim.s; return { tick: s.tick, timer: s.he.autoTimer }; });
    assert.deepEqual(t1, t0, 'pausiert');
    await page.keyboard.press('p');
    await he(() => { window.__kernwacht.session.sim.s.he.autoTimer = 0.05; });
    await page.waitForFunction(() => window.__kernwacht.session.sim.s.phase === 'combat', null, { timeout: 15000 });
    assert.equal(await he(() => window.__kernwacht.session.sim.s.wave), 1);
  });

  await step('Turm über das ganze Modell anklickbar, Turmübersicht (O) mit Aufwerten, Drehsperre', async () => {
    const t = await he(() => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      sim.s.gold = 5000;
      for (const e of sim.s.enemies) { e.alive = false; }
      sim.s.enemies = [];
      sim.s.spawnQueue = [];
      // Mini-Pause: Welt steht still, Klicks bleiben möglich (keine Gegner laufen ins Klickziel).
      // Erst einen sauberen Zustand herstellen (ein verspätet verarbeitetes „P“ des Vorschritts darf kein Pausenmenü offen lassen).
      app.session.hv.miniPaused = false;
      if (sim.s.paused) sim.setPaused(false);
      app.session.miniPause();
      const m = sim.s.map;
      const r = app.canvas.getBoundingClientRect();
      for (let y = 2; y < m.height - 2; y++) for (let x = 2; x < m.width - 2; x++) {
        if (!sim.canBuild('ranger', x, y).ok) continue;
        const p = app.view.project(x + 0.5, 1.2, y + 0.5);
        if (p.x < 120 || p.y < 120 || p.x > r.width - 120 || p.y > r.height - 200) continue;
        sim.build('ranger', x, y);
        const tw = sim.towerAt(x, y);
        return { uid: tw.uid, x, y };
      }
      return null;
    });
    assert.ok(t, 'Bauplatz gefunden');
    await page.waitForFunction(() => !window.__kernwacht.session.hud.hasModal(), null, { timeout: 5000 });
    await page.waitForTimeout(400);
    // Klick auf die Turmspitze (deutlich über dem Bodenfeld)
    const top = await he(([x, y]) => {
      const app = window.__kernwacht;
      // Je nach Testtempo kann gerade eine Modifikatorauswahl offen sein – überspringen.
      if (app.session.sim.s.phase === 'choice') app.session.sim.chooseModifier(null);
      const m = app.session.sim.s.map;
      const base = m.heights[y * m.width + x] * 0.24;
      const p = app.view.project(x + 0.5, base + 1.15, y + 0.5);
      const r = app.canvas.getBoundingClientRect();
      return { x: r.left + p.x, y: r.top + p.y };
    }, [t.x, t.y]);
    await page.mouse.click(top.x, top.y);
    const dbg = await he((pt) => {
      const app = window.__kernwacht; const r = app.canvas.getBoundingClientRect(); const s = app.session;
      const el = document.elementFromPoint(pt.x, pt.y);
      return { pickT: app.view.pickTower(pt.x - r.left, pt.y - r.top, s.sim), pickE: app.view.pickEnemy(pt.x - r.left, pt.y - r.top, s.sim), enemy: s.hv.selectedEnemy, el: el ? el.className || el.tagName : null, paused: s.sim.s.paused, targeting: s.targeting, modal: s.hud.hasModal(), kind: s.hud.modalKind, mini: s.hv.miniPaused, phase: s.sim.s.phase };
    }, top);
    assert.equal(await he(() => window.__kernwacht.session.ui.selectedTower), t.uid, `Turm über die Spitze ausgewählt ${JSON.stringify(dbg)}`);
    await page.keyboard.press('Escape');
    await page.keyboard.press('o');
    await page.waitForSelector('[data-testid=tower-list]:not(.hidden)');
    await page.click(`[data-testid=tl-up-${t.uid}]`);
    assert.equal(await he((uid) => window.__kernwacht.session.sim.c.towerByUid.get(uid).level, t.uid), 2, 'in der Übersicht aufgewertet');
    await page.screenshot({ path: `${OUT}/desktop-20-tower-list.png` });
    await page.keyboard.press('o');
    await page.click('[data-testid=lock-rotation]');
    const rot0 = await he(() => window.__kernwacht.view.cam.rot);
    await page.keyboard.press('q');
    assert.equal(await he(() => window.__kernwacht.view.cam.rot), rot0, 'Drehung gesperrt');
    assert.equal(await he(() => window.__kernwacht.save.settings.lockRotation), true);
    await page.click('[data-testid=lock-rotation]');
    await page.keyboard.press('q');
    assert.equal(await he(() => window.__kernwacht.view.cam.rot), rot0 - 1, 'Drehung wieder frei');
    await he(() => window.__kernwacht.session.miniPause());
  });

  await step('Gegner anklicken zeigt Live-Werte; nächste Welle per Knopf vorzeitig rufen', async () => {
    const w0 = await he(() => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      if (sim.s.phase === 'choice') sim.chooseModifier(null);
      if (sim.s.phase !== 'combat' || !sim.s.enemies.some((x) => x.alive) && sim.s.spawnQueue.length === 0) sim.nextWave();
      if (sim.s.phase === 'choice') { sim.chooseModifier(null); sim.nextWave(); }
      sim.s.waveTime = 9999;
      sim.step();
      return sim.s.wave;
    });
    const pos = await he(() => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      const e = sim.s.enemies.find((x) => x.alive && !x.stealthed);
      for (const x of sim.s.enemies) x.speed = 0;
      e.maxHp = e.hp = 1e7; // darf während des Tests nicht von Türmen getötet werden
      app.view.cam.tx = e.x; app.view.cam.tz = e.y; app.view.cam.clamp();
      return { uid: e.uid };
    });
    let pt = null;
    for (let attempt = 0; attempt < 6 && !pt; attempt++) {
    await page.waitForTimeout(900);
    pt = await he(() => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      const r = app.canvas.getBoundingClientRect();
      // Einen Gegner wählen, der sicher im freien Bildbereich liegt und vom Klicktest erkannt wird
      for (const e of sim.s.enemies) {
        if (!e.alive || e.stealthed) continue;
        const m = sim.s.map;
        const c = m.cells[Math.floor(e.y) * m.width + Math.floor(e.x)];
        const base = [1, 3, 4].includes(c) ? -0.04 : m.heights[Math.floor(e.y) * m.width + Math.floor(e.x)] * 0.24;
        const p = app.view.project(e.x, base + (e.flying ? 1.1 : 0) + 0.4, e.y);
        if (p.x < 380 || p.y < 120 || p.x > r.width - 120 || p.y > r.height - 220) continue;
        const hit = app.view.pickEnemy(p.x, p.y, sim);
        if (!hit) continue;
        const he2 = sim.c.enemyByUid.get(hit);
        he2.maxHp = he2.hp = 1e7;
        return { x: r.left + p.x, y: r.top + p.y };
      }
      // Keiner gefunden: Kamera auf den nächsten Gegner richten und erneut versuchen
      const alive = sim.s.enemies.filter((x) => x.alive && !x.stealthed);
      const next = alive[Math.floor(Math.random() * alive.length)];
      if (next) { app.view.cam.tx = next.x; app.view.cam.tz = next.y; app.view.cam.clamp(); }
      return null;
    });
    }
    assert.ok(pt, 'anklickbarer Gegner im Bild');
    await page.mouse.click(pt.x, pt.y);
    await page.waitForSelector('[data-testid=enemy-hp]');
    assert.match(await page.textContent('[data-testid=enemy-hp]'), /LP/);
    await page.screenshot({ path: `${OUT}/desktop-21-enemy-info.png` });
    await page.waitForFunction(() => !document.querySelector('[data-testid=start-wave]').disabled);
    await page.click('[data-testid=start-wave]');
    assert.equal(await he(() => window.__kernwacht.session.sim.s.wave), w0 + 1, 'nächste Welle vorzeitig gestartet');
  });

  await step('Helden-Endlos: Laufende freigeschaltete Items werden beim Ende dauerhaft gespeichert', async () => {
    await he(() => { const sim = window.__kernwacht.session.sim; sim.s.stats.wavesCleared = 6; sim.abandon(); });
    await page.waitForSelector('[data-testid=hero-result]', { timeout: 20000 });
    await page.screenshot({ path: `${OUT}/desktop-19-hero-result.png` });
    await page.reload();
    await page.waitForSelector('[data-testid=menu]');
    const meta = await he(() => window.__kernwacht.save.progress.heroMeta);
    assert.ok(meta.unlockedItems.includes('leather'), 'Lederwams in der Sammlung');
    assert.equal(meta.bestWave, 6);
    assert.ok(meta.claimedMilestones.includes(5));
    assert.equal(meta.unlockedItems.filter((i) => i === 'leather').length, 1, 'kein Duplikat');
  });

  await step('Passiver Baum, Turmfreischaltung, Mini-Pause mit Aufwerten, Held festsetzen', async () => {
    await he(() => { const app = window.__kernwacht; app.save.progress.heroMeta.bestWave = 25; app.go('heroSetup'); });
    await page.waitForSelector('[data-testid=passive-tree]');
    assert.match(await page.textContent('[data-testid=passive-points]'), /2 frei/);
    await page.click('[data-testid=pnode-p_vigor]');
    await page.click('[data-testid=pnode-p_might]');
    assert.match(await page.textContent('[data-testid=passive-points]'), /0 frei/);
    assert.equal(await page.isDisabled('[data-testid=pnode-p_learn]'), true, 'keine Punkte mehr');
    await page.screenshot({ path: `${OUT}/desktop-22-passive.png` });
    await page.click('[data-testid=start-hero]');
    await page.waitForSelector('[data-testid=champion]');
    const pas = await he(() => ({ meta: window.__kernwacht.save.progress.heroMeta.passive, run: window.__kernwacht.session.sim.s.he.passive }));
    assert.deepEqual(pas.run, { p_vigor: 1, p_might: 1 });
    assert.deepEqual(pas.meta, { p_vigor: 1, p_might: 1 });
    assert.ok(await page.$eval('[data-testid=tower-storm]', (b) => b.classList.contains('locked')), 'Sturmrufer gesperrt bis Welle 3');
    assert.ok(!(await page.$eval('[data-testid=tower-ranger]', (b) => b.classList.contains('locked'))));
    // Turm bauen, Welle starten, Mini-Pause
    const t = await he(() => {
      const sim = window.__kernwacht.session.sim;
      sim.s.gold = 2000;
      const m = sim.s.map;
      for (let y = 1; y < m.height - 1; y++) for (let x = 1; x < m.width - 1; x++) if (sim.build('ranger', x, y).ok) return sim.towerAt(x, y).uid;
      return 0;
    });
    await page.keyboard.press('Enter');
    await page.waitForFunction(() => window.__kernwacht.session.sim.s.phase === 'combat');
    await page.click('[data-testid=mini-pause]');
    await page.waitForSelector('[data-testid=mini-pause-badge]:not(.hidden)');
    assert.equal(!!(await page.$('[data-testid=pause-menu]')), false, 'kein Pausenmenü');
    const tick = await he(() => window.__kernwacht.session.sim.s.tick);
    await page.keyboard.press('o');
    await page.click(`[data-testid=tl-up-${t}]`);
    assert.equal(await he((u) => window.__kernwacht.session.sim.c.towerByUid.get(u).level, t), 2, 'Aufwerten in der Mini-Pause');
    assert.equal(await he(() => window.__kernwacht.session.sim.s.tick), tick, 'Simulation steht');
    await page.keyboard.press('o');
    await he((u) => window.__kernwacht.session.selectTower(u), t);
    await page.waitForSelector('[data-testid=sell]');
    assert.equal(await page.isDisabled('[data-testid=sell]'), true, 'kein Verkauf in der Mini-Pause');
    await he((u) => window.__kernwacht.session.sell(u), t);
    assert.ok(await he((u) => !!window.__kernwacht.session.sim.c.towerByUid.get(u), t), 'Turm bleibt stehen');
    await page.keyboard.press('Escape');
    await page.click('[data-testid=mini-pause]');
    await page.waitForFunction((tk) => window.__kernwacht.session.sim.s.tick > tk, tick, { timeout: 20000 });
    // Held festsetzen
    await page.click('[data-testid=hold-position]');
    assert.equal(await he(() => window.__kernwacht.session.sim.s.he.champion.holdPos), true);
    const before = await he(() => { const c = window.__kernwacht.session.sim.s.he.champion; return [c.x, c.y]; });
    const box = await page.$eval('canvas.game-canvas', (c) => { const r = c.getBoundingClientRect(); return { x: r.left + r.width * 0.5, y: r.top + r.height * 0.4 }; });
    await page.mouse.click(box.x, box.y, { button: 'right' });
    assert.equal(await he(() => window.__kernwacht.session.sim.s.he.champion.path.length), 0, 'kein Laufweg');
    const after = await he(() => { const c = window.__kernwacht.session.sim.s.he.champion; return [c.x, c.y]; });
    assert.deepEqual(after, before);
    await page.click('[data-testid=hold-position]');
    assert.equal(await he(() => window.__kernwacht.session.sim.s.he.champion.holdPos), false);
  });

  await step('2.5: Kampagnenheld spielen, Hindernis abbauen, Fusionshinweis vor dem Bau', async () => {
    await he(() => window.__kernwacht.go('heroSetup'));
    await page.waitForSelector('[data-testid=champ-picks]');
    assert.ok((await page.$$('[data-testid^=champ-]')).length >= 12, 'zwölf Helden zur Wahl');
    assert.equal(await page.isDisabled('[data-testid=champ-kaela]'), false, 'Kaela frei');
    await page.click('[data-testid=champ-kaela]');
    await page.screenshot({ path: `${OUT}/desktop-23-heroes-25.png`, fullPage: true });
    await page.click('[data-testid=start-hero]');
    await page.waitForSelector('[data-testid=champion]');
    assert.equal(await he(() => window.__kernwacht.session.sim.s.he.champion.id), 'kaela');
    // Abbau über das Hindernis-Panel
    const idx = await he(() => {
      const ses = window.__kernwacht.session;
      const sim = ses.sim;
      const m = sim.s.map;
      const c = sim.s.he.champion;
      for (let i = 0; i < m.cells.length; i++) {
        const x = i % m.width, y = Math.floor(i / m.width);
        if (m.cells[i] !== 2 || (x === sim.s.he.shop.x && y === sim.s.he.shop.y)) continue;
        let kind = null;
        for (let k = 0; k < m.decor.length; k += 3) if (m.decor[k] === i) kind = m.decor[k + 1];
        if (kind === null || kind === 3 || kind === 7) continue;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
          const n = (y + dy) * m.width + x + dx;
          if (m.cells[n] === 0 && !sim.towerAt(x + dx, y + dy)) {
            c.x = c.hx = x + dx + 0.5; c.y = c.hy = y + dy + 0.5; c.path = [];
            ses.hv.selectedObstacle = i;
            return i;
          }
        }
      }
      return -1;
    });
    assert.ok(idx >= 0, 'Hindernis gefunden');
    await page.waitForSelector('[data-testid=mine]');
    await page.click('[data-testid=mine]');
    const st = await he(() => window.__kernwacht.session.sim.s.he.mining);
    assert.ok(st && st.total >= 25 && st.total <= 60, 'Abbau läuft eine Wellenlänge');
    await page.screenshot({ path: `${OUT}/desktop-24-mining.png` });
    await he((i) => { const sim = window.__kernwacht.session.sim; sim.s.he.mining.t = 0.05; }, idx);
    await page.waitForFunction((i) => window.__kernwacht.session.sim.s.map.cells[i] === 0, idx);
    // Fusionshinweis vor dem Bau
    await he(() => {
      const ses = window.__kernwacht.session;
      const sim = ses.sim;
      sim.s.gold = 5000;
      const m = sim.s.map;
      for (let y = 2; y < m.height - 2; y++) for (let x = 2; x < m.width - 3; x++) {
        if (m.cells[y * m.width + x] === 0 && m.cells[y * m.width + x + 1] === 0 && sim.build('spore', x, y).ok) return;
      }
    });
    await page.keyboard.press('Escape');
    await he(() => window.__kernwacht.session.selectBuild('ranger'));
    await page.waitForSelector('[data-testid=fusion-hint]');
    assert.match(await page.textContent('[data-testid=fusion-hint]'), /Dornenhain/);
    await page.keyboard.press('Escape');
  });

  await step('2.5: Tastenbelegung ändern, Konflikt wird getauscht, Standard wiederherstellen', async () => {
    await he(() => window.__kernwacht.go('settings'));
    await page.waitForSelector('[data-testid=key-grid]');
    await page.click('[data-testid=key-grid]');
    await page.keyboard.press('F7');
    assert.deepEqual(await he(() => window.__kernwacht.save.settings.keys), { grid: 'f7' });
    assert.equal((await page.textContent('[data-testid=key-grid]')).trim(), 'F7');
    // Belegte Taste: beide Aktionen tauschen
    await page.click('[data-testid=key-speed]');
    await page.keyboard.press('q');
    assert.deepEqual(await he(() => window.__kernwacht.save.settings.keys), { grid: 'f7', speed: 'q', rotL: 'f' });
    await page.screenshot({ path: `${OUT}/desktop-25-keys.png`, fullPage: true });
    await page.click('[data-testid=keys-reset]');
    assert.deepEqual(await he(() => window.__kernwacht.save.settings.keys), {});
  });

  await step('Einstellungen werden gespeichert und nach Neuladen übernommen', async () => {
    await page.evaluate(() => window.__kernwacht.go('menu'));
    await page.click('[data-testid=settings]');
    await page.check('text=Stumm');
    await page.reload();
    await page.waitForSelector('[data-testid=menu]');
    const muted = await page.evaluate(() => window.__kernwacht.save.settings.muted);
    assert.equal(muted, true);
    const records = await page.evaluate(() => window.__kernwacht.save.progress.bestScores.length);
    assert.ok(records >= 1, 'Bestwert der beendeten Partie gespeichert');
  });

  await step('Hilfe & Kodex und Bestwerte öffnen', async () => {
    await page.click('[data-testid=help]');
    await page.click('[data-tab=towers]');
    assert.ok((await page.$$('.codex-entry')).length >= 12);
    await page.screenshot({ path: `${OUT}/desktop-13-help.png` });
    await page.click('[data-tab=heroEndless]');
    assert.ok((await page.$('[data-tab=heroes]')) === null, '3.9: kein Reiter für Lane-Helden');
    await page.click('[data-tab=legend]');
    assert.ok((await page.$$('.codex-entry')).length >= 4, 'Fusionen im Kodex');
    await page.click('[data-testid=back]');
    await page.click('[data-testid=records]');
    await page.waitForSelector('[data-testid=achievements]');
    assert.ok((await page.$$('.ach')).length >= 12);
    await page.screenshot({ path: `${OUT}/desktop-14-records.png`, fullPage: true });
    await page.click('[data-testid=back]');
    await page.click('[data-testid=settings]');
    await page.waitForSelector('[data-testid=skin-standard]');
    assert.equal(await page.isDisabled('[data-testid=skin-standard]'), false);
  });

  await step('Keine Laufzeitfehler auf dem Desktop', async () => {
    assert.equal(errors.length, 0, errors.join(' | '));
  });
  await ctx.close();
}

// ------------------------------------------------------------------ Desktop, frische Seite (2.6/2.7)
// Eigener Browser-Kontext: die lange Desktop-Sitzung oben sammelt viel Zustand an.
{
  const { ctx, page, errors } = await newPage({ viewport: { width: 1280, height: 760 } });
  const he = (fn, arg) => page.evaluate(fn, arg);
  await page.evaluate(() => { const app = window.__kernwacht; app.save.settings.tutorialSeen = true; app.save.progress.heroMeta.bestWave = 25; app.persist(); });

  await step('2.6: Doppelklick wählt alle Türme einer Sorte, gemeinsam aufwerten, Alles aufwerten, Turmliste scrollt', async () => {
    await he(() => window.__kernwacht.go('heroSetup'));
    await page.waitForSelector('[data-testid=start-hero]');
    await page.click('[data-testid=start-hero]');
    await page.waitForSelector('[data-testid=champion]');
    const t = await he(() => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      sim.s.gold = 100000;
      if (!sim.s.paused) app.session.miniPause();
      const m = sim.s.map;
      const r = app.canvas.getBoundingClientRect();
      const out = [];
      for (let y = 2; y < m.height - 2; y++) for (let x = 2; x < m.width - 2; x++) {
        if (!sim.canBuild('ranger', x, y).ok) continue;
        const p = app.view.project(x + 0.5, 1.2, y + 0.5);
        if (out.length === 0 && (p.x < 140 || p.y < 140 || p.x > r.width - 140 || p.y > r.height - 220)) continue;
        sim.build('ranger', x, y);
        out.push({ uid: sim.towerAt(x, y).uid, x, y });
        if (out.length >= 24) return out;
      }
      return out;
    });
    assert.ok(t.length >= 12, `genug Türme (${t.length})`);
    await page.waitForTimeout(500);
    // Keine Gegner am Klickpunkt (Gegner haben beim Anklicken Vorrang vor Türmen)
    await he(() => { const sim = window.__kernwacht.session.sim; for (const e of sim.s.enemies) e.alive = false; sim.s.enemies = []; sim.s.spawnQueue = []; sim.s.he.autoTimer = 999; });
    await page.waitForTimeout(300);
    // Kamera muss stillstehen (Startzoom/Folgen), sonst verrutscht der Klickpunkt
    {
      let prev = '';
      for (let i = 0; i < 40; i++) {
        const cur = await he(([x, y]) => { const p = window.__kernwacht.view.project(x + 0.5, 0.5, y + 0.5); return `${Math.round(p.x)},${Math.round(p.y)}`; }, [t[0].x, t[0].y]);
        if (cur === prev) break;
        prev = cur;
        await page.waitForTimeout(250);
      }
    }
    // Klickpunkt, an dem die Turmauswahl den Turm tatsächlich trifft (Modellhöhe variiert)
    const top = await he(([x, y, uid]) => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      const m = sim.s.map;
      const r = app.canvas.getBoundingClientRect();
      let best = null;
      for (const hgt of [0.5, 0.35, 0.65, 0.8, 0.2, 1.0]) {
        const p = app.view.project(x + 0.5, m.heights[y * m.width + x] * 0.24 + hgt, y + 0.5);
        best = best ?? p;
        if (app.view.pickTower(p.x, p.y, sim) === uid) { best = p; break; }
      }
      return { x: r.left + best.x, y: r.top + best.y };
    }, [t[0].x, t[0].y, t[0].uid]);
    await page.mouse.dblclick(top.x, top.y);
    await page.waitForSelector('[data-testid=group-box]', { timeout: 8000 }).catch(async () => {
      const d = await he((pt) => { const app = window.__kernwacht; const s = app.session; const r = app.canvas.getBoundingClientRect(); const el = document.elementFromPoint(pt.x, pt.y); return { sel: s.ui.selectedTower, group: s.hv.groupDef, last: s.lastTowerTap, tapTime: s.tapTime, now: performance.now(), pickT: app.view.pickTower(pt.x - r.left, pt.y - r.top, s.sim), el: el ? el.className || el.tagName : null, modal: s.hud.hasModal(), paused: s.sim.s.paused, t0: s.sim.c.towerByUid.has(pt.uid), nT: s.sim.s.towers.length, proj: (() => { const tw = s.sim.c.towerByUid.get(pt.uid); if (!tw) return null; const p = app.view.project(tw.cx + 0.5, 0.5, tw.cy + 0.5); return [Math.round(p.x + r.left), Math.round(p.y + r.top)]; })(), pt: [Math.round(pt.x), Math.round(pt.y)] }; }, { ...top, uid: t[0].uid });
      throw new Error(`Gruppenauswahl per Doppelklick fehlgeschlagen ${JSON.stringify(d)}`);
    });
    assert.equal(await he(() => window.__kernwacht.session.hv.groupDef), 'ranger');
    assert.match(await page.textContent('[data-testid=group-box]'), new RegExp(`${t.length}× `));
    await page.screenshot({ path: `${OUT}/desktop-26-group.png` });
    await page.click('[data-testid=group-upgrade]');
    const levels = await he((uids) => uids.map((u) => window.__kernwacht.session.sim.c.towerByUid.get(u)), t.map((x) => x.uid));
    assert.ok(levels.every((x) => x.level === 4 && x.spec === 0), 'alle Waldläufer auf Stufe 4, keine Spezialisierung');
    await page.keyboard.press('Escape');
    // Alles aufwerten im Kopf: weitere Türme anderer Sorte
    const others = await he(() => {
      const sim = window.__kernwacht.session.sim;
      const m = sim.s.map;
      const out = [];
      for (let y = 1; y < m.height - 1 && out.length < 16; y++) for (let x = 1; x < m.width - 1 && out.length < 16; x++) if (sim.build('ember', x, y).ok) out.push(sim.towerAt(x, y).uid);
      sim.s.gold = 100000;
      return out;
    });
    await page.keyboard.press('o');
    await page.waitForSelector('[data-testid=tower-list]:not(.hidden)');
    // Ausgebaute (Stufe 4 aber noch ohne Spezialisierung) bleiben sichtbar, die Liste scrollt und behält die Position
    const sc = await page.$eval('[data-testid=tower-list] .tl-rows', (el) => { el.scrollTop = 220; return { top: el.scrollTop, h: el.scrollHeight, c: el.clientHeight }; });
    assert.ok(sc.h > sc.c, 'Liste ist scrollbar');
    await he(() => { window.__kernwacht.session.sim.s.gold -= 7; });
    await page.waitForTimeout(400);
    const after = await page.$eval('[data-testid=tower-list] .tl-rows', (el) => el.scrollTop);
    assert.ok(Math.abs(after - sc.top) < 2, `Scrollposition bleibt (${sc.top} → ${after})`);
    await page.click('[data-testid=tl-upgrade-all]');
    assert.ok(await he((uids) => uids.every((u) => window.__kernwacht.session.sim.c.towerByUid.get(u).level === 4), others), 'alle Glutwerfer aufgewertet');
    // Voll ausgebaut = Stufe 4 und spezialisiert → ausgeblendet
    // 3.6: im Endlos gilt ein Turm erst mit der höchsten Veredelung (★25) als ausgebaut
    await he((uids) => { const sim = window.__kernwacht.session.sim; for (const u of uids) { sim.specialize(u, 1); sim.c.towerByUid.get(u).stars = 25; } sim.c.statsDirty = true; }, others);
    // Auf den Neuaufbau warten (Software-Rendering ist langsam). Nur Wahrheitswerte vergleichen – ein
    // ElementHandle in einer Assertion-Meldung würde Node beim Formatieren den Speicher sprengen.
    await page.waitForFunction((us) => us.every((u) => !document.querySelector(`[data-testid=tl-${u}]`)), others.slice(0, 3), { timeout: 15000 })
      .catch(() => { throw new Error('voll ausgebaute Türme werden nicht ausgeblendet'); });
    await page.screenshot({ path: `${OUT}/desktop-27-tower-list.png` });
    await page.click('[data-testid=tl-hide-maxed]');
    await page.waitForSelector(`[data-testid=tl-${others[0]}]`);
    await page.keyboard.press('o');
    // Goldgräber belegt 2×2
    const gm = await he(() => {
      const sim = window.__kernwacht.session.sim;
      const m = sim.s.map;
      for (let y = 1; y < m.height - 2; y++) for (let x = 1; x < m.width - 2; x++) if (sim.build('goldmine', x, y).ok) {
        const t = sim.towerAt(x, y);
        return [sim.towerAt(x + 1, y) === t, sim.towerAt(x, y + 1) === t, sim.towerAt(x + 1, y + 1) === t];
      }
      return null;
    });
    assert.deepEqual(gm, [true, true, true], 'Goldgräber belegt 2×2 Felder');
    await page.keyboard.press('Escape');
  });

  await step('2.7: Versionsanzeige, freie Kamera, dauerhafte Stufe 99 mit Paragon-Bäumen, Runen sockeln', async () => {
    await he(() => {
      const app = window.__kernwacht;
      const m = app.save.progress.heroMeta;
      m.champions.lyra = { level: 99, xp: 0, tree: {} };
      m.heroLevel = { level: 99, xp: 0 };
      m.paragon = { level: 5, xp: 0, points: {} };
      app.go('heroSetup');
    });
    await page.waitForSelector('[data-testid=champ-lvl-lyra]');
    assert.match(await page.textContent('[data-testid=champ-lvl-lyra]'), /Stufe 99\/99/);
    assert.match(await page.textContent('[data-testid=setup-paragon]'), /5\/500/);
    await page.click('[data-testid=champ-lyra]');
    await page.click('[data-testid=start-hero]');
    await page.waitForSelector('[data-testid=champion]');
    assert.match(await page.textContent('[data-testid=version]'), /^v\d+\.\d+\.\d+$/);
    assert.equal(await he(() => window.__kernwacht.session.sim.s.he.champion.level), 99);
    // Paragon im Charakterbogen
    await page.keyboard.press('Tab');
    await page.click('[data-testid=tab-paragon]');
    await page.waitForSelector('[data-testid=pg-tree-off]');
    assert.equal((await page.$$('[data-testid^=pg-tree-]')).length, 4, 'Offensive, Defensive, Utility, Türme');
    await page.click('[data-testid=pg-pg_might]');
    await page.waitForFunction(() => document.querySelector('[data-testid=paragon-free]')?.textContent.trim() === '14');
    await page.click('[data-testid=pg-pg_vital]');
    await page.waitForFunction(() => document.querySelector('[data-testid=paragon-free]')?.textContent.trim() === '13');
    await page.screenshot({ path: `${OUT}/desktop-28-paragon.png` });
    assert.deepEqual(await he(() => window.__kernwacht.save.progress.heroMeta.paragon.points), { pg_might: 1, pg_vital: 1 }, 'dauerhaft gespeichert');
    await page.keyboard.press('Escape');
    // Runen: Turm Stufe 2, Rune aus dem Beutel sockeln
    const uid = await he(() => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      sim.s.gold = 10000;
      if (!sim.s.paused) app.session.miniPause();
      const m = sim.s.map;
      let t = null;
      for (let y = 2; y < m.height - 2 && !t; y++) for (let x = 2; x < m.width - 2 && !t; x++) if (sim.build('ranger', x, y).ok) t = sim.towerAt(x, y);
      sim.upgrade(t.uid);
      sim.s.wave = Math.max(sim.s.wave, 100);
      sim.s.he.runes = [{ uid: sim.uid(), rune: 'might', tier: 3, tower: 0 }, { uid: sim.uid(), rune: 'haste', tier: 1, tower: 0 }];
      app.session.selectTower(t.uid);
      return t.uid;
    });
    await page.waitForSelector('[data-testid=rune-box]');
    await page.dblclick('[data-testid=rune-bag-might-3]');
    assert.equal(await he(() => window.__kernwacht.session.sim.s.he.runes.find((r) => r.rune === 'might').def), 'ranger', 'Rune in die Sorte gesockelt');
    await page.screenshot({ path: `${OUT}/desktop-29-runes.png` });
    await page.dblclick('[data-testid=rune-slot-0]');
    assert.equal(await he(() => window.__kernwacht.session.sim.s.he.runes.find((r) => r.rune === 'might').def ?? null), null, 'zurück im Beutel');
    await page.keyboard.press('Escape');
    // Freie Kamera: mittlere Maustaste ziehen dreht und neigt, Zurücksetzen
    const box = await page.$eval('canvas.game-canvas', (c) => { const r = c.getBoundingClientRect(); return { x: r.left + r.width * 0.5, y: r.top + r.height * 0.5 }; });
    const y0 = await he(() => window.__kernwacht.view.cam.yaw);
    await page.mouse.move(box.x, box.y);
    await page.mouse.down({ button: 'middle' });
    await page.mouse.move(box.x + 60, box.y + 10, { steps: 6 });
    await page.mouse.move(box.x + 120, box.y + 30, { steps: 6 });
    await page.mouse.up({ button: 'middle' });
    const cam = await he(() => ({ yaw: window.__kernwacht.view.cam.yaw, free: window.__kernwacht.view.cam.freeYaw, tilt: window.__kernwacht.view.cam.pitchAdj }));
    assert.ok(Math.abs(cam.free) > 0.3, `frei gedreht ${JSON.stringify(cam)}`);
    assert.ok(Math.abs(cam.yaw - y0) > 0.3);
    assert.ok(cam.tilt !== 0, 'geneigt');
    await page.screenshot({ path: `${OUT}/desktop-30-free-camera.png` });
    await page.click('[data-testid=reset-view]');
    assert.equal(await he(() => window.__kernwacht.view.cam.freeYaw), 0);
    await page.keyboard.press('.');
    assert.ok(Math.abs(await he(() => window.__kernwacht.view.cam.freeYaw) - Math.PI / 12) < 1e-6, 'Taste . dreht 15°');
    await page.keyboard.press('Home');
  });

  await step('2.8: Minenwerfer wirft Minen auf die Lane, Sporenminen nach Fusion', async () => {
    const r = await he(() => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      if (sim.s.paused) app.session.miniPause();
      sim.s.gold = 100000;
      sim.s.wave = Math.max(sim.s.wave, 6);
      const m = sim.s.map;
      let t = null;
      for (let y = 1; y < m.height - 1 && !t; y++) for (let x = 1; x < m.width - 1 && !t; x++) {
        let lane = false;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) if (m.cells[(y + dy) * m.width + x + dx] === 1) lane = true;
        if (lane && sim.build('minelayer', x, y).ok) t = sim.towerAt(x, y);
      }
      if (!t) return null;
      app.lookAt?.(t.cx, t.cy);
      app.session.lookAt(t.cx + 0.5, t.cy + 0.5);
      if (sim.s.phase === 'build') app.session.startWave();
      return t.uid;
    });
    assert.ok(r, 'Minenwerfer gebaut');
    await page.waitForFunction((u) => window.__kernwacht.session.sim.s.zones.some((z) => z.kind === 'mine' && z.link === u), r, { timeout: 30000 });
    await page.waitForTimeout(800);
    await page.screenshot({ path: `${OUT}/desktop-31-mines.png` });
  });

  await step('2.9: Bäume und Felsen über das ganze Modell anklickbar', async () => {
    const target = await he(() => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      if (!sim.s.paused) app.session.miniPause();
      app.session.selectTower(0);
      const m = sim.s.map;
      const r = app.canvas.getBoundingClientRect();
      for (let k = 0; k + 2 < m.decor.length; k += 3) {
        const idx = m.decor[k];
        const kind = m.decor[k + 1];
        if (m.cells[idx] !== 2 || kind === 3 || kind === 7) continue;
        if (sim.s.he.shop.y * m.width + sim.s.he.shop.x === idx) continue;
        const x = idx % m.width, y = Math.floor(idx / m.width);
        const base = m.heights[idx] * 0.24;
        const top = app.view.project(x + 0.5, base + (kind === 0 || kind === 5 ? 1.0 : 0.55), y + 0.5);
        if (top.x < 150 || top.y < 120 || top.x > r.width - 150 || top.y > r.height - 260) continue;
        // Der Klickpunkt darf nicht über einem Turm, Gegner oder HUD-Element (Minikarte, Leisten) liegen
        if (app.view.pickTower(top.x, top.y, sim) || app.view.pickEnemy(top.x, top.y, sim)) continue;
        if (document.elementFromPoint(r.left + top.x, r.top + top.y) !== app.canvas) continue;
        return { idx, x: r.left + top.x, y: r.top + top.y, pick: app.view.pickObstacle(top.x, top.y, sim), build: app.session.ui.buildDef, tgt: app.session.targeting, modal: app.session.hud.hasModal(), cellPick: app.view.pickCell(top.x, top.y, m) };
      }
      return null;
    });
    assert.ok(target, 'sichtbares Hindernis');
    await page.mouse.click(target.x, target.y);
    const after = await he(() => { const s = window.__kernwacht.session; return { sel: s.hv.selectedObstacle, tower: s.ui.selectedTower, enemy: s.hv.selectedEnemy, champ: s.champSelected }; });
    assert.equal(after.sel, target.idx, `Hindernis über die Modellspitze ausgewählt ${JSON.stringify({ target, after })}`);
    await page.waitForSelector('[data-testid=mine]');
  });

  await step('2.9.2: Alle Türme verkaufen (zweimal klicken), gesperrt in der Mini-Pause', async () => {
    const n = await he(() => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      app.session.hv.miniPaused = false;
      if (sim.s.paused) sim.setPaused(false);
      app.session.selectTower(0);
      sim.s.gold = 10000;
      const m = sim.s.map;
      let k = 0;
      for (let y = 1; y < m.height - 1 && k < 3; y++) for (let x = 1; x < m.width - 1 && k < 3; x++) if (sim.build('ranger', x, y).ok) k++;
      return sim.s.towers.length;
    });
    assert.ok(n >= 3);
    if (!(await page.$('[data-testid=tower-list]:not(.hidden)'))) await page.keyboard.press('o');
    await page.waitForSelector('[data-testid=tl-sell-all]');
    // Mini-Pause sperrt den Knopf
    await he(() => window.__kernwacht.session.miniPause());
    await page.waitForSelector('[data-testid=tl-sell-all][disabled]');
    await he(() => window.__kernwacht.session.miniPause());
    await page.waitForSelector('[data-testid=tl-sell-all]:not([disabled])');
    const g0 = await he(() => window.__kernwacht.session.sim.s.gold);
    await page.click('[data-testid=tl-sell-all]');
    assert.ok(await he(() => window.__kernwacht.session.sim.s.towers.length) >= 3, 'erster Klick verkauft noch nicht');
    await page.waitForSelector('[data-testid=tl-sell-all].armed');
    await page.click('[data-testid=tl-sell-all]');
    assert.equal(await he(() => window.__kernwacht.session.sim.s.towers.length), 0, 'alle verkauft');
    assert.ok(await he(() => window.__kernwacht.session.sim.s.gold) > g0, 'Gold gutgeschrieben');
    await page.keyboard.press('o');
  });

  await step('2.9.3: Turmübersicht und Turmpanel sind im laufenden Kampf klickbar (langsamer Klick)', async () => {
    // Laufender Kampf: Gold, Schaden und Abschüsse ändern sich ständig
    const ids = await he(() => {
      const app = window.__kernwacht;
      const s = app.session;
      const sim = s.sim;
      s.hv.miniPaused = false;
      if (sim.s.paused) sim.setPaused(false);
      sim.s.gold = 100000;
      const m = sim.s.map;
      const out = [];
      for (let y = 1; y < m.height - 1 && out.length < 4; y++) for (let x = 1; x < m.width - 1 && out.length < 4; x++) {
        let lane = false;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) if (m.cells[(y + dy) * m.width + x + dx] === 1) lane = true;
        if (lane && sim.build('ranger', x, y).ok) out.push(sim.towerAt(x, y).uid);
      }
      // Ein Turm auf Stufe 4 (für die Spezialisierung)
      while (sim.c.towerByUid.get(out[1]).level < 4) sim.upgrade(out[1]);
      if (sim.s.phase === 'build') s.startWave();
      s.selectTower(0);
      return out;
    });
    await page.waitForFunction(() => window.__kernwacht.session.sim.s.enemies.some((e) => e.alive), null, { timeout: 15000 });
    // Gold tickt laufend (wie bei Abschüssen)
    await he(() => { window.__e2eGold = setInterval(() => { window.__kernwacht.session.sim.s.gold += 3; }, 30); });
    const slowClick = async (sel) => {
      const box = await (await page.waitForSelector(sel)).boundingBox();
      await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
      await page.mouse.down();
      await page.waitForTimeout(220);
      await page.mouse.up();
    };
    await he(() => window.__kernwacht.session.hud.toggleTowerList(true));
    // SwiftShader schafft im Kampf nur wenige Bilder pro Sekunde: auf den Neuaufbau der Liste warten
    await page.waitForSelector(`[data-testid=tl-up-${ids[0]}]`, { timeout: 20000 });
    await page.$eval(`[data-testid=tl-up-${ids[0]}]`, (e) => e.scrollIntoView({ block: 'center' }));
    await slowClick(`[data-testid=tl-up-${ids[0]}]`);
    await page.waitForFunction((u) => window.__kernwacht.session.sim.c.towerByUid.get(u).level === 2, ids[0], { timeout: 5000 }).catch(() => { throw new Error('Aufwerten-Klick in der Übersicht ging verloren'); });
    await page.waitForSelector(`[data-testid=tl-spec-${ids[1]}]`, { timeout: 20000 });
    await page.$eval(`[data-testid=tl-spec-${ids[1]}]`, (e) => e.scrollIntoView({ block: 'center' }));
    await slowClick(`[data-testid=tl-spec-${ids[1]}]`);
    await page.waitForFunction((u) => window.__kernwacht.session.ui.selectedTower === u, ids[1], { timeout: 5000 }).catch(() => { throw new Error('Spezial.-Klick in der Übersicht ging verloren'); });
    await page.waitForSelector('[data-testid=spec-1]:not([disabled])', { timeout: 20000 });
    // Wie ein Spieler: im Panel zur Spezialisierung scrollen; die Position muss den Live-Neuaufbau überstehen
    await page.$eval('[data-testid=spec-1]', (e) => e.scrollIntoView({ block: 'center' }));
    await page.waitForTimeout(2500);
    await slowClick('[data-testid=spec-1]');
    await page.waitForFunction((u) => window.__kernwacht.session.sim.c.towerByUid.get(u).spec === 1, ids[1], { timeout: 5000 }).catch(async () => {
      const d = await he((u) => { const b = document.querySelector('[data-testid=spec-1]'); const r = b?.getBoundingClientRect(); const el = r ? document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2) : null; const t = window.__kernwacht.session.sim.c.towerByUid.get(u); return { exists: !!b, dis: b?.disabled, hit: el === b, elc: el ? (el.className || el.tagName) : null, lvl: t.level, spec: t.spec, sel: window.__kernwacht.session.ui.selectedTower, toasts: [...document.querySelectorAll('.toast')].map((x) => x.textContent).slice(-3) }; }, ids[1]);
      throw new Error(`Spezialisierungs-Klick im Panel ging verloren ${JSON.stringify(d)}`);
    });
    await he(() => clearInterval(window.__e2eGold));
    await page.keyboard.press('o');
  });

  await step('2.9.4: Fusion in der Turmübersicht, Ergebnis in Richtung Lane', async () => {
    const r = await he(() => {
      const app = window.__kernwacht;
      const s = app.session;
      const sim = s.sim;
      s.hv.miniPaused = false;
      if (sim.s.paused) sim.setPaused(false);
      s.selectTower(0);
      sim.s.gold = 1e6;
      sim.s.wave = Math.max(sim.s.wave, 5);
      const m = sim.s.map;
      for (let y = 2; y < m.height - 2; y++) for (let x = 2; x < m.width - 2; x++) {
        if (m.cells[y * m.width + x] !== 1) continue;
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
          const nx = x + dx, ny = y + dy, fx = x + 2 * dx, fy = y + 2 * dy;
          if (!sim.canBuild('ember', fx, fy).ok || !sim.canBuild('storm', nx, ny).ok) continue;
          if ([[1, 0], [-1, 0], [0, 1], [0, -1]].some(([ex, ey]) => m.cells[(fy + ey) * m.width + fx + ex] === 1)) continue;
          sim.build('ember', fx, fy);
          sim.build('storm', nx, ny);
          const a = sim.towerAt(fx, fy), b = sim.towerAt(nx, ny);
          for (const [t, sp] of [[a, 1], [b, 2]]) { while (t.level < 4) sim.upgrade(t.uid); sim.specialize(t.uid, sp); }
          return { a: a.uid, b: b.uid, near: [nx, ny], far: [fx, fy] };
        }
      }
      return null;
    });
    assert.ok(r, 'Fusionspaar gebaut');
    await he(() => window.__kernwacht.session.hud.toggleTowerList(true));
    await page.waitForSelector('[data-testid=tl-fusions]', { timeout: 20000 });
    const sel = `[data-testid=tl-fuse-${Math.min(r.a, r.b) === r.a ? r.a : r.b}-${Math.min(r.a, r.b) === r.a ? r.b : r.a}], [data-testid=tl-fuse-${r.a}-${r.b}], [data-testid=tl-fuse-${r.b}-${r.a}]`;
    await page.waitForSelector(`:is(${sel}):not([disabled])`, { timeout: 20000 });
    await page.screenshot({ path: `${OUT}/desktop-32-fusion-list.png` });
    await page.click(`:is(${sel})`);
    const res = await he((rr) => { const sim = window.__kernwacht.session.sim; const n = sim.towerAt(rr.near[0], rr.near[1]); const f = sim.towerAt(rr.far[0], rr.far[1]); return { near: n ? n.def : null, far: f ? f.def : null }; }, r);
    assert.deepEqual(res, { near: 'plasma', far: null }, 'legendärer Turm auf dem Feld am Weg');
    await he(() => window.__kernwacht.session.hud.toggleTowerList(false));
  });

  await step('2.9.5: Tempo 5×/8×, Spiel läuft im Hintergrund weiter, legendären Turm versetzen', async () => {
    // Tempo-Zyklus 1 → 2 → 3 → 5 → 8 → 1
    await he(() => { const s = window.__kernwacht.session; s.hv.miniPaused = false; if (s.sim.s.paused) s.sim.setPaused(false); s.sim.setSpeed(3); });
    await page.click('[data-testid=speed]');
    assert.equal(await he(() => window.__kernwacht.session.sim.s.speed), 5);
    await page.click('[data-testid=speed]');
    assert.equal(await he(() => window.__kernwacht.session.sim.s.speed), 8);
    await page.click('[data-testid=speed]');
    assert.equal(await he(() => window.__kernwacht.session.sim.s.speed), 1);
    // Tab verborgen: kein Pausieren, die Simulation rechnet weiter
    const t0 = await he(() => {
      const s = window.__kernwacht.session;
      if (s.sim.s.phase === 'build') s.startWave();
      Object.defineProperty(document, 'hidden', { configurable: true, get: () => true });
      document.dispatchEvent(new Event('visibilitychange'));
      return s.sim.s.tick;
    });
    await page.waitForTimeout(1500);
    const bg = await he(() => ({ paused: window.__kernwacht.session.sim.s.paused, tick: window.__kernwacht.session.sim.s.tick }));
    await he(() => { Object.defineProperty(document, 'hidden', { configurable: true, get: () => false }); document.dispatchEvent(new Event('visibilitychange')); });
    assert.equal(bg.paused, false, 'kein automatisches Pausieren');
    assert.ok(bg.tick > t0, 'Simulation läuft im Hintergrund weiter');
    // Legendären Turm versetzen
    const r = await he(() => {
      const app = window.__kernwacht;
      const s = app.session;
      const sim = s.sim;
      sim.s.gold = 1e6;
      const leg = sim.s.towers.find((t) => t.def === 'plasma');
      if (!leg) return null;
      const m = sim.s.map;
      const rect = app.canvas.getBoundingClientRect();
      for (let y = 1; y < m.height - 1; y++) for (let x = 1; x < m.width - 1; x++) {
        if (!sim.canMoveTower(leg.uid, x, y).ok || Math.abs(x - leg.cx) + Math.abs(y - leg.cy) < 3) continue;
        s.lookAt(x + 0.5, y + 0.5);
        s.selectTower(leg.uid);
        return { uid: leg.uid, x, y };
      }
      return null;
    });
    assert.ok(r, 'legendärer Turm und Zielfeld');
    await page.waitForSelector('[data-testid=move-tower]');
    await page.click('[data-testid=move-tower]');
    await page.waitForTimeout(300);
    const pt = await he((rr) => { const app = window.__kernwacht; const m = app.session.sim.s.map; const p = app.view.project(rr.x + 0.5, m.heights[rr.y * m.width + rr.x] * 0.24, rr.y + 0.5); const rc = app.canvas.getBoundingClientRect(); return { x: rc.left + p.x, y: rc.top + p.y }; }, r);
    await page.mouse.move(pt.x, pt.y);
    await page.mouse.click(pt.x, pt.y);
    const moved = await he((rr) => { const t = window.__kernwacht.session.sim.c.towerByUid.get(rr.uid); return [t.cx, t.cy]; }, r);
    assert.deepEqual(moved, [r.x, r.y], 'Turm versetzt');
  });

  await step('3.0: Weltstufe und Wellen-Affixe in der Wellenleiste, Meisterschaft im Turmpanel', async () => {
    await he(() => {
      const app = window.__kernwacht;
      const s = app.session;
      const sim = s.sim;
      s.hv.miniPaused = false;
      if (sim.s.paused) sim.setPaused(false);
      // Bauphase vor Welle 180 erzwingen: Vorschau zeigt Weltstufe 3 und zwei Affixe
      sim.s.enemies = [];
      sim.s.spawnQueue = [];
      sim.s.phase = 'build';
      sim.s.wave = 179;
      sim.s.he.autoTimer = 999;
    });
    await page.waitForFunction(() => /Weltstufe 3/.test(document.querySelector('[data-testid=world-tier]')?.textContent ?? ''), null, { timeout: 20000 });
    assert.ok((await page.$$('[data-testid^=affix-]')).length === 2, 'zwei Affixe');
    await page.screenshot({ path: `${OUT}/desktop-33-affixes.png` });
    const uid = await he(() => { const s = window.__kernwacht.session; const t = s.sim.s.towers.find((x) => x.def === 'ranger' || x.def === 'plasma'); if (t) s.selectTower(t.uid); return t ? t.uid : 0; });
    assert.ok(uid);
    await page.waitForSelector('[data-testid=mastery]', { timeout: 20000 });
    assert.match(await page.textContent('[data-testid=mastery]'), /Meisterschaft .*Rang \d+\/20/);
    await page.keyboard.press('Escape');
  });

  await step('3.1: Challenge-Boss in der Wellenvorschau (Welle 275)', async () => {
    await he(() => {
      const sim = window.__kernwacht.session.sim;
      sim.s.enemies = [];
      sim.s.spawnQueue = [];
      sim.s.phase = 'build';
      sim.s.wave = 274;
      sim.s.he.autoTimer = 999;
    });
    await page.waitForFunction(() => document.querySelector('.strip-label')?.textContent?.includes('CHALLENGE'), null, { timeout: 20000 });
  });

  await step('3.1: Spezialisierter Turmkauf per Auswahlmenü (ab Welle 250)', async () => {
    await he(() => {
      const s = window.__kernwacht.session;
      s.cancel();
      s.sim.s.gold = 1e6;
      s.sim.s.wave = 260;
      s.sim.s.phase = 'build';
      s.sim.s.he.autoTimer = 999;
    });
    await page.waitForTimeout(300);
    await page.click('[data-testid=tower-ranger]');
    await page.waitForSelector('[data-testid=prespec-pop]', { timeout: 5000 });
    assert.equal((await page.$$('[data-testid^=prespec-]')).length >= 4, true, 'Normal + 2 Spezialisierungen');
    await page.screenshot({ path: `${OUT}/desktop-34-prespec.png` });
    await page.click('[data-testid=prespec-1]');
    await page.waitForSelector('[data-testid=prespec-build]', { timeout: 5000 });
    assert.equal(await page.$('[data-testid=prespec-pop]') === null, true, 'Menü geschlossen');
    const before = await he(() => window.__kernwacht.session.sim.s.towers.length);
    const c = await findBuildCell(page, 'ranger');
    const p = await cellPoint(page, c.x, c.y);
    await page.mouse.click(p.x, p.y);
    await page.waitForFunction((n) => window.__kernwacht.session.sim.s.towers.length > n, before, { timeout: 5000 });
    const t = await he(([x, y]) => { const t = window.__kernwacht.session.sim.towerAt(x, y); return t ? { level: t.level, spec: t.spec, def: t.def } : null; }, [c.x, c.y]);
    assert.equal(JSON.stringify(t), JSON.stringify({ level: 4, spec: 1, def: 'ranger' }));
    globalThis.__preCell = c;
  });

  await step('3.1: Rune anklicken zeigt Wirkung, Doppelklick sockelt bzw. nimmt heraus', async () => {
    const c = globalThis.__preCell;
    await he(([x, y]) => {
      const s = window.__kernwacht.session;
      s.cancel();
      const sim = s.sim;
      const t = sim.towerAt(x, y);
      sim.s.he.runes = [{ uid: sim.uid(), rune: 'might', tier: 3, tower: 0 }];
      s.selectTower(t.uid);
    }, [c.x, c.y]);
    const socketed = () => he(([x, y]) => { const sim = window.__kernwacht.session.sim; const t = sim.towerAt(x, y); return sim.s.he.runes.filter((r) => r.def === t.def).length; }, [c.x, c.y]);
    await page.waitForSelector('[data-testid=rune-bag-might-3]', { timeout: 10000 });
    await page.click('[data-testid=rune-bag-might-3]');
    await page.waitForSelector('[data-testid=rune-info]', { timeout: 5000 });
    assert.match(await page.textContent('[data-testid=rune-info]'), /Doppelklick/);
    assert.equal(await socketed(), 0, 'Einfacher Klick sockelt nicht');
    await page.screenshot({ path: `${OUT}/desktop-35-rune-info.png` });
    await page.dblclick('[data-testid=rune-bag-might-3]');
    await page.waitForFunction(([x, y]) => { const sim = window.__kernwacht.session.sim; const t = sim.towerAt(x, y); return sim.s.he.runes.some((r) => r.def === t.def); }, [c.x, c.y], { timeout: 5000 });
    await page.waitForSelector('[data-testid=rune-slot-0].on', { timeout: 5000 });
    await page.click('[data-testid=rune-slot-0]');
    await page.waitForSelector('[data-testid=rune-info-unsocket]', { timeout: 5000 });
    assert.equal(await socketed(), 1, 'Klick auf gesockelte Rune zeigt nur Info');
    await page.dblclick('[data-testid=rune-slot-0]');
    await page.waitForFunction(([x, y]) => { const sim = window.__kernwacht.session.sim; const t = sim.towerAt(x, y); return !sim.s.he.runes.some((r) => r.def === t.def); }, [c.x, c.y], { timeout: 5000 });
    await page.keyboard.press('Escape');
  });

  await step('3.2: Gruppe – Spezialisierung aller Türme einer Sorte per Knopf wechseln', async () => {
    const uids = await he(() => {
      const s = window.__kernwacht.session;
      const sim = s.sim;
      s.cancel();
      sim.s.gold = 1e6;
      sim.s.phase = 'build';
      sim.s.he.autoTimer = 999;
      const m = sim.s.map;
      let n = sim.s.towers.filter((t) => t.def === 'ranger').length;
      for (let y = 1; y < m.height - 1 && n < 3; y++) for (let x = 1; x < m.width - 1 && n < 3; x++) if (sim.canBuild('ranger', x, y).ok && sim.build('ranger', x, y).ok) n++;
      const g = sim.s.towers.filter((t) => t.def === 'ranger');
      s.selectTower(g[0].uid);
      s.hv.groupDef = 'ranger';
      s.hud.forcePanelRefresh();
      return g.map((t) => t.uid);
    });
    assert.ok(uids.length >= 2);
    await page.waitForSelector('[data-testid=group-spec-2]', { timeout: 8000 });
    await page.screenshot({ path: `${OUT}/desktop-36-group-spec.png` });
    await page.click('[data-testid=group-spec-2]');
    await page.waitForFunction(() => window.__kernwacht.session.sim.s.towers.filter((t) => t.def === 'ranger').every((t) => t.level === 4 && t.spec === 2), null, { timeout: 5000 });
    await page.keyboard.press('Escape');
  });

  await step('3.2: Heldenschmiede zeigt zwei eigene Rezepte und stellt den Signaturgegenstand her', async () => {
    const n0 = await he(() => {
      const s = window.__kernwacht.session;
      s.cancel();
      const sim = s.sim;
      const hs = sim.s.he;
      sim.s.wave = 20;
      sim.s.phase = 'build';
      hs.autoTimer = 999;
      hs.champion.inventory = [];
      hs.champion.equipment = hs.champion.equipment.map(() => null);
      for (const k of Object.keys(hs.resources)) hs.resources[k] = 999;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, -1], [1, -1], [-1, 1]]) {
        const x = hs.shop.x + dx; const y = hs.shop.y + dy;
        if (sim.s.map.cells[y * sim.s.map.width + x] !== 2 && !sim.towerAt(x, y)) { hs.champion.x = x + 0.5; hs.champion.y = y + 0.5; hs.champion.hx = hs.champion.x; hs.champion.hy = hs.champion.y; hs.champion.path = []; break; }
      }
      return hs.champion.inventory.length;
    });
    await page.keyboard.press('x');
    await page.waitForSelector('[data-testid=he-window]:not(.hidden)');
    await page.click('[data-testid=tab-forge]');
    await page.waitForSelector('[data-testid=hero-forge]');
    assert.equal((await page.$$('[data-testid=hero-forge] [data-testid^="craft-hero:"]')).length, 2, 'zwei Heldenrezepte');
    await page.screenshot({ path: `${OUT}/desktop-37-hero-forge.png` });
    await page.click('[data-testid=hero-forge] [data-testid^="craft-hero:"]:not([disabled])');
    await page.waitForFunction((n) => window.__kernwacht.session.sim.s.he.champion.inventory.length > n, n0, { timeout: 5000 });
    await page.keyboard.press('Escape');
  });

  await step('3.3: Turmlimit in der Kopfzeile, Bau gesperrt bei vollem Limit; Runen gelten für die ganze Sorte', async () => {
    const info = await he(() => {
      const s = window.__kernwacht.session;
      s.cancel();
      const sim = s.sim;
      sim.s.rules.towerLimitOff = false;
      sim.s.phase = 'build';
      sim.s.he.autoTimer = 999;
      sim.s.gold = 1e6;
      const n = sim.s.towers.filter((t) => !['barricade', 'lanebarricade', 'pylon', 'totem', 'goldmine'].includes(t.def)).length;
      // Limit künstlich genau auf die vorhandene Turmzahl setzen (Start 20, Bosswellen-Plätze ggf. negativ)
      sim.s.wave = 0;
      sim.s.bossSlots = n - 20;
      sim.c.statsDirty = true;
      const m = sim.s.map;
      let reason = '';
      for (let y = 1; y < m.height - 1 && !reason; y++) for (let x = 1; x < m.width - 1 && !reason; x++) {
        const c = sim.canBuild('ranger', x, y);
        if (!c.ok && c.reason.startsWith('Turmlimit')) reason = c.reason;
      }
      return { n, reason };
    });
    assert.ok(info.n >= 6, `Türme: ${info.n}`);
    assert.match(info.reason, /Turmlimit erreicht/);
    await page.waitForFunction((n) => (document.querySelector('[data-testid=tower-limit]')?.textContent ?? '').startsWith(`${n}/`), info.n, { timeout: 5000 });
    // Runen: Hinweis „gilt für alle“ im Turmpanel
    await he(() => {
      const s = window.__kernwacht.session;
      const sim = s.sim;
      sim.s.wave = Math.max(sim.s.wave, 120);
      const t = sim.s.towers.find((x) => x.def === 'ranger');
      s.selectTower(t.uid);
    });
    await page.waitForSelector('[data-testid=rune-sort-note]', { timeout: 8000 });
    assert.match(await page.textContent('[data-testid=rune-sort-note]'), /Gilt für alle \d+ Waldläuferposten/);
    await page.screenshot({ path: `${OUT}/desktop-38-limit-runes.png` });
    await he(() => { const s = window.__kernwacht.session.sim.s; s.rules.towerLimitOff = true; s.bossSlots = 0; });
    await page.keyboard.press('Escape');
  });

  await step('3.4: Skillbaum zurücksetzen, Bauplatz-Erklärung', async () => {
    await he(() => {
      const s = window.__kernwacht.session;
      s.cancel();
      const sim = s.sim;
      sim.s.phase = 'build';
      sim.s.he.autoTimer = 999;
      sim.s.gold = 1e6;
      const c = sim.s.he.champion;
      c.skillPoints = Math.max(c.skillPoints, 2);
    });
    // einen Knoten lernen (erster lernbarer)
    await he(() => {
      const sim = window.__kernwacht.session.sim;
      const c = sim.s.he.champion;
      for (const id of ['_hp', '_a', '_core']) { const r = sim.learnSkill(`${c.id}${id}`); if (r.ok) break; }
      if (!Object.values(c.tree).some((v) => v > 0)) { for (const k of ['l_dmg', 'b_dmg', 'v_dmg']) if (sim.learnSkill(k).ok) break; }
    });
    await page.keyboard.press('Tab');
    await page.waitForSelector('[data-testid=tab-tree]');
    await page.click('[data-testid=tab-tree]');
    await page.waitForSelector('[data-testid=tree-reset]', { timeout: 5000 });
    await page.click('[data-testid=tree-reset]');
    await page.waitForFunction(() => Object.values(window.__kernwacht.session.sim.s.he.champion.tree).every((v) => !v), null, { timeout: 5000 });
    await page.keyboard.press('Escape');
    await page.waitForTimeout(200);
    // Bauplatz-Erklärung
    await he(() => { window.__kernwacht.session.sim.s.rules.towerLimitOff = false; });
    await page.click('[data-testid=tower-limit-btn]');
    await page.waitForSelector('[data-testid=limit-pop]', { timeout: 5000 });
    assert.match(await page.textContent('[data-testid=limit-pop]'), /Fokusbonus/);
    assert.match(await page.textContent('[data-testid=limit-pop]'), /Bosswelle/);
    await page.screenshot({ path: `${OUT}/desktop-39-limit-pop.png` });
    await page.click('[data-testid=tower-limit-btn]');
    await he(() => { window.__kernwacht.session.sim.s.rules.towerLimitOff = true; });
  });

  await step('3.5: Flakbatterie in der Turmleiste, Fusion Himmelswacht', async () => {
    assert.ok(await page.$('[data-testid=tower-skyguard]'), 'Flakbatterie-Knopf');
    const r = await he(() => {
      const s = window.__kernwacht.session;
      s.cancel();
      const sim = s.sim;
      sim.s.gold = 1e6;
      sim.s.wave = Math.max(sim.s.wave, 20);
      const m = sim.s.map;
      for (let y = 1; y < m.height - 1; y++) for (let x = 1; x < m.width - 2; x++) {
        if (!sim.canBuild('skyguard', x, y).ok || !sim.canBuild('frost', x + 1, y).ok) continue;
        sim.build('skyguard', x, y);
        sim.build('frost', x + 1, y);
        const a = sim.towerAt(x, y); const b = sim.towerAt(x + 1, y);
        while (a.level < 4) sim.upgrade(a.uid);
        while (b.level < 4) sim.upgrade(b.uid);
        sim.specialize(a.uid, 1); sim.specialize(b.uid, 1);
        const f = sim.fuse(a.uid, b.uid);
        const t = sim.s.towers.find((o) => o.def === 'skywatch');
        if (t) s.selectTower(t.uid);
        return { ok: f.ok, has: !!t };
      }
      return { ok: false, has: false };
    });
    assert.ok(r.ok && r.has, JSON.stringify(r));
    await page.waitForFunction(() => document.querySelector('[data-testid=game] .ph h3')?.textContent?.includes('Himmelswacht'), null, { timeout: 5000 });
    await page.screenshot({ path: `${OUT}/desktop-40-skywatch.png` });
    await page.keyboard.press('Escape');
  });

  await step('3.6: Veredeln per Knopf im Turmpanel und je Sorte in der Turmübersicht', async () => {
    const uid = await he(() => {
      const s = window.__kernwacht.session;
      s.cancel();
      const sim = s.sim;
      sim.s.phase = 'build';
      sim.s.he.autoTimer = 999;
      sim.s.gold = 1e7;
      const t = sim.s.towers.find((x) => x.def === 'ranger');
      while (t.level < 4) sim.upgrade(t.uid);
      if (!t.spec) sim.specialize(t.uid, 1);
      t.stars = 0;
      s.selectTower(t.uid);
      return t.uid;
    });
    await page.waitForSelector('[data-testid=star-btn]', { timeout: 8000 });
    await page.screenshot({ path: `${OUT}/desktop-41-star.png` });
    await page.click('[data-testid=star-btn]');
    await page.waitForFunction((u) => window.__kernwacht.session.sim.c.towerByUid.get(u).stars === 1, uid, { timeout: 5000 });
    await page.waitForSelector('[data-testid=tower-stars]', { timeout: 5000 });
    await page.keyboard.press('Escape');
    // Turmübersicht: Knopf je Sorte, ohne Scrollen
    await page.click('[data-testid=open-tower-list]');
    await page.waitForSelector('[data-testid=tl-type-ranger]', { timeout: 5000 });
    const vis = await page.$eval('[data-testid=tl-type-ranger]', (el) => { const r = el.getBoundingClientRect(); return r.top >= 0 && r.bottom <= innerHeight; });
    assert.equal(vis, true, 'Sortenknopf sichtbar ohne Scrollen');
    const before = await he(() => window.__kernwacht.session.sim.s.towers.filter((t) => t.def === 'ranger').reduce((a, t) => a + t.level * 100 + (t.stars ?? 0), 0));
    await page.click('[data-testid=tl-type-ranger]');
    await page.waitForFunction((b) => window.__kernwacht.session.sim.s.towers.filter((t) => t.def === 'ranger').reduce((a, t) => a + t.level * 100 + (t.stars ?? 0), 0) > b, before, { timeout: 5000 });
    await page.screenshot({ path: `${OUT}/desktop-42-towerlist-types.png` });
    await he(() => window.__kernwacht.session.hud.toggleTowerList(false));
    // 3.8: kein Bauplatzkauf und kein Kernmenü mehr
    assert.ok((await page.$('[data-testid=repair-core]')) === null, 'kein Reparaturknopf');
    await he(() => { window.__kernwacht.session.sim.s.rules.towerLimitOff = true; });
  });

  await step('3.8: Lane-Barrikade auf die Lane setzen (nicht vor dem Riss), Abklingzeit', async () => {
    const ok = await he(() => {
      const s = window.__kernwacht.session;
      s.cancel();
      const sim = s.sim;
      sim.s.phase = 'build';
      sim.s.he.autoTimer = 999;
      sim.s.laneBarricades = [];
      for (const e of sim.s.enemies) e.alive = false;
      sim.s.enemies = [];
      sim.s.spawnQueue = [];
      return sim.s.map.variant !== 'mazing';
    });
    assert.ok(ok, 'Classic-Karte');
    await page.waitForSelector('[data-testid=tower-lanebarricade]', { timeout: 5000 });
    await page.click('[data-testid=tower-lanebarricade]');
    // Kamera muss stillstehen, sonst verrutscht der Klickpunkt
    {
      let prev = '';
      for (let i = 0; i < 40; i++) {
        const cur = await he(() => { const p = window.__kernwacht.view.project(10.5, 0, 10.5); return `${Math.round(p.x)},${Math.round(p.y)}`; });
        if (cur === prev) break;
        prev = cur;
        await page.waitForTimeout(250);
      }
    }
    const c = await he(() => {
      const app = window.__kernwacht;
      const sim = app.session.sim;
      const m = sim.s.map;
      const r = app.canvas.getBoundingClientRect();
      let best = null; let bd = 1e9;
      for (let y = 0; y < m.height; y++) for (let x = 0; x < m.width; x++) {
        if (!sim.canBuild('lanebarricade', x, y).ok) continue;
        const p = app.view.project(x + 0.5, -0.04, y + 0.5);
        if (p.x < 80 || p.y < 120 || p.x > r.width - 80 || p.y > r.height - 120) continue;
        const pick = app.view.pickCell(p.x, p.y, m);
        if (!pick || pick.x !== x || pick.y !== y) continue;
        // nicht von Bedienelementen (Heldenleiste, Panels) verdeckt
        if (document.elementFromPoint(r.left + p.x, r.top + p.y) !== app.canvas) continue;
        const d = (p.x - r.width / 2) ** 2 + (p.y - r.height / 2) ** 2;
        if (d < bd) { bd = d; best = { x, y }; }
      }
      return best;
    });
    assert.ok(c, 'freies Lanefeld sichtbar');
    const placed = () => he(([x, y]) => window.__kernwacht.session.sim.towerAt(x, y)?.def === 'lanebarricade', [c.x, c.y]);
    for (let attempt = 0; attempt < 3 && !(await placed()); attempt++) {
      if (attempt > 0) {
        await he(() => window.__kernwacht.session.selectBuild('lanebarricade'));
        await page.waitForTimeout(300);
      }
      const p = await cellPoint(page, c.x, c.y);
      await page.mouse.move(p.x, p.y);
      await page.waitForTimeout(150);
      await page.mouse.click(p.x, p.y);
      await page.waitForTimeout(400);
    }
    if (!(await placed())) {
      const p = await cellPoint(page, c.x, c.y);
      const d = await he(([pt, cell]) => { const app = window.__kernwacht; const ses = app.session; const r = app.canvas.getBoundingClientRect(); const el = document.elementFromPoint(pt.x, pt.y); const pk = app.view.pickCell(pt.x - r.left, pt.y - r.top, ses.sim.s.map); return { def: ses.ui.buildDef, hover: ses.ui.hoverCell, prev: ses.ui.preview ? { ok: ses.ui.preview.ok, reason: ses.ui.preview.reason } : null, pick: pk, cell, el: el ? (el.className || el.tagName) : null, chk: ses.sim.canBuild('lanebarricade', cell.x, cell.y), paused: ses.sim.s.paused, phase: ses.sim.s.phase, modal: ses.hud.hasModal(), sel: ses.ui.selectedTower, hero: ses.hv.selectedHero }; }, [p, c]);
      throw new Error(`Lane-Barrikade nicht gesetzt ${JSON.stringify(d)}`);
    }
    await page.screenshot({ path: `${OUT}/desktop-44-lane-barricade.png` });
    assert.equal(await page.textContent('[data-testid=tower-lanebarricade] .tc'), '1/2');
    await page.keyboard.press('Escape');
  });

  await step('3.10: Karteneditor – Karte exportieren, importieren und im Helden-Endlos spielen', async () => {
    await he(() => window.__kernwacht.go('menu'));
    await page.click('[data-testid=editor]');
    await page.waitForSelector('[data-testid=editor-canvas]');
    await page.click('[data-testid=export]');
    const code = await page.inputValue('[data-testid=map-code]');
    assert.ok(code.length > 20, 'Kartencode exportiert');
    await page.fill('[data-testid=map-code]', code);
    await page.click('[data-testid=import]');
    await page.screenshot({ path: `${OUT}/desktop-45-editor.png` });
    assert.equal(await page.isDisabled('[data-testid=editor-play]'), false);
    await page.click('[data-testid=editor-play]');
    await page.waitForSelector('[data-testid=map-select]');
    assert.equal(await page.inputValue('[data-testid=map-select]'), 'editor');
    assert.match(await page.textContent('[data-testid=map-hint]'), /Eigene Karte/);
    await page.click('[data-testid=start-hero]');
    await page.waitForSelector('[data-testid=champion]');
    const info = await he(() => { const s = window.__kernwacht.session.sim.s; return { mode: s.mode, w: s.map.width, h: s.map.height, shop: s.he.shop.x >= 0 }; });
    assert.equal(JSON.stringify(info), JSON.stringify({ mode: 'heroEndless', w: 22, h: 14, shop: true }));
  });

  await step('3.13: Kino-Grafik – Tiefenunschärfe ab Mittel, abschaltbar, Glas-Oberfläche', async () => {
    await he(() => { const a = window.__kernwacht; a.save.settings.quality = 'medium'; a.applySettings(); });
    await page.waitForTimeout(600);
    const q1 = await he(() => ({ ...window.__kernwacht.renderer.quality, calls: window.__kernwacht.renderer.drawCalls }));
    assert.equal(q1.dof, true, 'Tiefenunschärfe auf Mittel aktiv');
    assert.ok(q1.calls > 10, `Zeichenaufrufe ${q1.calls}`);
    await page.screenshot({ path: `${OUT}/desktop-46-cinematic.png` });
    await he(() => window.__kernwacht.go('settings'));
    await page.waitForSelector('[data-testid=set-cinematic]');
    assert.equal(await page.isChecked('[data-testid=set-cinematic]'), true);
    await page.click('[data-testid=set-cinematic]');
    assert.equal(await he(() => window.__kernwacht.renderer.quality.dof), false, 'abgeschaltet');
    await page.click('[data-testid=set-cinematic]');
    assert.equal(await he(() => window.__kernwacht.renderer.quality.dof), true);
    await he(() => { const a = window.__kernwacht; a.save.settings.quality = 'low'; a.applySettings(); });
    assert.equal(await he(() => window.__kernwacht.renderer.quality.dof), false, 'Niedrig ohne Unschärfe');
    const bf = await page.evaluate(() => getComputedStyle(document.querySelector('.menu-buttons') ?? document.querySelector('.screen-inner') ?? document.body).backdropFilter);
    assert.match(bf, /blur/, 'Glasflächen mit Unschärfe');
  });

  await step('Keine Laufzeitfehler (2.6/2.7)', async () => {
    assert.equal(errors.length, 0, errors.join(' | '));
  });
  await ctx.close();
}

// ------------------------------------------------------------------ Smartphone (Touch)
if (!process.env.E2E_FRESH_ONLY) {
  const { ctx, page, errors } = await newPage({ viewport: { width: 844, height: 390 }, hasTouch: true, isMobile: true, deviceScaleFactor: 2 });
  await page.evaluate(() => { window.__kernwacht.save.settings.tutorialSeen = true; });

  await step('Mobil quer: Helden-Endlos starten per Tippen', async () => {
    await page.tap('[data-testid=hero-endless]');
    await page.tap('[data-testid=start-hero]');
    await page.waitForSelector('[data-testid=game]');
    await page.evaluate(() => { const s = window.__kernwacht.session.sim.s; s.gold = 5000; s.he.autoTimer = 999; });
    await page.waitForTimeout(400);
    await page.screenshot({ path: `${OUT}/mobile-01-landscape.png` });
  });

  await step('Mobil: Turm antippen, Feld antippen (Vorschau), bestätigen', async () => {
    await page.tap('[data-testid=tower-ranger]');
    const c = await findBuildCell(page, 'ranger');
    const p = await cellPoint(page, c.x, c.y);
    await page.touchscreen.tap(p.x, p.y);
    await page.waitForSelector('[data-testid=confirm-build]', { timeout: 3000 });
    await page.screenshot({ path: `${OUT}/mobile-02-confirm.png` });
    assert.equal((await state(page)).towers, 0, 'erstes Tippen baut noch nicht');
    await page.tap('[data-testid=confirm-build]');
    await page.waitForTimeout(150);
    assert.equal((await state(page)).towers, 1);
    // Zweites Verfahren: doppeltes Tippen auf dasselbe Feld
    await page.tap('[data-testid=tower-ranger]');
    const c2 = await findBuildCell(page, 'ranger');
    const p2 = await cellPoint(page, c2.x, c2.y);
    await page.touchscreen.tap(p2.x, p2.y);
    await page.waitForTimeout(120);
    await page.touchscreen.tap(p2.x, p2.y);
    await page.waitForTimeout(150);
    assert.equal((await state(page)).towers, 2);
  });

  await step('Mobil: Turm antippen öffnet Details, Welle per Tippen starten', async () => {
    const t = await page.evaluate(() => { const t = window.__kernwacht.session.sim.s.towers[0]; return { x: t.cx, y: t.cy }; });
    const p = await cellPoint(page, t.x, t.y);
    await page.touchscreen.tap(p.x, p.y);
    await page.waitForSelector('[data-testid=upgrade]');
    await page.screenshot({ path: `${OUT}/mobile-03-panel.png` });
    await page.tap('[data-testid=start-wave]');
    await page.waitForTimeout(300);
    assert.equal((await state(page)).phase, 'combat');
  });

  await step('Mobil hoch: Layout reagiert, Bedienung bleibt möglich', async () => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.waitForTimeout(500);
    await page.screenshot({ path: `${OUT}/mobile-04-portrait.png` });
    const hint = await page.isVisible('.portrait-hint');
    assert.ok(hint, 'Hinweis auf Querformat sichtbar');
    const bar = await page.$('[data-testid=tower-ember]');
    const box = await bar.boundingBox();
    assert.ok(box && box.width >= 44 && box.height >= 44, 'Mindestgröße der Bedienelemente');
    await page.tap('[data-testid=pause]');
    await page.waitForSelector('[data-testid=pause-menu]');
  });

  await step('Keine Laufzeitfehler auf dem Smartphone', async () => {
    assert.equal(errors.length, 0, errors.join(' | '));
  });
  await ctx.close();
}

await browser.close();
server.close();
console.log(`\nE2E-Ergebnis (Produktionsbuild):\n${results.join('\n')}\n\n${failed === 0 ? 'Alle Prüfungen bestanden.' : `${failed} Prüfung(en) fehlgeschlagen.`}`);
process.exit(failed === 0 ? 0 : 1);
