import { mixColor } from './math3d.ts';
import { SHAPE, type MeshId, type Renderer, type SpriteShape } from './Renderer.ts';

// Effektsystem: weiche Sprite-Partikel (Glühen, Funken, Rauch, Ringe, Sterne), Trümmerstücke,
// verästelte Blitze, Strahlen und Bodendekale. Rein visuell, nutzt Math.random (nicht deterministisch).

export interface SpriteParticle {
  shape: SpriteShape;
  add: boolean;
  x: number; y: number; z: number;
  vx: number; vy: number; vz: number;
  life: number; max: number;
  s0: number; s1: number;
  c0: number; c1: number;
  a: number;
  grav: number;
  drag: number;
  rot: number; vrot: number;
  stretch: number;      // >0: entlang der Bildschirmgeschwindigkeit gestreckt
  flat: boolean;        // liegt auf dem Boden
  top: boolean;         // über allem (ohne Tiefentest)
  fadeIn: number;
  seed: number;
}

interface Debris {
  mesh: MeshId;
  x: number; y: number; z: number;
  vx: number; vy: number; vz: number;
  life: number; max: number;
  s: number; color: number; emit: number;
  yaw: number; vyaw: number; floor: number;
}

interface Bolt { pts: number[]; life: number; max: number; color: number; width: number }

interface Decal { x: number; y: number; z: number; size: number; color: number; a: number; life: number; max: number; shape: SpriteShape; rot: number; add: boolean }

const R = Math.random;

export class Fx {
  sp: SpriteParticle[] = [];
  private debris: Debris[] = [];
  private bolts: Bolt[] = [];
  private decals: Decal[] = [];
  cap = 1600;
  /** 0 = aus (Bewegung reduzieren) … 1 = voll. */
  intensity = 1;

  clear(): void {
    this.sp = [];
    this.debris = [];
    this.bolts = [];
    this.decals = [];
  }

  emit(p: Partial<SpriteParticle> & { x: number; y: number; z: number; life: number; c0: number }): void {
    if (this.sp.length >= this.cap) return;
    this.sp.push({
      shape: SHAPE.glow, add: true, vx: 0, vy: 0, vz: 0, max: p.life, s0: 0.3, s1: 0.3, c1: p.c0, a: 1,
      grav: 0, drag: 0, rot: 0, vrot: 0, stretch: 0, flat: false, top: false, fadeIn: 0, seed: R() * 100, ...p,
    } as SpriteParticle);
  }

  /** Funkenregen. */
  sparks(x: number, y: number, z: number, color: number, n: number, speed = 1, size = 0.12): void {
    for (let i = 0; i < n; i++) {
      const a = R() * Math.PI * 2;
      const v = (1 + R() * 2.5) * speed;
      this.emit({
        shape: SHAPE.spark, x, y, z, vx: Math.cos(a) * v, vy: (1 + R() * 2.5) * speed, vz: Math.sin(a) * v,
        life: 0.3 + R() * 0.35, c0: mixColor(color, 0xffffff, 0.5), c1: color, s0: size, s1: size * 0.3, grav: 9, drag: 1.5, stretch: 0.12,
      });
    }
  }

  /** Flammen mit aufsteigendem Rauch. */
  fire(x: number, y: number, z: number, scale = 1, color = 0xff7a20, smoke = true): void {
    this.emit({
      x: x + (R() - 0.5) * 0.2 * scale, y, z: z + (R() - 0.5) * 0.2 * scale, vy: 0.9 + R() * 0.8, vx: (R() - 0.5) * 0.3, vz: (R() - 0.5) * 0.3,
      life: 0.35 + R() * 0.3, c0: mixColor(color, 0xfff0a0, 0.6), c1: mixColor(color, 0x801000, 0.5), s0: 0.24 * scale, s1: 0.05 * scale, fadeIn: 0.1,
    });
    if (smoke && R() < 0.45) {
      this.emit({
        shape: SHAPE.smoke, add: false, x, y: y + 0.3 * scale, z, vy: 0.5 + R() * 0.4, vx: (R() - 0.5) * 0.25, vz: (R() - 0.5) * 0.25,
        life: 0.9 + R() * 0.6, c0: 0x3a3430, c1: 0x6a6460, a: 0.32, s0: 0.15 * scale, s1: 0.55 * scale, fadeIn: 0.2, vrot: (R() - 0.5),
      });
    }
  }

  smoke(x: number, y: number, z: number, size: number, color = 0x5a5450, alpha = 0.4, life = 1.2): void {
    this.emit({
      shape: SHAPE.smoke, add: false, x, y, z, vy: 0.35 + R() * 0.3, vx: (R() - 0.5) * 0.4, vz: (R() - 0.5) * 0.4,
      life: life * (0.7 + R() * 0.6), c0: color, c1: mixColor(color, 0x9a9490, 0.4), a: alpha, s0: size * 0.5, s1: size * 1.4, fadeIn: 0.15, vrot: (R() - 0.5) * 0.8, drag: 0.8,
    });
  }

  glow(x: number, y: number, z: number, size: number, color: number, life = 0.25, alpha = 1): void {
    this.emit({ x, y, z, life, c0: color, s0: size, s1: size * 1.3, a: alpha });
  }

  /** Bodenring (Schockwelle). */
  ring(x: number, y: number, z: number, r: number, color: number, life = 0.45, width = 0.12, from = 0.15): void {
    this.emit({ shape: SHAPE.ring, flat: true, x, y: y + 0.03, z, life, c0: color, s0: Math.max(0.05, r * from), s1: r, a: 0.95, seed: width });
  }

  /** Aufrechter Lichtstrahl (Einsatz, Fusion). */
  pillar(x: number, y: number, z: number, height: number, color: number, life = 0.6, width = 0.3): void {
    this.bolts.push({ pts: [x, y, z, x, y + height, z], life, max: life, color, width: -width });
  }

  explosion(x: number, y: number, z: number, r: number, color = 0xff7a20, big = false): void {
    const k = this.intensity;
    this.glow(x, y + 0.3, z, r * 1.6, mixColor(color, 0xffffff, 0.4), 0.22);
    this.ring(x, y, z, r * 1.2, color, 0.45, 0.2);
    const n = Math.round((big ? 22 : 10) * k);
    for (let i = 0; i < n; i++) {
      const a = R() * Math.PI * 2;
      const v = R() * r * 2.2;
      this.emit({
        x: x + Math.cos(a) * 0.1, y: y + 0.2, z: z + Math.sin(a) * 0.1, vx: Math.cos(a) * v, vy: 0.8 + R() * 2.2, vz: Math.sin(a) * v,
        life: 0.35 + R() * 0.35, c0: 0xfff0a0, c1: mixColor(color, 0x400800, 0.4), s0: 0.32 * r, s1: 0.06, drag: 3,
      });
    }
    for (let i = 0; i < Math.round((big ? 8 : 4) * k); i++) this.smoke(x + (R() - 0.5) * r, y + 0.3, z + (R() - 0.5) * r, 0.6 * r, 0x403830, 0.45, 1.6);
    this.sparks(x, y + 0.2, z, color, Math.round((big ? 14 : 6) * k), 1.2);
    if (big) this.debrisBurst(x, y, z, 0x4a4038, 6);
    this.decal(x, y, z, r * 1.1, 0x14100c, 0.5, 6);
  }

  debrisBurst(x: number, y: number, z: number, color: number, n: number, emit = 0): void {
    for (let i = 0; i < n * this.intensity; i++) {
      const a = R() * Math.PI * 2;
      const v = 1 + R() * 2;
      this.debris.push({
        mesh: R() < 0.5 ? 'rbox' : 'gem', x, y: y + 0.2, z, vx: Math.cos(a) * v, vy: 2 + R() * 3, vz: Math.sin(a) * v,
        life: 0.9 + R() * 0.5, max: 1.4, s: 0.08 + R() * 0.1, color, emit, yaw: R() * 6, vyaw: (R() - 0.5) * 12, floor: y,
      });
    }
    if (this.debris.length > 220) this.debris.splice(0, this.debris.length - 220);
  }

  decal(x: number, y: number, z: number, size: number, color: number, alpha: number, life: number, shape: SpriteShape = SHAPE.smoke, add = false): void {
    this.decals.push({ x, y: y + 0.015, z, size, color, a: alpha, life, max: life, shape, rot: R() * 6, add });
    if (this.decals.length > 90) this.decals.shift();
  }

  /** Verästelter Blitz. */
  bolt(x0: number, y0: number, z0: number, x1: number, y1: number, z1: number, color: number, width = 0.06, life = 0.18, branches = 1): void {
    const main = jag(x0, y0, z0, x1, y1, z1);
    this.bolts.push({ pts: main, life, max: life, color, width });
    for (let b = 0; b < branches; b++) {
      if (main.length < 12) break;
      const idx = 1 + Math.floor(R() * (main.length / 3 - 2));
      const bx = main[idx * 3], by = main[idx * 3 + 1], bz = main[idx * 3 + 2];
      const dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
      const len = Math.hypot(dx, dy, dz) * (0.25 + R() * 0.25);
      const a = R() * Math.PI * 2;
      const ex = bx + (dx / (Math.hypot(dx, dz) || 1)) * len * 0.5 + Math.cos(a) * len * 0.6;
      const ez = bz + (dz / (Math.hypot(dx, dz) || 1)) * len * 0.5 + Math.sin(a) * len * 0.6;
      this.bolts.push({ pts: jag(bx, by, bz, ex, by - len * 0.3, ez), life: life * 0.8, max: life, color, width: width * 0.6 });
    }
    if (this.bolts.length > 120) this.bolts.splice(0, this.bolts.length - 120);
  }

  /** Gerader, ausblendender Strahl. */
  beam(x0: number, y0: number, z0: number, x1: number, y1: number, z1: number, color: number, width: number, life: number): void {
    this.bolts.push({ pts: [x0, y0, z0, x1, y1, z1], life, max: life, color, width });
  }

  update(dt: number, r: Renderer): void {
    // Dekale
    let w = 0;
    for (const d of this.decals) {
      d.life -= dt;
      if (d.life <= 0) continue;
      const f = Math.min(1, d.life / (d.max * 0.4));
      r.ground(d.add, d.shape, d.x, d.y, d.z, d.size, d.color, d.a * f, d.rot, 0.5, 0.15);
      this.decals[w++] = d;
    }
    this.decals.length = w;

    // Sprite-Partikel
    const [rx, ry, rz] = r.camRight;
    const [ux, uy, uz] = r.camUp;
    w = 0;
    for (let i = 0; i < this.sp.length; i++) {
      const p = this.sp[i];
      p.life -= dt;
      if (p.life <= 0) continue;
      if (p.drag > 0) {
        const k = Math.max(0, 1 - p.drag * dt);
        p.vx *= k; p.vy *= k; p.vz *= k;
      }
      p.vy -= p.grav * dt;
      p.x += p.vx * dt;
      p.y += p.vy * dt;
      p.z += p.vz * dt;
      if (p.grav > 0 && p.y < 0) {
        p.y = 0;
        p.vy *= -0.35;
        p.vx *= 0.6;
        p.vz *= 0.6;
      }
      p.rot += p.vrot * dt;
      const t = 1 - p.life / p.max;
      const size = p.s0 + (p.s1 - p.s0) * t;
      let a = p.a * (1 - t * t);
      if (p.fadeIn > 0 && t < p.fadeIn) a *= t / p.fadeIn;
      const col = p.c0 === p.c1 ? p.c0 : mixColor(p.c0, p.c1, t);
      if (p.flat) {
        r.ground(p.add, p.shape, p.x, p.y, p.z, size, col, a, p.rot, 0.3, p.shape === SHAPE.ring ? p.seed : 0.15);
      } else if (p.stretch > 0) {
        const sx = p.vx * rx + p.vy * ry + p.vz * rz;
        const sy = p.vx * ux + p.vy * uy + p.vz * uz;
        const sp = Math.hypot(sx, sy);
        r.billboard(p.add, p.shape, p.x, p.y, p.z, size, col, a, Math.atan2(sy, sx), 1 + sp * p.stretch * 4, p.seed, p.top);
      } else {
        r.billboard(p.add, p.shape, p.x, p.y, p.z, size, col, a, p.rot, 1, p.seed, p.top);
      }
      this.sp[w++] = p;
    }
    this.sp.length = w;

    // Trümmer
    w = 0;
    for (const d of this.debris) {
      d.life -= dt;
      if (d.life <= 0) continue;
      d.vy -= 12 * dt;
      d.x += d.vx * dt;
      d.y += d.vy * dt;
      d.z += d.vz * dt;
      d.yaw += d.vyaw * dt;
      if (d.y < d.floor) {
        d.y = d.floor;
        d.vy *= -0.3;
        d.vx *= 0.5;
        d.vz *= 0.5;
        d.vyaw *= 0.5;
      }
      const s = d.s * Math.min(1, d.life / 0.3);
      r.add('opaque', d.mesh, d.x, d.y + s / 2, d.z, s, s, s, d.color, 1, d.emit, d.yaw, d.yaw * 0.7);
      this.debris[w++] = d;
    }
    this.debris.length = w;

    // Blitze und Strahlen
    w = 0;
    for (const b of this.bolts) {
      b.life -= dt;
      if (b.life <= 0) continue;
      const f = b.life / b.max;
      const flick = b.width < 0 ? 1 : 0.65 + R() * 0.35;
      const width = Math.abs(b.width) * (b.width < 0 ? f : 1);
      const pts = b.pts;
      for (let i = 0; i + 5 < pts.length; i += 3) {
        r.line(pts[i], pts[i + 1], pts[i + 2], pts[i + 3], pts[i + 4], pts[i + 5], width * 3.2, b.color, 0.35 * f * flick);
        r.line(pts[i], pts[i + 1], pts[i + 2], pts[i + 3], pts[i + 4], pts[i + 5], width, mixColor(b.color, 0xffffff, 0.7), f * flick);
      }
      this.bolts[w++] = b;
    }
    this.bolts.length = w;
  }
}

export function jag(x0: number, y0: number, z0: number, x1: number, y1: number, z1: number): number[] {
  const dx = x1 - x0, dy = y1 - y0, dz = z1 - z0;
  const len = Math.hypot(dx, dy, dz);
  const n = Math.max(2, Math.min(14, Math.round(len / 0.32)));
  const amp = Math.min(0.32, len * 0.1);
  const pts: number[] = [x0, y0, z0];
  for (let i = 1; i < n; i++) {
    const t = i / n;
    pts.push(x0 + dx * t + (R() - 0.5) * amp * 2, y0 + dy * t + (R() - 0.5) * amp * 1.4, z0 + dz * t + (R() - 0.5) * amp * 2);
  }
  pts.push(x1, y1, z1);
  return pts;
}
