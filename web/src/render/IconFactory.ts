import { BOSSES, ENEMIES } from '../config/enemies.ts';
import { HEROES } from '../config/heroes.ts';
import { TOWERS } from '../config/towers.ts';
import type { BossId, EnemyId, HeroId, TowerId } from '../core/types.ts';
import { lookAt, mat4, mixColor, multiply, ortho } from './math3d.ts';
import { BOSS_HEIGHT, BOSS_MODELS, ENEMY_MODELS, HERO_MODELS, levelParts, specParts, TOWER_MODELS, TOWER_TOP, type Part } from './models.ts';
import { Renderer, SHAPE, type Grade, type Lighting } from './Renderer.ts';

// Rendert die 3D-Modelle einmalig in kleine Bilder (Turmleiste, Kodex, Heldenkarten, Vorschau).
// Fällt ohne WebGL2 auf null zurück – die UI zeigt dann die Text-Symbole.

const LIGHT: Lighting = {
  light: [-0.5, 0.8, 0.35],
  lightColor: [1.0, 0.96, 0.9],
  sky: [0.45, 0.42, 0.6],
  ground: [0.16, 0.13, 0.22],
  fog: [0.1, 0.08, 0.16],
  fogCenter: [0, 0],
  fogStart: 100,
  fogEnd: 120,
  clear: [0.13, 0.1, 0.21],
};

const GRADE: Grade = { tint: [1, 1, 1], saturation: 1.1, contrast: 1.05, vignette: 1.4, bloom: 0, flash: 0 };

export class IconFactory {
  private r: Renderer | null = null;
  private canvas: HTMLCanvasElement | null = null;
  private cache = new Map<string, string | null>();
  private failed = false;
  stoneColor = 0x8c8678;

  private ensure(): Renderer | null {
    if (this.r || this.failed) return this.r;
    try {
      const c = document.createElement('canvas');
      c.width = 128;
      c.height = 128;
      this.canvas = c;
      this.r = new Renderer(c, { preserve: true });
      this.r.setQuality({ msaa: 4, shadowSize: 0, bloom: false });
    } catch {
      this.failed = true;
      this.r = null;
    }
    return this.r;
  }

  tower(id: TowerId, size = 96, level = 1, spec: 0 | 1 | 2 = 0): string | null {
    const def = TOWERS[id];
    const top = TOWER_TOP[id];
    const parts = [...TOWER_MODELS[id], ...(TOWERS[id].attack !== 'none' ? levelParts(level, top) : []), ...(TOWERS[id].attack !== 'none' ? specParts(spec, top) : [])];
    return this.render(`t:${id}:${size}:${level}:${spec}:${this.stoneColor}`, parts, def.color, spec === 1 ? 0xffa040 : spec === 2 ? 0x60e0ff : 0xffffff, top + (spec ? 0.5 : 0), size, -0.2);
  }

  /** Drehende Vorschau: Spritesheet aus mehreren Blickwinkeln (für CSS-Animation). */
  towerTurntable(id: TowerId, size = 160, level = 4, spec: 0 | 1 | 2 = 1, frames = 16): { url: string; frames: number; size: number } | null {
    const key = `tt:${id}:${size}:${level}:${spec}:${frames}:${this.stoneColor}`;
    const hit = this.sheets.get(key);
    if (hit !== undefined) return hit;
    const def = TOWERS[id];
    const top = TOWER_TOP[id];
    const parts = [...TOWER_MODELS[id], ...(TOWERS[id].attack !== 'none' ? levelParts(level, top) : []), ...(TOWERS[id].attack !== 'none' ? specParts(spec, top) : [])];
    const sheet = document.createElement('canvas');
    sheet.width = size * frames;
    sheet.height = size;
    const ctx = sheet.getContext('2d');
    if (!ctx) return null;
    for (let f = 0; f < frames; f++) {
      const url = this.render(`ttf:${key}:${f}`, parts, def.color, spec === 1 ? 0xffa040 : spec === 2 ? 0x60e0ff : 0xffffff, top + (spec ? 0.5 : 0), size, (f / frames) * Math.PI * 2, true);
      if (!url || !this.canvas) {
        this.sheets.set(key, null);
        return null;
      }
      ctx.drawImage(this.canvas, f * size, 0);
    }
    const out = { url: sheet.toDataURL('image/png'), frames, size };
    this.sheets.set(key, out);
    return out;
  }

  private sheets = new Map<string, { url: string; frames: number; size: number } | null>();

  enemy(id: Exclude<EnemyId, 'boss'>, size = 64): string | null {
    const def = ENEMIES[id];
    const sc = def.size * 2;
    const parts = ENEMY_MODELS[id];
    const flyLift = def.flying ? 0.4 : 0;
    return this.render(`e:${id}:${size}`, parts.map((p) => scalePart(p, sc, flyLift)), def.color, 0xffffff, sc * 1.1 + flyLift, size, 0.5);
  }

  boss(id: BossId, size = 96): string | null {
    const def = BOSSES[id];
    return this.render(`b:${id}:${size}`, BOSS_MODELS[id], def.color, 0xffffff, BOSS_HEIGHT[id], size, 0.4);
  }

  hero(id: HeroId, size = 96): string | null {
    const def = HEROES[id];
    return this.render(`h:${id}:${size}`, HERO_MODELS[id], def.color, def.accent, 1.15, size, 0.55);
  }

  private render(key: string, parts: Part[], main: number, accent: number, height: number, size: number, yaw: number, noCache = false): string | null {
    if (!noCache && this.cache.has(key)) return this.cache.get(key) ?? null;
    const r = this.ensure();
    if (!r || !this.canvas) {
      this.cache.set(key, null);
      return null;
    }
    try {
      this.canvas.width = size;
      this.canvas.height = size;
      r.resize(size, size, 1);
      const view = mat4();
      const proj = mat4();
      const vp = mat4();
      const pitch = Math.atan(1 / Math.SQRT2);
      const cyaw = Math.PI / 4 + 0.35;
      const tc: [number, number, number] = [Math.cos(pitch) * Math.sin(cyaw), Math.sin(pitch), Math.cos(pitch) * Math.cos(cyaw)];
      const cy = height * 0.48;
      const ext = Math.max(0.62, height * 0.62);
      lookAt(view, tc[0] * 20, cy + tc[1] * 20, tc[2] * 20, 0, cy, 0);
      ortho(proj, -ext, ext, -ext, ext, 0.1, 60);
      multiply(vp, proj, view);
      r.camRight = [Math.cos(cyaw), 0, -Math.sin(cyaw)];
      r.camUp = [tc[1] * r.camRight[2] - tc[2] * r.camRight[1], tc[2] * r.camRight[0] - tc[0] * r.camRight[2], tc[0] * r.camRight[1] - tc[1] * r.camRight[0]];
      r.toCam = tc;
      r.time = 0.6;
      const c = Math.cos(yaw), s = Math.sin(yaw);
      for (const p of parts) {
        const ox = p.x * c + p.z * s;
        const oz = -p.x * s + p.z * c;
        let col = p.c === -1 ? main : p.c === -2 ? accent : p.c === -3 ? this.stoneColor : p.c;
        if (p.c === -1) col = mixColor(col, 0xffffff, 0.04);
        r.add('opaque', p.m, ox, p.y, oz, p.sx, p.sy, p.sz, col, 1, (p.e ?? 0) * 0.8, yaw + (p.yaw ?? 0), p.pitch ?? 0, p.roll ?? 0, p.mat ?? 0);
        if (p.glow) r.billboard(true, SHAPE.glow, ox, p.y, oz, Math.max(p.sx, p.sy) * 1.4, p.glow, 0.4);
      }
      r.ground(true, SHAPE.glow, 0, 0.01, 0, Math.max(0.7, ext * 0.9), accent === 0xffffff ? main : accent, 0.18, 0, 0.3);
      r.render(vp, tc, LIGHT, GRADE);
      if (noCache) return 'frame';
      const url = this.canvas.toDataURL('image/png');
      this.cache.set(key, url);
      return url;
    } catch {
      this.cache.set(key, null);
      return null;
    }
  }
}

function scalePart(p: Part, s: number, lift: number): Part {
  return { ...p, x: p.x * s, y: p.y * s + lift, z: p.z * s, sx: p.sx * s, sy: p.sy * s, sz: p.sz * s };
}

/** Drehende 3D-Vorschau als CSS-animiertes Spritesheet (oder Ersatzbild). */
export function turntableEl(sheet: { url: string; frames: number; size: number } | null, fallback: HTMLElement): HTMLElement {
  if (!sheet) return fallback;
  const el = document.createElement('div');
  el.className = 'turntable';
  el.setAttribute('role', 'img');
  el.setAttribute('aria-label', '3D-Vorschau');
  el.style.setProperty('--n', String(sheet.frames));
  el.style.setProperty('--s', `${sheet.size}px`);
  el.style.backgroundImage = `url(${sheet.url})`;
  return el;
}

/** Bild-Element für ein Symbol – mit Text-Ersatz, falls kein Bild erzeugt werden konnte. */
export function iconImg(url: string | null, fallback: string, cls = 'mi'): HTMLElement {
  if (url) {
    const img = document.createElement('img');
    img.src = url;
    img.alt = '';
    img.className = cls;
    img.draggable = false;
    return img;
  }
  const span = document.createElement('span');
  span.className = `${cls} fallback`;
  span.textContent = fallback;
  span.setAttribute('aria-hidden', 'true');
  return span;
}
