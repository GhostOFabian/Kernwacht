import type { GameApplication } from '../app/GameApplication.ts';
import { BIOME_ORDER, BIOMES } from '../config/biomes.ts';
import { Cell, type BiomeId, type MapDefinition, type MapVariant } from '../core/types.ts';
import { computeField, INF, tracePath } from '../sim/Pathfinding.ts';
import { generateWorld, makeSeed, MAP_VERSION, validateMap } from '../sim/WorldGenerator.ts';
import { clear, h } from '../ui/dom.ts';
import { drawMiniMap } from '../ui/MiniMap.ts';
import { copyText, seg, shell } from '../ui/screens/common.ts';
import { decodeMap, encodeMap, finalizeEditorMap } from './mapCode.ts';

// Lokaler, begrenzter Karteneditor: Start/Ziel, Wege, Bauflächen, Hindernisse, Ressourcen,
// Validierung, Export/Import als Kartencode und lokale Speicherung. Ungültige Karten sind nicht spielbar.

type Tool = 'path' | 'open' | 'blocked' | 'nobuild' | 'spawn' | 'goal' | 'vein' | 'ley';

const TOOLS: [Tool, string, string][] = [
  ['path', '▬', 'Weg'], ['open', '□', 'Baufläche'], ['blocked', '▣', 'Hindernis'], ['nobuild', '⊘', 'Kein Bau'],
  ['spawn', '▶', 'Start'], ['goal', '◆', 'Ziel'], ['vein', '●', 'Goldader'], ['ley', '○', 'Leylinie'],
];

const DIMS: Record<MapVariant, [number, number]> = { classic: [22, 14], mazing: [20, 14] };

function blankMap(variant: MapVariant, biome: BiomeId): MapDefinition {
  const [w, hh] = DIMS[variant];
  const cells = new Array<number>(w * hh).fill(Cell.Open);
  const my = Math.floor(hh / 2);
  if (variant === 'classic') for (let x = 0; x < w; x++) cells[my * w + x] = Cell.Path;
  cells[my * w] = Cell.Spawn;
  cells[my * w + w - 1] = Cell.Goal;
  return finalizeEditorMap({
    version: MAP_VERSION, width: w, height: hh, variant, biome, cells, heights: [], veins: [], ley: [], decor: [],
    spawn: { x: 0, y: my }, goal: { x: w - 1, y: my }, terrainEvent: null, fallback: false, name: 'Eigene Karte',
  });
}

export function renderEditor(app: GameApplication): HTMLElement {
  let map = blankMap('classic', 'forest');
  let tool: Tool = 'path';
  let name = 'Meine Karte';
  const canvas = h('canvas', { class: 'minimap editor-canvas', 'aria-label': 'Editorfläche', 'data-testid': 'editor-canvas' });
  const status = h('div', { class: 'editor-status', 'aria-live': 'polite' });
  const saved = h('div', { class: 'saved-maps' });
  const codeBox = h('textarea', { class: 'code', rows: '3', 'aria-label': 'Kartencode', placeholder: 'Kartencode hier einfügen…', 'data-testid': 'map-code' });
  const playBtn = h('button', { class: 'btn primary', 'data-testid': 'editor-play', onclick: () => play() }, '▶ Mit Helden spielen');
  let cellSize = 16;

  const redraw = (): void => {
    finalizeEditorMap(map);
    const v = validateMap(map);
    const f = computeField(map, null);
    const route = f[map.spawn.y * map.width + map.spawn.x] < INF ? tracePath(map, f, map.spawn) : null;
    cellSize = drawMiniMap(canvas, map, { route, grid: true });
    clear(status);
    status.append(v.ok
      ? h('div', { class: 'notice good' }, `✔ Gültig und spielbar · Weglänge ${Math.round(v.pathLength)} · Bauflächen ${v.buildable}`)
      : h('div', { class: 'notice warn' }, h('b', {}, '✖ Nicht spielbar: '), v.errors.join(' · ')));
    playBtn.disabled = !v.ok;
  };

  const setCell = (x: number, y: number): void => {
    if (x < 0 || y < 0 || x >= map.width || y >= map.height) return;
    const w = map.width;
    const i = y * w + x;
    const cur = map.cells[i];
    const openCode = map.variant === 'classic' ? Cell.Path : Cell.Open;
    const replaceSingle = (code: number): void => {
      for (let k = 0; k < map.cells.length; k++) if (map.cells[k] === code) map.cells[k] = openCode;
      map.cells[i] = code;
    };
    switch (tool) {
      case 'path': if (map.variant === 'classic' && cur !== Cell.Spawn && cur !== Cell.Goal) map.cells[i] = Cell.Path; break;
      case 'open': if (cur !== Cell.Spawn && cur !== Cell.Goal) map.cells[i] = Cell.Open; break;
      case 'blocked': if (cur !== Cell.Spawn && cur !== Cell.Goal) map.cells[i] = Cell.Blocked; break;
      case 'nobuild': if (cur !== Cell.Spawn && cur !== Cell.Goal) map.cells[i] = Cell.NoBuild; break;
      case 'spawn': replaceSingle(Cell.Spawn); break;
      case 'goal': replaceSingle(Cell.Goal); break;
      case 'vein':
        if (cur === Cell.Open) map.veins = map.veins.includes(i) ? map.veins.filter((v) => v !== i) : [...map.veins, i];
        break;
      case 'ley':
        if (cur === Cell.Open) map.ley = map.ley.includes(i) ? map.ley.filter((v) => v !== i) : [...map.ley, i];
        break;
    }
  };

  let painting = false;
  let lastCell = -1;
  const paintAt = (e: PointerEvent): void => {
    const r = canvas.getBoundingClientRect();
    const x = Math.floor((e.clientX - r.left) / cellSize);
    const y = Math.floor((e.clientY - r.top) / cellSize);
    const idx = y * map.width + x;
    if (idx === lastCell) return;
    lastCell = idx;
    setCell(x, y);
    redraw();
  };
  canvas.addEventListener('pointerdown', (e) => {
    painting = true;
    lastCell = -1;
    canvas.setPointerCapture(e.pointerId);
    paintAt(e);
  });
  canvas.addEventListener('pointermove', (e) => {
    if (painting && tool !== 'vein' && tool !== 'ley' && tool !== 'spawn' && tool !== 'goal') paintAt(e);
  });
  canvas.addEventListener('pointerup', () => { painting = false; });
  canvas.addEventListener('pointercancel', () => { painting = false; });

  const play = (): void => {
    finalizeEditorMap(map);
    if (!validateMap(map).ok) {
      app.toast('Ungültige Karten sind nicht spielbar.', 'warn');
      return;
    }
    // 3.10: eigene Karten werden im Helden-Endlos gespielt – Held und Ausrüstung wählst du im Startbildschirm
    app.go('heroSetup', { heroMap: { ...map, name: name || 'Eigene Karte' } });
  };

  const loadCode = (code: string): boolean => {
    const r = decodeMap(code);
    if (!r.map) {
      app.toast(`Kartencode ungültig: ${r.error ?? ''}`, 'warn', 4000);
      return false;
    }
    map = r.map;
    rebuildOptions();
    redraw();
    if (r.validation && !r.validation.ok) app.toast('Karte geladen, ist aber noch nicht spielbar – siehe Prüfung.', 'warn');
    return true;
  };

  const renderSaved = (): void => {
    clear(saved);
    const list = app.save.customMaps;
    if (!list.length) {
      saved.append(h('p', { class: 'muted small' }, 'Noch keine Karten gespeichert.'));
      return;
    }
    saved.append(h('ul', {}, list.map((m, k) => {
      const ok = decodeMap(m.code).validation?.ok ?? false;
      return h('li', {},
        h('span', {}, `${m.name} ${ok ? '✔' : '✖ ungültig'}`),
        h('button', { class: 'btn sm', onclick: () => { name = m.name; nameInput.value = m.name; loadCode(m.code); } }, 'Laden'),
        h('button', { class: 'btn sm', onclick: () => { app.save.customMaps.splice(k, 1); app.persist(); renderSaved(); } }, 'Löschen'));
    })));
  };

  const nameInput = h('input', { type: 'text', value: name, maxlength: '40', 'aria-label': 'Kartenname', oninput: (e: Event) => { name = (e.target as HTMLInputElement).value; } });
  const templateSeed = h('input', { type: 'text', value: 'VORLAGE-1', maxlength: '32', 'aria-label': 'Seed für Vorlage', id: 'template-seed' });
  const optionsBox = h('div', { class: 'options' });
  const rebuildOptions = (): void => {
    clear(optionsBox);
    optionsBox.append(
      h('div', { class: 'field' }, h('span', {}, 'Variante'),
        seg<MapVariant>([['classic', 'Classic'], ['mazing', 'Mazing']], map.variant, (v) => {
          map = blankMap(v, map.biome);
          if (v === 'mazing' && tool === 'path') tool = 'blocked';
          rebuildOptions();
          redraw();
        }, 'Variante')),
      h('label', { class: 'field' }, h('span', {}, 'Biom'),
        h('select', { 'aria-label': 'Biom', onchange: (e: Event) => { map.biome = (e.target as HTMLSelectElement).value as BiomeId; redraw(); } },
          BIOME_ORDER.map((b) => h('option', { value: b, selected: map.biome === b }, BIOMES[b].name)))),
      h('div', { class: 'field' }, h('span', {}, 'Werkzeug'),
        h('div', { class: 'tools', role: 'radiogroup', 'aria-label': 'Werkzeug' }, TOOLS.filter(([t]) => !(t === 'path' && map.variant === 'mazing')).map(([t, ic, label]) =>
          h('button', { class: `btn sm${tool === t ? ' on' : ''}`, role: 'radio', 'aria-checked': String(tool === t), 'data-tool': t, onclick: () => { tool = t; rebuildOptions(); } }, `${ic} ${label}`)))),
      h('p', { class: 'muted small' }, map.variant === 'classic'
        ? 'Classic: Male einen durchgehenden Weg vom Start (▶) zum Ziel (◆). Bauflächen liegen daneben.'
        : 'Mazing: Gegner laufen frei über Bauflächen. Hindernisse formen das Gelände; Spieler bauen das Labyrinth.'),
      h('div', { class: 'row tight' },
        h('button', { class: 'btn sm', onclick: () => { map = blankMap(map.variant, map.biome); redraw(); } }, 'Leeren'),
        templateSeed,
        h('button', { class: 'btn sm', onclick: () => {
          const seedText = templateSeed.value.trim();
          if (!seedText) {
            app.toast('Bitte einen Seed für die Vorlage eingeben.', 'warn');
            return;
          }
          const g = generateWorld({ seed: makeSeed(seedText), variant: map.variant, biome: map.biome });
          map = finalizeEditorMap({ ...g, heights: [], decor: [], name: 'Eigene Karte' });
          redraw();
        } }, 'Vorlage aus Seed')),
    );
  };

  rebuildOptions();
  const el = shell(app, 'Karteneditor', 'menu',
    h('p', { class: 'muted small' }, 'Lokaler Karteneditor für Helden-Endlos: Karten werden nur auf diesem Gerät gespeichert und als Kartencode geteilt. Gespeicherte Karten wählst du im Helden-Endlos-Startbildschirm unter „Karte“. Ungültige Karten können nicht gespielt werden.'),
    h('div', { class: 'editor' },
      h('div', { class: 'editor-main' }, canvas, status),
      h('div', { class: 'editor-side' },
        optionsBox,
        h('label', { class: 'field' }, h('span', {}, 'Name'), nameInput),
        h('div', { class: 'row tight' },
          playBtn,
          h('button', { class: 'btn', onclick: () => {
            finalizeEditorMap(map);
            const code = encodeMap(map);
            const list = app.save.customMaps;
            const at = list.findIndex((m) => m.name === name);
            if (at >= 0) list[at] = { name, code };
            else list.unshift({ name, code });
            app.save.customMaps = list.slice(0, 30);
            app.persist();
            renderSaved();
            app.toast(`„${name}“ gespeichert`);
          } }, '💾 Speichern'),
          h('button', { class: 'btn', 'data-testid': 'export', onclick: () => { finalizeEditorMap(map); const c = encodeMap(map); codeBox.value = c; void copyText(app, c); } }, '⧉ Exportieren')),
        h('label', { class: 'field' }, h('span', {}, 'Kartencode'), codeBox),
        h('button', { class: 'btn', 'data-testid': 'import', onclick: () => loadCode(codeBox.value) }, '⤓ Code laden'),
        h('h3', {}, 'Gespeicherte Karten'), saved)));
  requestAnimationFrame(() => {
    redraw();
    renderSaved();
  });
  window.addEventListener('resize', redraw, { once: true });
  return el;
}
