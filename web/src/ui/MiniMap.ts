import { BIOMES } from '../config/biomes.ts';
import { Cell, type GridPos, type MapDefinition } from '../core/types.ts';

// 2D-Draufsicht einer Karte (Welt-Vorschau, Karteneditor). Formen zusätzlich zu Farben:
// Start = Dreieck, Ziel = Raute, Goldader = Kreis, Leylinie = Ring, Hindernis = dunkles Quadrat mit Kreuz.

export interface MiniMapOptions {
  route?: GridPos[] | null;
  highlight?: GridPos | null;
  grid?: boolean;
}

function hex(c: number): string {
  return '#' + c.toString(16).padStart(6, '0');
}

export function drawMiniMap(canvas: HTMLCanvasElement, map: MapDefinition, opts: MiniMapOptions = {}): number {
  const ctx = canvas.getContext('2d');
  if (!ctx) return 0;
  const dpr = Math.min(2, window.devicePixelRatio || 1);
  const cssW = canvas.clientWidth || 320;
  const cell = Math.max(4, Math.floor(cssW / map.width));
  const w = cell * map.width;
  const h = cell * map.height;
  canvas.width = w * dpr;
  canvas.height = h * dpr;
  canvas.style.height = `${h}px`;
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  const pal = BIOMES[map.biome].palette;
  for (let y = 0; y < map.height; y++) {
    for (let x = 0; x < map.width; x++) {
      const i = y * map.width + x;
      const c = map.cells[i];
      let col = (x + y) % 2 ? pal.ground : pal.ground2;
      if (c === Cell.Path || c === Cell.Spawn || c === Cell.Goal) col = pal.path;
      else if (c === Cell.Blocked) col = pal.obstacle;
      else if (c === Cell.NoBuild) col = pal.cliff;
      else if (map.heights[i] >= 2) col = pal.high;
      ctx.fillStyle = hex(col);
      ctx.fillRect(x * cell, y * cell, cell, cell);
      if (c === Cell.Blocked) {
        ctx.strokeStyle = 'rgba(0,0,0,0.45)';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(x * cell + 2, y * cell + 2);
        ctx.lineTo(x * cell + cell - 2, y * cell + cell - 2);
        ctx.moveTo(x * cell + cell - 2, y * cell + 2);
        ctx.lineTo(x * cell + 2, y * cell + cell - 2);
        ctx.stroke();
      }
      if (c === Cell.NoBuild) {
        ctx.fillStyle = 'rgba(255,255,255,0.18)';
        ctx.fillRect(x * cell + cell * 0.4, y * cell + cell * 0.4, cell * 0.2, cell * 0.2);
      }
    }
  }
  if (opts.grid) {
    ctx.strokeStyle = 'rgba(0,0,0,0.18)';
    ctx.lineWidth = 1;
    for (let x = 0; x <= map.width; x++) {
      ctx.beginPath();
      ctx.moveTo(x * cell + 0.5, 0);
      ctx.lineTo(x * cell + 0.5, h);
      ctx.stroke();
    }
    for (let y = 0; y <= map.height; y++) {
      ctx.beginPath();
      ctx.moveTo(0, y * cell + 0.5);
      ctx.lineTo(w, y * cell + 0.5);
      ctx.stroke();
    }
  }
  for (const i of map.veins) {
    ctx.fillStyle = '#f0c040';
    ctx.beginPath();
    ctx.arc((i % map.width + 0.5) * cell, (Math.floor(i / map.width) + 0.5) * cell, cell * 0.28, 0, Math.PI * 2);
    ctx.fill();
  }
  for (const i of map.deposits ?? []) {
    ctx.fillStyle = '#7ae0ff';
    ctx.beginPath();
    ctx.arc((i % map.width + 0.5) * cell, (Math.floor(i / map.width) + 0.5) * cell, cell * 0.28, 0, Math.PI * 2);
    ctx.fill();
  }
  for (const i of map.ley) {
    ctx.strokeStyle = '#c070ff';
    ctx.lineWidth = Math.max(1.5, cell * 0.12);
    ctx.beginPath();
    ctx.arc((i % map.width + 0.5) * cell, (Math.floor(i / map.width) + 0.5) * cell, cell * 0.3, 0, Math.PI * 2);
    ctx.stroke();
  }
  if (opts.route && opts.route.length > 1) {
    ctx.strokeStyle = 'rgba(255,240,160,0.85)';
    ctx.lineWidth = Math.max(1.5, cell * 0.14);
    ctx.setLineDash([cell * 0.3, cell * 0.25]);
    ctx.beginPath();
    opts.route.forEach((p, k) => {
      const px = (p.x + 0.5) * cell;
      const py = (p.y + 0.5) * cell;
      if (k === 0) ctx.moveTo(px, py);
      else ctx.lineTo(px, py);
    });
    ctx.stroke();
    ctx.setLineDash([]);
  }
  // Start (Dreieck) und Ziel (Raute)
  const s = map.spawn;
  ctx.fillStyle = '#c060ff';
  ctx.beginPath();
  ctx.moveTo((s.x + 0.15) * cell, (s.y + 0.15) * cell);
  ctx.lineTo((s.x + 0.9) * cell, (s.y + 0.5) * cell);
  ctx.lineTo((s.x + 0.15) * cell, (s.y + 0.85) * cell);
  ctx.closePath();
  ctx.fill();
  const g = map.goal;
  ctx.fillStyle = '#60f0ff';
  ctx.beginPath();
  ctx.moveTo((g.x + 0.5) * cell, (g.y + 0.08) * cell);
  ctx.lineTo((g.x + 0.92) * cell, (g.y + 0.5) * cell);
  ctx.lineTo((g.x + 0.5) * cell, (g.y + 0.92) * cell);
  ctx.lineTo((g.x + 0.08) * cell, (g.y + 0.5) * cell);
  ctx.closePath();
  ctx.fill();
  if (opts.highlight) {
    ctx.strokeStyle = '#ffffff';
    ctx.lineWidth = 2;
    ctx.strokeRect(opts.highlight.x * cell + 1, opts.highlight.y * cell + 1, cell - 2, cell - 2);
  }
  return cell;
}
