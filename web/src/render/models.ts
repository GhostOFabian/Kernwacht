import type { BossId, EnemyId, HeroId, TowerId } from '../core/types.ts';
import type { MeshId } from './Renderer.ts';

// Eigene, prozedurale Modelle aus geometrischen Grundkörpern (abgerundete Würfel, glatte Zylinder,
// Kugeln). Koordinaten relativ zur Zellmitte, y = 0 ist die Geländeoberkante.
// c = -1 → Hauptfarbe, -2 → Akzentfarbe, -3 → Sockelstein (Skin).
// Animationsflags: turret (folgt dem Ziel), spin, bob, leg (Beinschwung, Phase), wing (Flügelschlag),
// arm (Angriffsschwung), glow (zusätzliches Leucht-Sprite), mat (Material).

export interface Part {
  m: MeshId;
  x: number; y: number; z: number;
  sx: number; sy: number; sz: number;
  c: number;
  e?: number;
  yaw?: number; pitch?: number; roll?: number;
  turret?: boolean;
  spin?: number;
  bob?: number;
  leg?: number;
  wing?: number;
  arm?: boolean;
  glow?: number;
  mat?: number;
}

const P = (m: MeshId, x: number, y: number, z: number, sx: number, sy: number, sz: number, c: number, extra: Partial<Part> = {}): Part =>
  ({ m, x, y, z, sx, sy, sz, c, ...extra });

const STONE = -3;
const STONE_D = 0x5e5850;
const WOOD = 0x7a5a3a;
const WOOD_D = 0x56402a;
const IRON = 0x4a4a54;
const STEEL = 0x8a90a0;
const COPPER = 0xc07a3c;
const GOLD = 0xf0c040;
const T = -1;
/** Dunkelt/hellt eine Farbe ab (k < 1 dunkler). */
const shadeHex = (c: number, k: number): number => (Math.min(255, Math.round(((c >> 16) & 255) * k)) << 16) | (Math.min(255, Math.round(((c >> 8) & 255) * k)) << 8) | Math.min(255, Math.round((c & 255) * k));
const A = -2;

const base = (): Part[] => [
  P('cyl12', 0, 0, 0, 0.9, 0.14, 0.9, STONE),
  P('cyl12', 0, 0.14, 0, 0.78, 0.06, 0.78, STONE_D),
];

export const TOWER_MODELS: Record<TowerId, Part[]> = {
  ranger: [
    ...base(),
    P('cyl8', 0, 0.2, 0, 0.4, 0.6, 0.4, WOOD),
    P('rbox', 0, 0.84, 0, 0.66, 0.12, 0.66, WOOD_D),
    P('cone8', 0, 0.92, 0, 0.78, 0.48, 0.78, T),
    P('rbox', 0.24, 1.02, 0, 0.1, 0.4, 0.1, WOOD, { turret: true }),
    P('rbox', 0.32, 1.02, 0, 0.46, 0.05, 0.05, T, { turret: true }),
  ],
  ember: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.56, 0.4, 0.56, 0x4a3a36),
    P('cyl12', 0, 0.6, 0, 0.46, 0.14, 0.46, IRON),
    P('cyl12', 0, 0.66, 0, 0.66, 0.22, 0.66, 0x3a2e2a),
    P('sphere', 0, 1.02, 0, 0.42, 0.42, 0.42, T, { e: 1.2, bob: 0.06, glow: 0xff7a30 }),
    P('rbox', 0.28, 0.92, 0, 0.52, 0.08, 0.1, WOOD_D, { turret: true }),
  ],
  frost: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.5, 0.3, 0.5, 0xa8c8e0),
    P('octa', 0, 0.98, 0, 0.38, 1.15, 0.38, T, { e: 0.55, bob: 0.04, glow: 0x9ae0ff }),
    P('octa', 0.36, 0.85, 0, 0.13, 0.28, 0.13, 0xd8f4ff, { e: 0.7, spin: 1.5 }),
    P('octa', -0.18, 0.85, 0.31, 0.13, 0.28, 0.13, 0xd8f4ff, { e: 0.7, spin: 1.5 }),
    P('octa', -0.18, 0.85, -0.31, 0.13, 0.28, 0.13, 0xd8f4ff, { e: 0.7, spin: 1.5 }),
  ],
  storm: [
    ...base(),
    P('cyl8', 0, 0.2, 0, 0.26, 0.9, 0.26, COPPER),
    P('torus', 0, 0.38, 0, 0.66, 0.66, 0.66, COPPER),
    P('torus', 0, 0.62, 0, 0.52, 0.52, 0.52, COPPER),
    P('torus', 0, 0.84, 0, 0.4, 0.4, 0.4, COPPER),
    P('sphere', 0, 1.18, 0, 0.34, 0.34, 0.34, T, { e: 1.3, bob: 0.05, glow: 0xfff08a }),
  ],
  spore: [
    ...base(),
    P('cyl8', 0, 0.2, 0, 0.26, 0.5, 0.26, 0xe8dcc0),
    P('sphere', 0, 0.8, 0, 0.86, 0.5, 0.86, T),
    P('sphere', 0.22, 0.95, 0.12, 0.15, 0.1, 0.15, 0xf8f0d0),
    P('sphere', -0.2, 0.93, -0.14, 0.13, 0.09, 0.13, 0xf8f0d0),
    P('sphere', 0.34, 0.64, 0, 0.2, 0.2, 0.2, 0x6a9a30, { turret: true }),
  ],
  ballista: [
    ...base(),
    P('rbox', 0, 0.3, 0, 0.66, 0.3, 0.66, WOOD_D),
    P('rbox', 0, 0.5, 0, 0.24, 0.22, 0.24, WOOD),
    P('rbox', 0.12, 0.66, 0, 0.96, 0.1, 0.16, WOOD, { turret: true }),
    P('rbox', 0.36, 0.68, 0, 0.12, 0.08, 0.86, T, { turret: true }),
    P('box', 0.3, 0.72, 0, 0.62, 0.03, 0.03, 0xe0e0e0, { turret: true }),
  ],
  rune: [
    ...base(),
    P('rbox', 0, 0.62, 0, 0.36, 0.9, 0.36, 0x3a2a4a),
    P('rbox', 0, 1.12, 0, 0.46, 0.08, 0.46, 0x2a1e38),
    P('octa', 0, 1.46, 0, 0.32, 0.46, 0.32, T, { e: 1.2, spin: 1.8, bob: 0.07, glow: 0xc080ff }),
    P('ringThin', 0, 0.6, 0, 0.46, 1, 0.46, T, { e: 1, spin: -1 }),
  ],
  resonator: [
    ...base(),
    P('cyl8', 0, 0.2, 0, 0.22, 0.25, 0.22, IRON),
    P('cone12', 0, 0.42, 0, 0.78, -0.42, 0.78, T, { pitch: 0 }),
    P('sphere', 0, 0.86, 0, 0.32, 0.32, 0.32, 0xd0f0e8, { e: 0.6, glow: 0x60e8d4 }),
    P('torus', 0, 0.44, 0, 0.86, 0.86, 0.86, 0x2a6a60),
  ],
  goldmine: [
    ...base(),
    P('rbox', 0, 0.38, 0.05, 0.74, 0.38, 0.58, WOOD_D),
    P('cone4', 0, 0.56, 0.05, 0.96, 0.34, 0.76, 0x6a3a2a, { yaw: Math.PI / 4 }),
    P('box', 0, 0.32, -0.27, 0.3, 0.26, 0.04, 0x1a1210),
    P('rbox', 0.24, 0.3, -0.34, 0.24, 0.14, 0.18, IRON),
    P('gem', 0.24, 0.42, -0.34, 0.16, 0.16, 0.16, GOLD, { e: 0.5 }),
    P('gem', -0.26, 0.26, -0.3, 0.13, 0.13, 0.13, GOLD, { e: 0.5 }),
  ],
  pylon: [
    ...base(),
    P('cyl8', 0, 0.2, 0, 0.15, 1.2, 0.15, 0x9aa0b0),
    P('cone8', 0, 0.2, 0, 0.52, 0.5, 0.52, 0x6a7080),
    P('torus', 0, 0.92, 0, 0.36, 0.36, 0.36, T, { e: 0.9, spin: 2 }),
    P('sphere', 0, 1.46, 0, 0.28, 0.28, 0.28, T, { e: 1.4, bob: 0.04, glow: 0x5ab8ff }),
  ],
  cannon: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.7, 0.34, 0.7, STONE_D),
    P('sphere', 0, 0.62, 0, 0.46, 0.36, 0.46, IRON, { turret: true }),
    P('cyl8', 0.12, 0.68, 0, 0.22, 0.66, 0.22, 0x2e2e34, { turret: true, pitch: -Math.PI / 2 + 0.25 }),
    P('torus', 0.7, 0.84, 0, 0.2, 0.2, 0.2, T, { turret: true, roll: Math.PI / 2, yaw: Math.PI / 2 }),
  ],
  riftbinder: [
    ...base(),
    P('rbox', 0.28, 0.64, 0, 0.16, 0.9, 0.16, 0x3a2a3a),
    P('rbox', -0.28, 0.64, 0, 0.16, 0.9, 0.16, 0x3a2a3a),
    P('rbox', 0, 1.14, 0, 0.76, 0.14, 0.2, 0x2a1e2a),
    P('torus', 0, 0.7, 0, 0.64, 0.64, 0.64, T, { e: 1.2, roll: Math.PI / 2, spin: 2.5 }),
    P('sphere', 0, 0.7, 0, 0.2, 0.2, 0.2, 0xffc0ff, { e: 1.5, bob: 0.05, glow: 0xff60ff }),
  ],
  meteor: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.66, 0.24, 0.66, 0x3a2a28),
    P('cone4', 0.3, 0.44, 0.3, 0.16, 0.7, 0.16, 0x5a3a30, { roll: -0.25, pitch: 0.25 }),
    P('cone4', -0.3, 0.44, 0.3, 0.16, 0.7, 0.16, 0x5a3a30, { roll: 0.25, pitch: 0.25 }),
    P('cone4', 0, 0.44, -0.38, 0.16, 0.7, 0.16, 0x5a3a30, { pitch: -0.25 }),
    P('torus', 0, 0.52, 0, 0.78, 0.78, 0.78, T, { e: 0.9, spin: 0.8 }),
    P('sphere', 0, 1.28, 0, 0.42, 0.42, 0.42, 0x3a2420, { bob: 0.12, spin: 0.6 }),
    P('sphere', 0, 1.28, 0, 0.48, 0.48, 0.48, T, { e: 1.4, bob: 0.12, mat: 2, glow: 0xff6a2a }),
  ],
  tesla: [
    ...base(),
    P('cyl8', 0, 0.2, 0, 0.3, 0.9, 0.3, 0x6a6a78),
    P('torus', 0, 0.36, 0, 0.5, 0.9, 0.5, COPPER),
    P('torus', 0, 0.52, 0, 0.5, 0.9, 0.5, COPPER),
    P('torus', 0, 0.68, 0, 0.5, 0.9, 0.5, COPPER),
    P('torus', 0, 0.84, 0, 0.5, 0.9, 0.5, COPPER),
    P('torus', 0, 1.18, 0, 0.72, 1.6, 0.72, STEEL),
    P('sphere', 0, 1.22, 0, 0.3, 0.3, 0.3, T, { e: 1.6, glow: 0x9ae0ff }),
  ],
  gravity: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.62, 0.2, 0.62, 0x2a2440),
    P('torus', 0, 0.55, 0, 0.9, 0.9, 0.9, 0x4a3a7a, { roll: 0.4, spin: 1.2 }),
    P('torus', 0, 0.55, 0, 0.72, 0.72, 0.72, 0x6a5aa0, { pitch: 0.6, spin: -1.6 }),
    P('sphere', 0, 0.62, 0, 0.36, 0.36, 0.36, 0x0a0614, { e: 0 }),
    P('sphere', 0, 0.62, 0, 0.44, 0.44, 0.44, T, { e: 0.8, bob: 0.03, glow: 0x8a6aff }),
  ],
  dragon: [
    ...base(),
    P('rbox', 0, 0.34, 0, 0.7, 0.3, 0.7, 0x5a4a40),
    P('rbox', 0, 0.62, 0, 0.5, 0.3, 0.5, 0x6a5a4a),
    P('sphere', 0, 0.82, 0, 0.62, 0.18, 0.62, 0x8a7050),
    P('sphere', 0.12, 0.92, 0.1, 0.16, 0.16, 0.16, 0xf0e0c0),
    P('sphere', -0.1, 0.92, -0.08, 0.14, 0.14, 0.14, 0xf0e0c0),
  ],
  laser: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.5, 0.36, 0.5, 0x2a3a40),
    P('rbox', 0, 0.68, 0, 0.3, 0.3, 0.3, STEEL, { turret: true }),
    P('octa', 0.16, 0.98, 0, 0.36, 0.7, 0.36, T, { turret: true, e: 1, pitch: -Math.PI / 2 + 0.2, glow: 0x60f0e0 }),
    P('octa', -0.25, 0.4, 0.25, 0.16, 0.4, 0.16, T, { e: 0.6 }),
    P('octa', 0.25, 0.4, -0.25, 0.16, 0.4, 0.16, T, { e: 0.6 }),
  ],
  totem: [
    ...base(),
    P('rbox', 0, 0.48, 0, 0.42, 0.56, 0.42, 0x8a6a40),
    P('rbox', 0, 0.96, 0, 0.46, 0.42, 0.46, 0x9a7a4a),
    P('rbox', 0.25, 1.02, 0, 0.12, 0.12, 0.5, A, { e: 0.6 }),
    P('rbox', 0, 1.3, 0, 0.56, 0.12, 0.2, 0x6a4a2a),
    P('sphere', 0.22, 0.98, 0.1, 0.08, 0.08, 0.08, 0xffe080, { e: 1.5 }),
    P('sphere', 0.22, 0.98, -0.1, 0.08, 0.08, 0.08, 0xffe080, { e: 1.5 }),
    P('cone8', 0, 1.36, 0, 0.34, 0.36, 0.34, T, { e: 0.4 }),
  ],
  plasma: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.66, 0.4, 0.66, 0x2a1a30),
    P('torus', 0, 0.68, 0, 0.9, 0.9, 0.9, COPPER, { spin: 1.4 }),
    P('torus', 0, 0.96, 0, 0.7, 0.7, 0.7, GOLD, { spin: -1.8, roll: 0.3 }),
    P('sphere', 0, 1.18, 0, 0.52, 0.52, 0.52, T, { e: 1.8, bob: 0.06, glow: 0xff8af0 }),
    P('cone8', 0, 1.5, 0, 0.18, 0.5, 0.18, 0xfff0a0, { e: 1.5 }),
  ],
  glacier: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.74, 0.36, 0.74, 0xb8d8f0),
    P('sphere', 0, 0.7, 0, 0.6, 0.46, 0.6, 0xe0f4ff, { turret: true }),
    P('cyl8', 0.2, 0.78, 0, 0.26, 0.8, 0.26, 0x9ad0f0, { turret: true, pitch: -Math.PI / 2 + 0.3 }),
    P('octa', 0, 1.2, 0, 0.3, 0.7, 0.3, T, { e: 1, spin: 1, glow: 0x9ae8ff }),
    P('octa', 0.4, 0.4, 0.3, 0.18, 0.5, 0.18, 0xe8faff, { e: 0.6 }),
    P('octa', -0.4, 0.4, -0.3, 0.18, 0.5, 0.18, 0xe8faff, { e: 0.6 }),
  ],
  voidlance: [
    ...base(),
    P('rbox', 0, 0.4, 0, 0.6, 0.4, 0.6, 0x1a1428),
    P('rbox', 0.12, 0.74, 0, 1.2, 0.12, 0.2, 0x3a2a5a, { turret: true }),
    P('cone8', 0.8, 0.74, 0, 0.16, 0.5, 0.16, T, { turret: true, roll: -Math.PI / 2, e: 1.2, glow: 0x9a5aff }),
    P('octa', 0, 1.2, 0, 0.3, 0.5, 0.3, T, { e: 1.4, spin: 2.2, bob: 0.08 }),
    P('ringThin', 0, 0.3, 0, 0.6, 1, 0.6, T, { e: 1.2, spin: 1 }),
  ],
  starfall: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.7, 0.3, 0.7, 0x1a1a30),
    P('cone8', 0, 0.5, 0, 0.5, 1.3, 0.5, 0x2a2a50),
    P('torus', 0, 1.2, 0, 1.1, 1.1, 1.1, T, { e: 1.2, spin: 0.7, roll: 0.25 }),
    P('gem', 0, 1.9, 0, 0.42, 0.42, 0.42, 0xffffff, { e: 2, spin: 1.5, bob: 0.1, glow: 0xffe080 }),
    P('gem', 0.5, 1.5, 0, 0.16, 0.16, 0.16, T, { e: 1.5, spin: 3, bob: 0.08 }),
    P('gem', -0.5, 1.5, 0, 0.16, 0.16, 0.16, T, { e: 1.5, spin: 3, bob: 0.08 }),
  ],
  thornwood: [
    ...base(),
    P('cyl8', 0, 0.2, 0, 0.36, 0.9, 0.36, WOOD_D),
    P('cone8', 0.16, 0.55, 0.1, 0.08, 0.4, 0.08, 0x5a8a30, { roll: -0.9 }),
    P('cone8', -0.14, 0.7, -0.1, 0.08, 0.36, 0.08, 0x5a8a30, { roll: 0.9 }),
    P('sphere', 0, 1.16, 0, 0.9, 0.6, 0.9, 0x3a7a2a, { mat: 3 }),
    P('sphere', 0.18, 1.32, 0.1, 0.5, 0.4, 0.5, T, { mat: 3 }),
    P('sphere', 0.3, 1.0, -0.2, 0.14, 0.14, 0.14, 0xc0f060, { e: 0.6 }),
    P('rbox', 0.32, 1.1, 0, 0.5, 0.06, 0.06, WOOD, { turret: true }),
    P('cone4', 0.6, 1.1, 0, 0.06, 0.16, 0.06, 0x9ad04a, { turret: true, roll: -Math.PI / 2 }),
  ],
  thunderspire: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.6, 0.3, 0.6, IRON),
    P('cyl8', 0, 0.5, 0, 0.3, 1.0, 0.3, 0x3a4a5a),
    P('torus', 0, 0.7, 0, 0.66, 0.66, 0.66, COPPER),
    P('torus', 0, 0.95, 0, 0.56, 0.56, 0.56, COPPER),
    P('torus', 0, 1.2, 0, 0.46, 0.46, 0.46, COPPER),
    P('cone8', 0, 1.5, 0, 0.16, 0.5, 0.16, STEEL),
    P('sphere', 0, 2.05, 0, 0.26, 0.26, 0.26, T, { e: 0.9, bob: 0.05, glow: 0x7ac8ff }),
  ],
  riftgate: [
    ...base(),
    P('rbox', -0.3, 0.6, 0, 0.18, 1.1, 0.2, 0x2a1840),
    P('rbox', 0.3, 0.6, 0, 0.18, 1.1, 0.2, 0x2a1840),
    P('rbox', 0, 1.18, 0, 0.8, 0.16, 0.24, 0x3a2058),
    P('torus', 0, 0.8, 0, 0.62, 0.62, 0.62, T, { e: 0.7, spin: 1.5, pitch: Math.PI / 2 }),
    P('disc', 0, 0.8, 0, 0.5, 1, 0.5, 0x6a20a0, { e: 0.6, pitch: Math.PI / 2, spin: 2 }),
    P('octa', 0, 1.42, 0, 0.18, 0.3, 0.18, T, { e: 0.8, spin: 2, bob: 0.06 }),
  ],
  phoenix: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.72, 0.36, 0.72, 0x5a2a1a),
    P('cone12', 0, 0.56, 0, 0.64, 0.4, 0.64, 0x8a3a1a),
    P('cyl8', 0, 0.96, 0, 0.12, 0.3, 0.12, WOOD_D),
    P('sphere', 0, 1.22, 0, 0.24, 0.2, 0.24, GOLD),
    P('cone8', 0.12, 1.22, 0, 0.14, 0.4, 0.14, 0xff8030, { e: 0.7, bob: 0.06 }),
  ],
  cathedral: [
    ...base(),
    P('rbox', 0, 0.45, 0, 0.72, 0.7, 0.72, 0xd8d0c0),
    P('rbox', 0, 0.85, 0, 0.5, 0.3, 0.5, 0xc0b8a8),
    P('cone4', 0, 1.0, 0, 0.6, 0.7, 0.6, 0x3a6a8a, { yaw: Math.PI / 4 }),
    P('cone8', 0, 0.62, 0, 0.36, 0.3, 0.36, GOLD, { pitch: Math.PI, bob: 0.02 }),
    P('cone4', 0.36, 0.8, 0.36, 0.14, 0.5, 0.14, 0x3a6a8a, { yaw: Math.PI / 4 }),
    P('cone4', -0.36, 0.8, -0.36, 0.14, 0.5, 0.14, 0x3a6a8a, { yaw: Math.PI / 4 }),
    P('ringThin', 0, 0.3, 0, 1.1, 1, 1.1, T, { e: 0.4, spin: 0.8 }),
  ],
  stormeye: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.62, 0.32, 0.62, 0xb8d8f0),
    P('octa', 0.22, 0.5, 0.18, 0.2, 0.6, 0.2, 0xe0f4ff, { e: 0.3 }),
    P('octa', -0.22, 0.5, -0.18, 0.2, 0.6, 0.2, 0xe0f4ff, { e: 0.3 }),
    P('cyl8', 0, 0.5, 0, 0.22, 0.8, 0.22, 0x5a6a8a),
    P('sphere', 0, 1.36, 0, 0.5, 0.36, 0.5, 0x4a5a7a),
    P('sphere', 0, 1.4, 0, 0.24, 0.24, 0.24, T, { e: 0.9, glow: 0xa0d8ff, bob: 0.04 }),
    P('torus', 0, 1.4, 0, 0.74, 0.74, 0.74, 0xe0f4ff, { spin: 2, roll: 0.4, e: 0.3 }),
  ],
  minelayer: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.72, 0.26, 0.72, WOOD_D),
    P('rbox', 0, 0.5, 0, 0.5, 0.3, 0.42, IRON, { turret: true }),
    P('cyl8', 0.08, 0.6, 0, 0.3, 0.62, 0.3, 0x2e2e34, { turret: true, pitch: -Math.PI / 2 + 0.7 }),
    P('torus', 0.36, 0.98, 0, 0.32, 0.32, 0.32, T, { turret: true, roll: Math.PI / 2, yaw: Math.PI / 2 }),
    P('cyl8', -0.26, 0.36, 0.22, 0.2, 0.08, 0.2, 0x3a3a30),
    P('sphere', -0.26, 0.47, 0.22, 0.08, 0.08, 0.08, 0xff4030, { e: 0.8 }),
    P('cyl8', -0.24, 0.36, -0.22, 0.2, 0.08, 0.2, 0x3a3a30),
    P('sphere', -0.24, 0.47, -0.22, 0.08, 0.08, 0.08, 0xff4030, { e: 0.8 }),
  ],
  sporemine: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.8, 0.3, 0.8, 0x5a4a2a),
    P('sphere', 0, 0.62, 0, 0.62, 0.42, 0.62, 0x6a9a3a, { turret: true }),
    P('cyl8', 0.1, 0.72, 0, 0.32, 0.66, 0.32, 0x3a4a2a, { turret: true, pitch: -Math.PI / 2 + 0.7 }),
    P('sphere', 0.42, 1.1, 0, 0.26, 0.26, 0.26, 0xb0e060, { turret: true, e: 0.5 }),
    P('sphere', -0.28, 0.44, 0.24, 0.2, 0.16, 0.2, 0x8ad040),
    P('sphere', -0.3, 0.44, -0.22, 0.2, 0.16, 0.2, 0x8ad040),
    P('cone8', -0.28, 0.5, 0.24, 0.24, 0.12, 0.24, 0xd06040),
    P('cone8', -0.3, 0.5, -0.22, 0.24, 0.12, 0.24, 0xd06040),
  ],
  skyguard: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.7, 0.28, 0.7, 0x3a4658),
    P('rbox', 0, 0.48, 0, 0.46, 0.28, 0.46, STEEL, { turret: true }),
    P('cyl8', 0.12, 0.68, 0.12, 0.12, 0.7, 0.12, 0x2a3240, { turret: true, pitch: -Math.PI / 2 + 0.9 }),
    P('cyl8', 0.12, 0.68, -0.12, 0.12, 0.7, 0.12, 0x2a3240, { turret: true, pitch: -Math.PI / 2 + 0.9 }),
    P('disc', -0.18, 0.72, 0, 0.3, 1, 0.3, T, { turret: true, pitch: Math.PI / 2 - 0.4, e: 0.2 }),
    P('sphere', -0.18, 0.86, 0, 0.06, 0.06, 0.06, 0x80e0ff, { e: 0.9, bob: 0.03 }),
  ],
  skywatch: [
    ...base(),
    P('cyl12', 0, 0.2, 0, 0.8, 0.32, 0.8, 0xb8d8f0),
    P('octa', 0.28, 0.46, 0.24, 0.16, 0.5, 0.16, 0xe0f4ff, { e: 0.3 }),
    P('octa', -0.28, 0.46, -0.24, 0.16, 0.5, 0.16, 0xe0f4ff, { e: 0.3 }),
    P('rbox', 0, 0.62, 0, 0.5, 0.3, 0.5, 0x5a7a9a, { turret: true }),
    P('cyl8', 0.14, 0.86, 0.14, 0.12, 0.8, 0.12, 0x3a4a60, { turret: true, pitch: -Math.PI / 2 + 0.9 }),
    P('cyl8', 0.14, 0.86, -0.14, 0.12, 0.8, 0.12, 0x3a4a60, { turret: true, pitch: -Math.PI / 2 + 0.9 }),
    P('sphere', 0, 1.3, 0, 0.2, 0.2, 0.2, T, { e: 0.9, glow: 0x9ad8ff, bob: 0.05 }),
    P('torus', 0, 1.3, 0, 0.6, 0.6, 0.6, 0xe0f4ff, { spin: 1.6, roll: 0.5, e: 0.3 }),
  ],
  barricade: [
    P('rbox', 0, 0.22, 0, 0.94, 0.44, 0.94, STONE_D),
    P('rbox', -0.2, 0.5, -0.15, 0.38, 0.16, 0.42, STONE),
    P('rbox', 0.2, 0.52, 0.18, 0.36, 0.18, 0.38, STONE),
    P('cone4', 0, 0.44, 0, 0.18, 0.26, 0.18, 0xb0a890),
  ],
  lanebarricade: [
    P('rbox', -0.32, 0.22, 0, 0.12, 0.44, 0.12, 0x6a4a2a),
    P('rbox', 0.32, 0.22, 0, 0.12, 0.44, 0.12, 0x6a4a2a),
    P('rbox', 0, 0.36, 0, 0.86, 0.12, 0.1, 0xf0c040),
    P('rbox', 0, 0.2, 0, 0.86, 0.1, 0.1, 0x303030),
    P('cone4', -0.15, 0.08, 0.22, 0.12, 0.14, 0.12, 0x9a9a9a),
    P('cone4', 0.18, 0.08, -0.2, 0.12, 0.14, 0.12, 0x9a9a9a),
  ],
};

/** Höhe für Netzwerkverbindungen, Symbole und Effekte je Turm. */
export const TOWER_TOP: Record<TowerId, number> = {
  ranger: 1.3, ember: 1.2, frost: 1.5, storm: 1.4, spore: 1.05, ballista: 0.8, rune: 1.7,
  resonator: 1.0, goldmine: 0.85, pylon: 1.5, cannon: 0.95, riftbinder: 1.25, barricade: 0.6, lanebarricade: 0.5,
  meteor: 1.6, tesla: 1.3, gravity: 1.0, dragon: 1.0, laser: 1.2, totem: 1.6,
  plasma: 1.6, glacier: 1.5, voidlance: 1.4, starfall: 2.1,
  thornwood: 1.5, thunderspire: 2.2, riftgate: 1.5, phoenix: 1.4, cathedral: 1.6, stormeye: 1.7, minelayer: 1.1, sporemine: 1.2, skyguard: 1.0, skywatch: 1.5,
};

/** Zusatzteile je Ausbaustufe (kumulativ): Banner, Goldringe, leuchtende Krone. */
export function levelParts(level: number, top: number): Part[] {
  const out: Part[] = [];
  if (level >= 2) {
    out.push(P('cyl8', 0.42, 0.18, 0.42, 0.05, 0.75, 0.05, WOOD_D));
    out.push(P('rbox', 0.42, 0.78, 0.5, 0.03, 0.2, 0.18, T, { wing: 0.15 }));
  }
  if (level >= 3) {
    out.push(P('torus', 0, 0.2, 0, 0.96, 0.35, 0.96, 0xc89a30, { e: 0.1 }));
    out.push(P('cyl8', -0.42, 0.18, -0.42, 0.05, 0.75, 0.05, WOOD_D));
    out.push(P('rbox', -0.42, 0.78, -0.34, 0.03, 0.2, 0.18, T, { wing: 0.15 }));
  }
  if (level >= 4) {
    for (let k = 0; k < 4; k++) {
      const a = (k / 4) * Math.PI * 2 + Math.PI / 4;
      out.push(P('cone4', Math.cos(a) * 0.48, 0.2, Math.sin(a) * 0.48, 0.12, 0.34, 0.12, GOLD, { e: 0.5 }));
    }
    out.push(P('ringThin', 0, top + 0.1, 0, 0.3, 1, 0.3, A, { e: 1.2, spin: 1.5 }));
  }
  return out;
}

/** Spezialisierungsmerkmale: A = glühende Krone (orange), B = kreisende Runen (cyan). */
export function specParts(spec: 0 | 1 | 2, top: number): Part[] {
  if (spec === 1) {
    return [
      P('torus', 0, top + 0.25, 0, 0.5, 0.3, 0.5, 0xffa040, { e: 1.2, spin: 1.2, glow: 0xffa040 }),
      P('cone4', 0, top + 0.3, 0, 0.16, 0.36, 0.16, 0xffc060, { e: 1.2 }),
    ];
  }
  if (spec === 2) {
    const out: Part[] = [];
    for (let k = 0; k < 3; k++) {
      const a = (k / 3) * Math.PI * 2;
      out.push(P('gem', Math.cos(a) * 0.55, top * 0.7, Math.sin(a) * 0.55, 0.14, 0.2, 0.14, 0x60e0ff, { e: 1.5, spin: 1.8, bob: 0.08, glow: 0x60e0ff }));
    }
    return out;
  }
  return [];
}

// Gegner: Einheitsgröße wird mit def.size skaliert. Blickrichtung +X.
// Gegner (Überarbeitung 2.5): klarere Silhouetten, Kopf/Körper/Gliedmaßen getrennt,
// Akzente über Material statt Leuchten. Blickrichtung +X, Höhe ≈ 1.
const EYE = 0x1a1410;
const SKIN = 0xe8c8a8;
const HORN = 0xe8dcc0;
export const ENEMY_MODELS: Record<Exclude<EnemyId, 'boss'>, Part[]> = {
  // Flinkling: schlanker Läufer mit großen Ohren und Schwanz
  runner: [
    P('rbox', -0.02, 0.17, 0.13, 0.12, 0.3, 0.1, 0x4a3a28, { leg: 0 }),
    P('rbox', -0.02, 0.17, -0.13, 0.12, 0.3, 0.1, 0x4a3a28, { leg: Math.PI }),
    P('sphere', 0, 0.46, 0, 0.62, 0.46, 0.44, T),
    P('sphere', 0.3, 0.66, 0, 0.36, 0.34, 0.34, T),
    P('cone4', 0.27, 0.8, 0.13, 0.12, 0.3, 0.08, T, { roll: -0.3, pitch: 0.5 }),
    P('cone4', 0.27, 0.8, -0.13, 0.12, 0.3, 0.08, T, { roll: -0.3, pitch: -0.5 }),
    P('sphere', 0.45, 0.68, 0.08, 0.08, 0.09, 0.08, 0xfff2c0),
    P('sphere', 0.45, 0.68, -0.08, 0.08, 0.09, 0.08, 0xfff2c0),
    P('sphere', 0.48, 0.68, 0.08, 0.04, 0.05, 0.04, EYE),
    P('sphere', 0.48, 0.68, -0.08, 0.04, 0.05, 0.04, EYE),
    P('cone8', -0.28, 0.46, 0, 0.12, 0.42, 0.12, 0x6a5030, { roll: 2.0 }),
    P('rbox', 0.12, 0.42, 0.22, 0.08, 0.24, 0.08, 0x4a3a28, { leg: Math.PI }),
    P('rbox', 0.12, 0.42, -0.22, 0.08, 0.24, 0.08, 0x4a3a28, { leg: 0 }),
  ],
  // Schwarmkäfer: Panzer mit Segmenten, sechs Beine, Fühler
  swarm: [
    P('sphere', -0.05, 0.3, 0, 0.82, 0.42, 0.66, T),
    P('sphere', -0.05, 0.38, 0, 0.7, 0.34, 0.56, 0x3a2414),
    P('rbox', -0.05, 0.42, 0, 0.66, 0.04, 0.04, 0x1a1008),
    P('sphere', 0.38, 0.3, 0, 0.32, 0.28, 0.32, 0x5a3420),
    P('sphere', 0.52, 0.34, 0.08, 0.06, 0.06, 0.06, 0xffd060, { e: 0.6 }),
    P('sphere', 0.52, 0.34, -0.08, 0.06, 0.06, 0.06, 0xffd060, { e: 0.6 }),
    P('cyl6', 0.5, 0.36, 0.06, 0.02, 0.3, 0.02, 0x2a1810, { roll: -1.0, pitch: 0.4 }),
    P('cyl6', 0.5, 0.36, -0.06, 0.02, 0.3, 0.02, 0x2a1810, { roll: -1.0, pitch: -0.4 }),
    P('rbox', 0.15, 0.12, 0, 0.06, 0.05, 0.86, 0x2a1810, { leg: 0, yaw: 0.4 }),
    P('rbox', -0.05, 0.12, 0, 0.06, 0.05, 0.9, 0x2a1810, { leg: Math.PI }),
    P('rbox', -0.25, 0.12, 0, 0.06, 0.05, 0.86, 0x2a1810, { leg: 0, yaw: -0.4 }),
  ],
  // Panzerkoloss: gebückter Hüne mit Schulterplatten, Hauern und Keule
  brute: [
    P('rbox', -0.02, 0.2, 0.26, 0.28, 0.4, 0.26, 0x4a4438, { leg: 0 }),
    P('rbox', -0.02, 0.2, -0.26, 0.28, 0.4, 0.26, 0x4a4438, { leg: Math.PI }),
    P('rbox', 0, 0.66, 0, 0.78, 0.66, 0.86, T),
    P('rbox', 0.06, 0.48, 0, 0.7, 0.14, 0.8, 0x3a3024),
    P('rbox', 0.3, 0.96, 0, 0.44, 0.36, 0.46, T),
    P('rbox', 0.08, 1.02, 0.46, 0.46, 0.2, 0.36, 0x8a8478, { roll: 0.25 }),
    P('rbox', 0.08, 1.02, -0.46, 0.46, 0.2, 0.36, 0x8a8478, { roll: 0.25 }),
    P('cone8', 0.5, 0.86, 0.12, 0.08, 0.2, 0.08, HORN, { roll: -0.6 }),
    P('cone8', 0.5, 0.86, -0.12, 0.08, 0.2, 0.08, HORN, { roll: -0.6 }),
    P('sphere', 0.52, 1.02, 0.1, 0.07, 0.06, 0.07, 0xff7030, { e: 0.8 }),
    P('sphere', 0.52, 1.02, -0.1, 0.07, 0.06, 0.07, 0xff7030, { e: 0.8 }),
    P('rbox', 0.2, 0.62, 0.52, 0.22, 0.56, 0.22, T, { arm: true }),
    P('cyl8', 0.36, 0.3, 0.52, 0.1, 0.62, 0.1, WOOD_D, { arm: true, roll: -0.4 }),
    P('rbox', 0.56, 0.84, 0.52, 0.24, 0.24, 0.24, IRON, { arm: true }),
    P('rbox', 0.2, 0.62, -0.52, 0.22, 0.56, 0.22, T),
  ],
  // Moorschrat: Knollenwesen mit Ranken und Blüte
  regen: [
    P('rbox', 0, 0.14, 0.16, 0.14, 0.26, 0.14, 0x3a5a2a, { leg: 0 }),
    P('rbox', 0, 0.14, -0.16, 0.14, 0.26, 0.14, 0x3a5a2a, { leg: Math.PI }),
    P('sphere', 0, 0.52, 0, 0.74, 0.7, 0.7, T),
    P('sphere', 0.14, 0.62, 0, 0.5, 0.5, 0.56, shadeHex(0x6aa050, 0.9)),
    P('sphere', 0.36, 0.64, 0.1, 0.08, 0.1, 0.08, 0xf0ffb0, { e: 0.6 }),
    P('sphere', 0.36, 0.64, -0.1, 0.08, 0.1, 0.08, 0xf0ffb0, { e: 0.6 }),
    P('leaf', -0.06, 0.86, 0, 0.42, 0.42, 0.42, 0x4ab05a, { pitch: -0.5, mat: 3 }),
    P('leaf', -0.06, 0.86, 0, 0.42, 0.42, 0.42, 0x3a9a4a, { yaw: 2.1, pitch: -0.5, mat: 3 }),
    P('leaf', -0.06, 0.86, 0, 0.42, 0.42, 0.42, 0x4ab05a, { yaw: 4.2, pitch: -0.5, mat: 3 }),
    P('sphere', -0.06, 0.96, 0, 0.16, 0.12, 0.16, 0xff80c0),
    P('cyl6', 0.1, 0.4, 0.34, 0.05, 0.4, 0.05, 0x3a5a2a, { roll: 0.8, arm: true }),
    P('cyl6', 0.1, 0.4, -0.34, 0.05, 0.4, 0.05, 0x3a5a2a, { roll: 0.8 }),
  ],
  // Schattenschwinge: Fledermausflieger
  flyer: [
    P('sphere', 0, 0, 0, 0.56, 0.36, 0.4, T),
    P('sphere', 0.28, 0.06, 0, 0.26, 0.24, 0.24, T),
    P('cone4', 0.28, 0.2, 0.06, 0.06, 0.16, 0.05, T, { pitch: 0.3 }),
    P('cone4', 0.28, 0.2, -0.06, 0.06, 0.16, 0.05, T, { pitch: -0.3 }),
    P('sphere', 0.39, 0.08, 0.06, 0.05, 0.05, 0.05, 0xffb040, { e: 0.9 }),
    P('sphere', 0.39, 0.08, -0.06, 0.05, 0.05, 0.05, 0xffb040, { e: 0.9 }),
    P('leaf', -0.02, 0.02, 0.12, 0.8, 1, 0.95, 0x4a2a5a, { wing: 1 }),
    P('leaf', -0.02, 0.02, -0.12, 0.8, 1, -0.95, 0x4a2a5a, { wing: -1 }),
    P('leaf', -0.02, 0.03, 0.12, 0.5, 1, 0.6, 0x7a4a90, { wing: 1 }),
    P('leaf', -0.02, 0.03, -0.12, 0.5, 1, -0.6, 0x7a4a90, { wing: -1 }),
    P('cone8', -0.28, 0, 0, 0.1, 0.3, 0.1, T, { roll: Math.PI / 2 }),
  ],
  // Schildträger: Ritter mit Turmschild und Helmbusch
  shield: [
    P('rbox', 0, 0.16, 0.14, 0.16, 0.32, 0.14, 0x5a6070, { leg: 0 }),
    P('rbox', 0, 0.16, -0.14, 0.16, 0.32, 0.14, 0x5a6070, { leg: Math.PI }),
    P('rbox', 0, 0.56, 0, 0.42, 0.5, 0.5, T),
    P('rbox', 0, 0.36, 0, 0.44, 0.08, 0.52, 0x3a3a44),
    P('sphere', 0, 0.94, 0, 0.32, 0.32, 0.32, 0xb0b8c8),
    P('rbox', 0.14, 0.94, 0, 0.06, 0.06, 0.2, 0x1a1a24),
    P('cone8', -0.04, 1.06, 0, 0.1, 0.3, 0.1, 0xc04040, { roll: 0.5 }),
    P('rbox', 0.4, 0.58, 0.04, 0.08, 0.8, 0.66, 0xc8d0e8),
    P('rbox', 0.44, 0.58, 0.04, 0.03, 0.62, 0.12, 0x6a8ad0),
    P('rbox', 0.44, 0.62, 0.04, 0.03, 0.12, 0.44, 0x6a8ad0),
    P('rbox', 0.1, 0.6, -0.32, 0.12, 0.42, 0.12, T, { arm: true }),
    P('rbox', 0.32, 0.86, -0.32, 0.05, 0.5, 0.05, STEEL, { arm: true }),
  ],
  // Phasenwandler: schwebender Kristallgeist mit Ringen
  phase: [
    P('octa', 0, 0.62, 0, 0.56, 0.9, 0.56, T, { e: 0.3, spin: 1.2 }),
    P('octa', 0, 0.62, 0, 0.36, 0.62, 0.36, 0xe0ffff, { e: 0.6, spin: -2 }),
    P('ringThin', 0, 0.64, 0, 0.9, 1, 0.9, 0x80e0e0, { e: 0.5, spin: 2, roll: 0.4 }),
    P('octa', 0.4, 0.78, 0.24, 0.12, 0.2, 0.12, T, { spin: 3, bob: 0.06 }),
    P('octa', -0.3, 0.5, -0.3, 0.12, 0.2, 0.12, T, { spin: -3, bob: 0.06 }),
  ],
  // Saboteur: Kobold mit Funkantenne und Werkzeugtasche
  saboteur: [
    P('rbox', 0, 0.12, 0.14, 0.14, 0.24, 0.12, 0x3a2a20, { leg: 0 }),
    P('rbox', 0, 0.12, -0.14, 0.14, 0.24, 0.12, 0x3a2a20, { leg: Math.PI }),
    P('rbox', 0, 0.42, 0, 0.36, 0.38, 0.4, T),
    P('sphere', 0.08, 0.74, 0, 0.32, 0.3, 0.32, 0x8aa070),
    P('rbox', 0.24, 0.78, 0, 0.06, 0.08, 0.24, 0x202020),
    P('sphere', 0.27, 0.78, 0.06, 0.04, 0.04, 0.04, 0xffe060, { e: 0.8 }),
    P('sphere', 0.27, 0.78, -0.06, 0.04, 0.04, 0.04, 0xffe060, { e: 0.8 }),
    P('rbox', -0.26, 0.48, 0, 0.18, 0.34, 0.34, 0x5a4030),
    P('cyl6', -0.3, 0.64, 0.08, 0.03, 0.5, 0.03, IRON),
    P('sphere', -0.3, 1.16, 0.08, 0.09, 0.09, 0.09, 0xffe060, { e: 0.9 }),
    P('rbox', 0.24, 0.4, 0.22, 0.2, 0.08, 0.08, IRON, { arm: true }),
  ],
  // Pfadspäher: Kundschafter mit Kapuze und Wimpel
  scout: [
    P('rbox', 0, 0.14, 0.12, 0.12, 0.28, 0.11, 0x3a3020, { leg: 0 }),
    P('rbox', 0, 0.14, -0.12, 0.12, 0.28, 0.11, 0x3a3020, { leg: Math.PI }),
    P('cone12', 0, 0.26, 0, 0.42, 0.56, 0.42, T),
    P('sphere', 0.02, 0.84, 0, 0.26, 0.26, 0.26, SKIN),
    P('cone8', -0.03, 0.82, 0, 0.32, 0.34, 0.32, shadeHex(0x6a8a3a, 0.8)),
    P('cyl6', -0.22, 0.2, 0, 0.04, 1.2, 0.04, WOOD),
    P('leaf', -0.22, 1.28, 0, 0.36, 0.5, 0.36, 0xe04040, { roll: Math.PI / 2, wing: 0.2 }),
    P('rbox', 0.16, 0.5, 0.18, 0.18, 0.06, 0.06, WOOD_D, { arm: true }),
  ],
  // Brutmutter: Spinne mit Hinterleib voller Eier
  broodmother: [
    P('sphere', -0.25, 0.56, 0, 0.82, 0.66, 0.8, T),
    P('sphere', -0.3, 0.72, 0.2, 0.18, 0.16, 0.18, 0xe0c0a0),
    P('sphere', -0.42, 0.66, -0.16, 0.16, 0.14, 0.16, 0xe0c0a0),
    P('sphere', -0.16, 0.8, -0.04, 0.14, 0.12, 0.14, 0xe0c0a0),
    P('sphere', 0.28, 0.46, 0, 0.46, 0.38, 0.42, 0x3a1828),
    P('sphere', 0.46, 0.54, 0.08, 0.06, 0.06, 0.06, 0xff4040, { e: 0.7 }),
    P('sphere', 0.46, 0.54, -0.08, 0.06, 0.06, 0.06, 0xff4040, { e: 0.7 }),
    P('sphere', 0.48, 0.48, 0, 0.05, 0.05, 0.05, 0xff4040, { e: 0.7 }),
    P('cone8', 0.5, 0.36, 0.06, 0.05, 0.16, 0.05, HORN, { pitch: Math.PI }),
    P('cone8', 0.5, 0.36, -0.06, 0.05, 0.16, 0.05, HORN, { pitch: Math.PI }),
    P('rbox', 0.25, 0.3, 0, 0.06, 0.06, 1.6, 0x2a1020, { yaw: 0.5, leg: 0, roll: 0.15 }),
    P('rbox', 0.18, 0.3, 0, 0.06, 0.06, 1.7, 0x2a1020, { yaw: 0.15, leg: Math.PI }),
    P('rbox', 0.12, 0.3, 0, 0.06, 0.06, 1.7, 0x2a1020, { yaw: -0.2, leg: 0 }),
    P('rbox', 0.05, 0.3, 0, 0.06, 0.06, 1.6, 0x2a1020, { yaw: -0.55, leg: Math.PI }),
  ],
  broodling: [
    P('sphere', -0.05, 0.3, 0, 0.6, 0.46, 0.56, T),
    P('sphere', 0.26, 0.28, 0, 0.3, 0.26, 0.28, 0x3a1828),
    P('sphere', 0.38, 0.32, 0.06, 0.05, 0.05, 0.05, 0xff6060, { e: 0.7 }),
    P('sphere', 0.38, 0.32, -0.06, 0.05, 0.05, 0.05, 0xff6060, { e: 0.7 }),
    P('rbox', 0.1, 0.14, 0, 0.05, 0.05, 1.1, 0x2a1020, { yaw: 0.5, leg: 0 }),
    P('rbox', 0, 0.14, 0, 0.05, 0.05, 1.1, 0x2a1020, { yaw: -0.5, leg: Math.PI }),
  ],
  // Höhlengolem: Felsriese mit Kristalladern
  golem: [
    P('rbox', 0, 0.22, 0.26, 0.32, 0.44, 0.32, 0x5a5048, { leg: 0 }),
    P('rbox', 0, 0.22, -0.26, 0.32, 0.44, 0.32, 0x5a5048, { leg: Math.PI }),
    P('rbox', 0, 0.76, 0, 0.74, 0.72, 0.84, T),
    P('rbox', -0.06, 0.92, 0, 0.6, 0.4, 0.96, shadeHex(0x8a8076, 0.85)),
    P('rbox', 0.26, 1.24, 0, 0.4, 0.32, 0.42, 0x8a8076),
    P('rbox', 0.46, 1.24, 0, 0.04, 0.06, 0.26, 0x203a38),
    P('gem', 0.47, 1.26, 0, 0.1, 0.06, 0.2, 0x60f0d0, { e: 0.9 }),
    P('rbox', 0.08, 0.72, 0.6, 0.3, 0.72, 0.3, 0x6a6058, { arm: true, leg: Math.PI }),
    P('rbox', 0.18, 0.36, 0.62, 0.36, 0.3, 0.36, 0x6a6058, { arm: true }),
    P('rbox', 0.08, 0.72, -0.6, 0.3, 0.72, 0.3, 0x6a6058, { arm: true, leg: 0 }),
    P('rbox', 0.18, 0.36, -0.62, 0.36, 0.3, 0.36, 0x6a6058, { arm: true }),
    P('gem', -0.1, 1.2, 0.3, 0.16, 0.3, 0.16, 0x60f0d0, { e: 0.6 }),
    P('gem', -0.2, 1.12, -0.28, 0.14, 0.26, 0.14, 0x60f0d0, { e: 0.6 }),
  ],
  golemling: [
    P('rbox', 0, 0.12, 0.2, 0.22, 0.24, 0.22, 0x6a6058, { leg: 0 }),
    P('rbox', 0, 0.12, -0.2, 0.22, 0.24, 0.22, 0x6a6058, { leg: Math.PI }),
    P('rbox', 0, 0.46, 0, 0.62, 0.5, 0.66, T),
    P('rbox', 0.24, 0.66, 0, 0.3, 0.24, 0.32, 0x8a8076),
    P('gem', 0.4, 0.66, 0, 0.08, 0.06, 0.16, 0x60f0d0, { e: 0.8 }),
  ],
  // Schattenschleicher: Assassine mit Umhang und Dolchen
  stalker: [
    P('rbox', 0, 0.16, 0.11, 0.12, 0.3, 0.1, 0x18141e, { leg: 0 }),
    P('rbox', 0, 0.16, -0.11, 0.12, 0.3, 0.1, 0x18141e, { leg: Math.PI }),
    P('cone12', -0.04, 0.2, 0, 0.56, 0.78, 0.5, T),
    P('rbox', 0.02, 0.6, 0, 0.3, 0.32, 0.34, 0x241e34),
    P('sphere', 0.06, 0.92, 0, 0.28, 0.28, 0.28, 0x14101c),
    P('cone8', 0.02, 0.92, 0, 0.32, 0.36, 0.32, T, { roll: 0.25 }),
    P('rbox', 0.2, 0.92, 0, 0.04, 0.04, 0.16, 0xff3a60, { e: 1.0 }),
    P('cone4', 0.36, 0.5, 0.22, 0.05, 0.36, 0.05, STEEL, { roll: -1.4, arm: true }),
    P('cone4', 0.36, 0.5, -0.22, 0.05, 0.36, 0.05, STEEL, { roll: -1.4, arm: true }),
    P('leaf', -0.12, 0.78, 0.18, 0.5, 1, 0.7, 0x241e34, { roll: -0.9, pitch: 2.4, wing: 0.15 }),
    P('leaf', -0.12, 0.78, -0.18, 0.5, 1, -0.7, 0x241e34, { roll: 0.9, pitch: -2.4, wing: -0.15 }),
  ],
  // Belagerungsbestie: gepanzertes Nashornwesen mit Rammhorn und Rüstung
  siege: [
    P('rbox', 0.32, 0.2, 0.3, 0.24, 0.4, 0.22, 0x3a2420, { leg: 0 }),
    P('rbox', 0.32, 0.2, -0.3, 0.24, 0.4, 0.22, 0x3a2420, { leg: Math.PI }),
    P('rbox', -0.34, 0.2, 0.3, 0.24, 0.4, 0.22, 0x3a2420, { leg: Math.PI }),
    P('rbox', -0.34, 0.2, -0.3, 0.24, 0.4, 0.22, 0x3a2420, { leg: 0 }),
    P('rbox', 0, 0.66, 0, 1.1, 0.62, 0.78, T),
    P('rbox', -0.05, 1.0, 0, 0.92, 0.12, 0.84, 0x5a3a2a),
    P('rbox', 0.2, 1.08, 0, 0.3, 0.1, 0.86, IRON),
    P('rbox', -0.25, 1.08, 0, 0.3, 0.1, 0.86, IRON),
    P('rbox', 0.66, 0.62, 0, 0.42, 0.46, 0.5, shadeHex(0x6a3a2a, 0.9)),
    P('cone8', 0.88, 0.72, 0, 0.18, 0.52, 0.18, HORN, { roll: -1.2 }),
    P('cone8', 0.78, 0.92, 0, 0.1, 0.24, 0.1, HORN, { roll: -0.6 }),
    P('sphere', 0.82, 0.74, 0.2, 0.06, 0.06, 0.06, 0xffa040, { e: 0.7 }),
    P('sphere', 0.82, 0.74, -0.2, 0.06, 0.06, 0.06, 0xffa040, { e: 0.7 }),
    P('cone8', -0.58, 0.7, 0, 0.1, 0.3, 0.1, T, { roll: 1.8 }),
  ],
  // Heilerschamane: Maskenträger mit Stab und Federn
  shaman: [
    P('rbox', 0, 0.12, 0.12, 0.13, 0.24, 0.12, 0x3a2a1a, { leg: 0 }),
    P('rbox', 0, 0.12, -0.12, 0.13, 0.24, 0.12, 0x3a2a1a, { leg: Math.PI }),
    P('cone12', 0, 0.18, 0, 0.52, 0.66, 0.52, T),
    P('rbox', 0, 0.56, 0, 0.28, 0.22, 0.34, shadeHex(0x2a6a4a, 0.9)),
    P('sphere', 0.02, 0.88, 0, 0.28, 0.28, 0.28, 0xb89a70),
    P('rbox', 0.15, 0.9, 0, 0.04, 0.24, 0.22, 0xe8dcc0),
    P('rbox', 0.17, 0.92, 0.05, 0.02, 0.04, 0.05, 0x101010),
    P('rbox', 0.17, 0.92, -0.05, 0.02, 0.04, 0.05, 0x101010),
    P('leaf', -0.06, 1.0, 0, 0.3, 0.4, 0.3, 0xe04040, { roll: 1.1 }),
    P('leaf', -0.06, 1.0, 0, 0.3, 0.4, 0.3, 0x40a0e0, { roll: 1.1, yaw: 0.6 }),
    P('cyl6', 0.3, 0.1, 0.22, 0.04, 1.08, 0.04, WOOD, { arm: true }),
    P('gem', 0.3, 1.24, 0.22, 0.14, 0.18, 0.14, 0x80ffb0, { e: 0.9, bob: 0.04, spin: 1.5 }),
  ],
  // Tunnelgräber: Maulwurfsechse mit Bohrschnauze
  tunneler: [
    P('rbox', 0.2, 0.1, 0.28, 0.18, 0.18, 0.14, 0x6a5a30, { leg: 0 }),
    P('rbox', 0.2, 0.1, -0.28, 0.18, 0.18, 0.14, 0x6a5a30, { leg: Math.PI }),
    P('rbox', -0.25, 0.1, 0.26, 0.18, 0.18, 0.14, 0x6a5a30, { leg: Math.PI }),
    P('rbox', -0.25, 0.1, -0.26, 0.18, 0.18, 0.14, 0x6a5a30, { leg: 0 }),
    P('sphere', -0.05, 0.34, 0, 1.0, 0.5, 0.62, T),
    P('sphere', -0.1, 0.44, 0, 0.78, 0.3, 0.5, shadeHex(0x8a7440, 0.85)),
    P('cyl12', 0.42, 0.34, 0, 0.32, 0.18, 0.32, IRON, { roll: -Math.PI / 2 }),
    P('cone8', 0.58, 0.34, 0, 0.3, 0.42, 0.3, 0xb0a890, { roll: -Math.PI / 2, spin: 8 }),
    P('rbox', 0.4, 0.5, 0.14, 0.06, 0.06, 0.06, 0xffc040, { e: 0.6 }),
    P('rbox', 0.4, 0.5, -0.14, 0.06, 0.06, 0.06, 0xffc040, { e: 0.6 }),
  ],
  // Sturmgreif: Adlerkopf, Löwenleib, große Schwingen
  griffin: [
    P('sphere', -0.05, 0, 0, 0.8, 0.44, 0.46, T),
    P('sphere', 0.38, 0.14, 0, 0.32, 0.32, 0.3, 0xf0f0ff),
    P('cone4', 0.56, 0.1, 0, 0.12, 0.22, 0.1, 0xffc040, { roll: -Math.PI / 2 - 0.3 }),
    P('sphere', 0.46, 0.2, 0.08, 0.04, 0.04, 0.04, EYE),
    P('sphere', 0.46, 0.2, -0.08, 0.04, 0.04, 0.04, EYE),
    P('leaf', 0, 0.06, 0.16, 1.2, 1, 1.25, shadeHex(0x9a8ad0, 0.9), { wing: 1 }),
    P('leaf', 0, 0.06, -0.16, 1.2, 1, -1.25, shadeHex(0x9a8ad0, 0.9), { wing: -1 }),
    P('leaf', 0, 0.08, 0.16, 0.7, 1, 0.8, 0xe8e8ff, { wing: 1 }),
    P('leaf', 0, 0.08, -0.16, 0.7, 1, -0.8, 0xe8e8ff, { wing: -1 }),
    P('leaf', -0.45, 0, 0, 0.36, 1, 0.5, 0x8a7ac0, { yaw: Math.PI / 2 }),
    P('rbox', 0.18, -0.2, 0.12, 0.06, 0.18, 0.06, 0xffc040),
    P('rbox', 0.18, -0.2, -0.12, 0.06, 0.18, 0.06, 0xffc040),
  ],
};

export const BOSS_MODELS: Record<BossId, Part[]> = {
  // Risskoloss: Steinriese mit Rissherz und schwebenden Splittern
  riftColossus: [
    P('rbox', 0, 0.3, 0.32, 0.36, 0.6, 0.34, 0x2a1840, { leg: 0 }),
    P('rbox', 0, 0.3, -0.32, 0.36, 0.6, 0.34, 0x2a1840, { leg: Math.PI }),
    P('rbox', 0, 1.0, 0, 0.8, 0.9, 0.92, T),
    P('rbox', 0.05, 0.62, 0, 0.82, 0.14, 0.94, 0x1a1028),
    P('rbox', -0.04, 1.42, 0.52, 0.56, 0.3, 0.46, 0x4a2a6a, { roll: 0.2 }),
    P('rbox', -0.04, 1.42, -0.52, 0.56, 0.3, 0.46, 0x4a2a6a, { roll: 0.2 }),
    P('rbox', 0.16, 1.64, 0, 0.46, 0.4, 0.46, 0x2a1840),
    P('rbox', 0.4, 1.66, 0, 0.04, 0.06, 0.3, 0xff60ff, { e: 0.9 }),
    P('gem', 0.42, 1.06, 0, 0.12, 0.36, 0.3, 0xff60ff, { e: 1.0, glow: 0xff60ff }),
    P('rbox', 0.06, 0.96, 0.66, 0.28, 0.8, 0.28, 0x4a2a6a, { arm: true }),
    P('rbox', 0.18, 0.5, 0.68, 0.36, 0.32, 0.36, 0x3a2050, { arm: true }),
    P('rbox', 0.06, 0.96, -0.66, 0.28, 0.8, 0.28, 0x4a2a6a, { arm: true }),
    P('rbox', 0.18, 0.5, -0.68, 0.36, 0.32, 0.36, 0x3a2050, { arm: true }),
    P('octa', -0.1, 2.25, 0.62, 0.24, 0.42, 0.24, 0xd080ff, { e: 0.7, spin: 1.2, bob: 0.1 }),
    P('octa', -0.1, 2.25, -0.62, 0.24, 0.42, 0.24, 0xd080ff, { e: 0.7, spin: -1.2, bob: 0.1 }),
    P('octa', -0.3, 2.45, 0, 0.2, 0.34, 0.2, 0xd080ff, { e: 0.7, spin: 2, bob: 0.08 }),
  ],
  // Aschenkönigin: Feuerkönigin mit Flammenschwingen, Krone und Szepter
  ashQueen: [
    P('cone12', 0, 0, 0, 1.1, 1.3, 1.1, T),
    P('cone12', 0, 0.2, 0, 0.9, 0.9, 0.9, shadeHex(0x8a2a10, 0.8)),
    P('cyl12', 0, 1.1, 0, 0.42, 0.5, 0.4, 0x8a2a10),
    P('rbox', 0, 1.42, 0, 0.5, 0.12, 0.62, GOLD),
    P('sphere', 0.02, 1.82, 0, 0.36, 0.38, 0.36, 0xffd0a0),
    P('rbox', 0.18, 1.84, 0.07, 0.02, 0.04, 0.06, 0x401008),
    P('rbox', 0.18, 1.84, -0.07, 0.02, 0.04, 0.06, 0x401008),
    P('cyl12', 0, 1.98, 0, 0.34, 0.08, 0.34, GOLD),
    P('cone4', 0, 2.04, 0, 0.1, 0.32, 0.1, GOLD, { e: 0.4 }),
    P('cone4', 0.14, 2.02, 0, 0.08, 0.24, 0.08, GOLD, { e: 0.4 }),
    P('cone4', -0.14, 2.02, 0, 0.08, 0.24, 0.08, GOLD, { e: 0.4 }),
    P('cone4', 0, 2.02, 0.14, 0.08, 0.24, 0.08, GOLD, { e: 0.4 }),
    P('cone4', 0, 2.02, -0.14, 0.08, 0.24, 0.08, GOLD, { e: 0.4 }),
    P('leaf', -0.15, 1.45, 0.25, 1.0, 1, 1.15, 0xff6020, { wing: 0.35, e: 0.5, roll: -0.5 }),
    P('leaf', -0.15, 1.45, -0.25, 1.0, 1, -1.15, 0xff6020, { wing: -0.35, e: 0.5, roll: 0.5 }),
    P('leaf', -0.15, 1.5, 0.25, 0.6, 1, 0.75, 0xffc040, { wing: 0.35, e: 0.5, roll: -0.5 }),
    P('leaf', -0.15, 1.5, -0.25, 0.6, 1, -0.75, 0xffc040, { wing: -0.35, e: 0.5, roll: 0.5 }),
    P('cyl6', 0.36, 0.6, 0.36, 0.05, 1.3, 0.05, GOLD, { arm: true }),
    P('sphere', 0.36, 1.98, 0.36, 0.2, 0.2, 0.2, 0xffa040, { e: 1.0, glow: 0xffa040, arm: true }),
  ],
  // Nullhüter: Monolith mit Runenringen und Auge
  nullWarden: [
    P('cyl16', 0, 0, 0, 1.0, 0.3, 1.0, 0x1a3a44),
    P('cyl16', 0, 0.3, 0, 0.86, 1.3, 0.86, T),
    P('rbox', 0.38, 0.9, 0, 0.12, 0.9, 0.22, 0x0e2830),
    P('rbox', -0.38, 0.9, 0, 0.12, 0.9, 0.22, 0x0e2830),
    P('rbox', 0, 0.9, 0.38, 0.22, 0.9, 0.12, 0x0e2830),
    P('rbox', 0, 0.9, -0.38, 0.22, 0.9, 0.12, 0x0e2830),
    P('cone12', 0, 1.6, 0, 0.94, 0.6, 0.94, 0x205a68),
    P('torus', 0, 0.95, 0, 1.6, 1.6, 1.6, 0xa0f0ff, { e: 0.6, spin: 1.2 }),
    P('torus', 0, 1.35, 0, 1.25, 1.25, 1.25, 0x7ad0e0, { e: 0.5, spin: -1.6, roll: 0.3 }),
    P('sphere', 0.44, 1.2, 0, 0.3, 0.3, 0.3, 0xe8ffff, { e: 0.8 }),
    P('sphere', 0.56, 1.2, 0, 0.12, 0.16, 0.16, 0x0a2a30),
    P('octa', 0, 2.4, 0, 0.3, 0.5, 0.3, 0xa0f0ff, { e: 0.8, spin: 1, bob: 0.08, glow: 0x80f0ff }),
  ],
  // Schlund der Tiefe: massiger Rachen mit Zahnreihen und Stacheln
  maw: [
    P('rbox', -0.3, 0.24, 0.55, 0.32, 0.46, 0.32, 0x3a1a2a, { leg: 0 }),
    P('rbox', -0.3, 0.24, -0.55, 0.32, 0.46, 0.32, 0x3a1a2a, { leg: Math.PI }),
    P('rbox', 0.3, 0.24, 0.5, 0.3, 0.42, 0.3, 0x3a1a2a, { leg: Math.PI }),
    P('rbox', 0.3, 0.24, -0.5, 0.3, 0.42, 0.3, 0x3a1a2a, { leg: 0 }),
    P('sphere', -0.1, 0.82, 0, 1.5, 1.1, 1.4, T),
    P('sphere', -0.2, 1.1, 0, 1.2, 0.6, 1.1, shadeHex(0x5a2a48, 0.85)),
    P('sphere', 0.62, 0.66, 0, 0.6, 0.52, 1.04, 0x1a0610),
    P('cone8', 0.78, 0.9, 0.36, 0.1, 0.3, 0.1, HORN, { pitch: Math.PI }),
    P('cone8', 0.84, 0.92, 0.12, 0.1, 0.32, 0.1, HORN, { pitch: Math.PI }),
    P('cone8', 0.84, 0.92, -0.12, 0.1, 0.32, 0.1, HORN, { pitch: Math.PI }),
    P('cone8', 0.78, 0.9, -0.36, 0.1, 0.3, 0.1, HORN, { pitch: Math.PI }),
    P('cone8', 0.8, 0.4, 0.26, 0.1, 0.26, 0.1, HORN),
    P('cone8', 0.84, 0.4, 0, 0.1, 0.28, 0.1, HORN),
    P('cone8', 0.8, 0.4, -0.26, 0.1, 0.26, 0.1, HORN),
    P('sphere', 0.42, 1.24, 0.34, 0.14, 0.14, 0.14, 0xffe040, { e: 0.9 }),
    P('sphere', 0.42, 1.24, -0.34, 0.14, 0.14, 0.14, 0xffe040, { e: 0.9 }),
    P('cone8', -0.3, 1.45, 0.25, 0.22, 0.5, 0.22, 0x8a5a70, { roll: 0.5 }),
    P('cone8', -0.55, 1.35, 0, 0.24, 0.55, 0.24, 0x8a5a70, { roll: 0.7 }),
    P('cone8', -0.3, 1.45, -0.25, 0.22, 0.5, 0.22, 0x8a5a70, { roll: 0.5 }),
  ],
  // Zwillingswächter Sol: goldener Paladin mit Sonnenring
  twinSol: [
    P('cone12', 0, 0, 0, 0.84, 1.1, 0.84, T),
    P('rbox', 0, 1.12, 0, 0.5, 0.42, 0.6, shadeHex(0xffc040, 0.85)),
    P('rbox', -0.02, 1.32, 0.38, 0.36, 0.16, 0.3, GOLD, { roll: 0.3 }),
    P('rbox', -0.02, 1.32, -0.38, 0.36, 0.16, 0.3, GOLD, { roll: 0.3 }),
    P('sphere', 0.02, 1.62, 0, 0.36, 0.38, 0.36, 0xfff0d0),
    P('cone8', 0.02, 1.7, 0, 0.4, 0.34, 0.4, GOLD),
    P('torus', -0.18, 1.7, 0, 1.0, 1.0, 1.0, GOLD, { e: 0.7, spin: 1.2, pitch: Math.PI / 2, glow: 0xffb040 }),
    P('cyl6', 0.38, 0.4, 0.3, 0.06, 1.3, 0.06, 0xd0a040, { arm: true }),
    P('cone4', 0.38, 1.7, 0.3, 0.16, 0.4, 0.16, 0xffe080, { arm: true, e: 0.5 }),
  ],
  // Zwillingswächter Luna: silberne Mondseherin mit Sichel und Schleier
  twinLuna: [
    P('cone12', 0, 0, 0, 0.84, 1.1, 0.84, T),
    P('rbox', 0, 1.12, 0, 0.46, 0.4, 0.56, shadeHex(0xa0b8ff, 0.8)),
    P('sphere', 0.02, 1.6, 0, 0.34, 0.36, 0.34, 0xe8f0ff),
    P('cone12', -0.04, 1.56, 0, 0.5, 0.62, 0.5, 0x6a8ad0),
    P('rbox', 0.18, 1.6, 0, 0.02, 0.05, 0.16, 0x2a3a6a),
    P('torus', 0.1, 2.15, 0, 0.62, 0.62, 0.62, 0xc0d8ff, { e: 0.7, roll: 1.2, spin: 0.6, glow: 0x80b0ff }),
    P('leaf', -0.14, 0.9, 0.28, 0.8, 1, 0.9, 0x5a7ac0, { wing: 0.25, roll: -0.5 }),
    P('leaf', -0.14, 0.9, -0.28, 0.8, 1, -0.9, 0x5a7ac0, { wing: -0.25, roll: 0.5 }),
    P('cyl6', 0.36, 0.4, -0.3, 0.05, 1.2, 0.05, 0xc0c8d8, { arm: true }),
    P('torus', 0.36, 1.62, -0.3, 0.36, 0.36, 0.36, 0xd8e8ff, { arm: true, roll: Math.PI / 2, e: 0.4 }),
  ],
  // Weltenbrecher: Titan aus Erzplatten mit leuchtenden Schwachpunkten
  worldbreaker: [
    P('rbox', 0, 0.38, 0.44, 0.52, 0.76, 0.46, 0x2a3446, { leg: 0 }),
    P('rbox', 0, 0.38, -0.44, 0.52, 0.76, 0.46, 0x2a3446, { leg: Math.PI }),
    P('rbox', 0, 1.25, 0, 1.05, 1.0, 1.24, T),
    P('rbox', 0.06, 0.84, 0, 1.08, 0.18, 1.28, 0x2a3446),
    P('rbox', 0.08, 1.4, 0, 1.0, 0.16, 1.32, 0x6a7490),
    P('rbox', -0.06, 1.8, 0.72, 0.7, 0.4, 0.56, 0x4a5470, { roll: 0.2 }),
    P('rbox', -0.06, 1.8, -0.72, 0.7, 0.4, 0.56, 0x4a5470, { roll: 0.2 }),
    P('rbox', 0.24, 2.0, 0, 0.6, 0.5, 0.62, 0x3a4458),
    P('rbox', 0.55, 2.02, 0, 0.04, 0.08, 0.4, 0xff4040, { e: 0.9 }),
    P('rbox', 0.1, 1.2, 1.0, 0.4, 1.2, 0.4, 0x3a4458, { arm: true }),
    P('rbox', 0.24, 0.56, 1.02, 0.56, 0.5, 0.56, 0x2a3446, { arm: true }),
    P('rbox', 0.1, 1.2, -1.0, 0.4, 1.2, 0.4, 0x3a4458, { arm: true }),
    P('rbox', 0.24, 0.56, -1.02, 0.56, 0.5, 0.56, 0x2a3446, { arm: true }),
    P('cone4', -0.24, 2.3, 0.3, 0.22, 0.6, 0.22, 0x6a7490),
    P('cone4', -0.24, 2.3, -0.3, 0.22, 0.6, 0.22, 0x6a7490),
    P('cone4', -0.4, 2.2, 0, 0.24, 0.66, 0.24, 0x6a7490),
  ],
};

/** Positionen der Schwachpunkte des Weltenbrechers (leuchtende Kristalle). */
export const WEAKPOINT_POS: [number, number, number][] = [[0.55, 1.4, 0.4], [0.55, 1.4, -0.4], [-0.2, 2.3, 0]];

export const BOSS_HEIGHT: Record<BossId, number> = {
  riftColossus: 2.6, ashQueen: 2.4, nullWarden: 2.4, maw: 1.9, twinSol: 2.2, twinLuna: 2.2, worldbreaker: 2.9,
};

// Helden (Überarbeitung 2.5): Beine, Torso, Gürtel, Schultern, Kopf mit Gesicht, Umhang, Waffe.
// Blickrichtung +X, Höhe ≈ 1,1. Waffenteile mit arm: true schwingen beim Angriff.
const CAPE = (c: number): Part[] => [
  P('leaf', -0.16, 0.86, 0, 0.5, 1, 0.66, c, { pitch: Math.PI, roll: -1.35, wing: 0.08 }),
];
const LEGS = (c: number): Part[] => [
  P('rbox', 0, 0.14, 0.11, 0.14, 0.3, 0.13, c, { leg: 0 }),
  P('rbox', 0, 0.14, -0.11, 0.14, 0.3, 0.13, c, { leg: Math.PI }),
  P('rbox', 0.03, 0.02, 0.11, 0.2, 0.06, 0.14, 0x2a2018, { leg: 0 }),
  P('rbox', 0.03, 0.02, -0.11, 0.2, 0.06, 0.14, 0x2a2018, { leg: Math.PI }),
];
const FACE = (y: number, skin: number, eye = 0x201812): Part[] => [
  P('sphere', 0, y, 0, 0.3, 0.32, 0.3, skin),
  P('sphere', 0.13, y + 0.02, 0.06, 0.04, 0.05, 0.04, eye),
  P('sphere', 0.13, y + 0.02, -0.06, 0.04, 0.05, 0.04, eye),
];
export const HERO_MODELS: Record<HeroId, Part[]> = {
  // Klingenwächterin: Plattenrüstung, Schild, Langschwert, Umhang
  blade: [
    ...LEGS(0x30384a),
    ...CAPE(0x8a2a2a),
    P('rbox', 0, 0.5, 0, 0.32, 0.4, 0.4, T),
    P('rbox', 0, 0.34, 0, 0.34, 0.08, 0.42, 0x3a2a1a),
    P('rbox', 0, 0.68, 0.22, 0.2, 0.14, 0.18, STEEL, { roll: 0.2 }),
    P('rbox', 0, 0.68, -0.22, 0.2, 0.14, 0.18, STEEL, { roll: 0.2 }),
    ...FACE(0.86, SKIN),
    P('sphere', -0.02, 0.92, 0, 0.32, 0.26, 0.32, STEEL),
    P('rbox', 0.12, 0.86, 0, 0.04, 0.16, 0.04, STEEL),
    P('rbox', 0.18, 0.5, -0.3, 0.06, 0.5, 0.36, A),
    P('rbox', 0.2, 0.5, -0.3, 0.03, 0.3, 0.06, GOLD),
    P('rbox', 0.24, 0.44, 0.26, 0.06, 0.06, 0.18, GOLD, { arm: true }),
    P('rbox', 0.24, 0.8, 0.26, 0.05, 0.62, 0.08, 0xe0e8ff, { arm: true, e: 0.15 }),
  ],
  // Jägerin: Lederrüstung, Kapuze, Langbogen, Köcher
  hunter: [
    ...LEGS(0x4a3020),
    ...CAPE(0x3a5a2a),
    P('rbox', 0, 0.5, 0, 0.28, 0.38, 0.34, T),
    P('rbox', 0, 0.34, 0, 0.3, 0.06, 0.36, 0x2a1a10),
    ...FACE(0.84, SKIN),
    P('cone8', -0.03, 0.8, 0, 0.36, 0.42, 0.36, 0x3a5a2a, { roll: 0.25 }),
    P('cyl8', -0.18, 0.44, -0.08, 0.1, 0.46, 0.1, 0x6a4a2a, { roll: 0.3 }),
    P('rbox', -0.24, 0.92, -0.06, 0.03, 0.12, 0.03, 0xe04040),
    P('torus', 0.24, 0.56, 0.08, 0.66, 0.66, 0.4, WOOD, { roll: Math.PI / 2, arm: true }),
    P('cyl6', 0.24, 0.24, 0.08, 0.01, 0.66, 0.01, 0xe8e0d0, { arm: true }),
    P('sphere', 0.32, 0.56, 0.08, 0.06, 0.06, 0.06, A, { e: 0.6 }),
  ],
  // Risswandlerin: Klingenmantel, Maske, Doppeldolche
  riftwalker: [
    ...LEGS(0x201830),
    P('cone12', -0.02, 0.22, 0, 0.46, 0.56, 0.44, T),
    P('rbox', 0, 0.58, 0, 0.26, 0.26, 0.32, 0x302848),
    ...FACE(0.86, 0x302848, 0xd080ff),
    P('cone8', -0.04, 0.84, 0, 0.36, 0.42, 0.36, T, { roll: 0.3 }),
    P('rbox', 0.15, 0.82, 0, 0.03, 0.06, 0.2, A, { e: 0.8 }),
    P('cone4', 0.3, 0.48, 0.2, 0.05, 0.44, 0.05, A, { arm: true, e: 0.5, roll: -1.2 }),
    P('cone4', 0.3, 0.48, -0.2, 0.05, 0.44, 0.05, A, { arm: true, e: 0.5, roll: -1.2 }),
    P('leaf', -0.14, 0.7, 0.16, 0.36, 1, 0.5, T, { roll: -0.9, pitch: 2.4, wing: 0.12 }),
    P('leaf', -0.14, 0.7, -0.16, 0.36, 1, -0.5, T, { roll: 0.9, pitch: -2.4, wing: -0.12 }),
  ],
  // Runenbardin: Robe, Federhut, Laute
  bard: [
    P('cone12', 0, 0, 0, 0.56, 0.62, 0.56, T),
    P('rbox', 0, 0.56, 0, 0.26, 0.24, 0.32, T),
    P('rbox', 0, 0.44, 0, 0.3, 0.06, 0.36, GOLD),
    ...FACE(0.84, SKIN),
    P('cyl12', -0.02, 0.92, 0, 0.5, 0.05, 0.5, 0x8a3a6a),
    P('cyl12', -0.02, 0.94, 0, 0.3, 0.14, 0.3, 0x8a3a6a),
    P('leaf', -0.1, 1.02, 0.08, 0.3, 0.5, 0.4, 0xffd060, { roll: 1.2 }),
    P('sphere', 0.22, 0.5, 0.16, 0.2, 0.24, 0.1, WOOD, { arm: true }),
    P('rbox', 0.3, 0.72, 0.16, 0.04, 0.32, 0.04, WOOD_D, { arm: true, roll: -0.4 }),
    P('ringThin', 0, 0.04, 0, 0.5, 1, 0.5, A, { e: 0.5, spin: 1 }),
  ],
  // Schmiedemeister: Lederschurz, Bart, Kriegshammer
  smith: [
    ...LEGS(0x3a2a1a),
    P('rbox', 0, 0.5, 0, 0.42, 0.42, 0.48, T),
    P('rbox', 0.12, 0.44, 0, 0.22, 0.42, 0.42, 0x6a4a2a),
    P('rbox', 0, 0.7, 0.26, 0.22, 0.12, 0.18, IRON, { roll: 0.2 }),
    P('rbox', 0, 0.7, -0.26, 0.22, 0.12, 0.18, IRON, { roll: 0.2 }),
    ...FACE(0.86, 0xe0b898),
    P('cone8', 0.1, 0.84, 0, 0.26, 0.26, 0.26, 0xc07040, { pitch: Math.PI }),
    P('sphere', -0.02, 0.98, 0, 0.26, 0.12, 0.26, 0x5a3a20),
    P('cyl6', 0.28, 0.36, 0.28, 0.05, 0.56, 0.05, WOOD, { arm: true }),
    P('rbox', 0.28, 0.94, 0.28, 0.26, 0.18, 0.18, IRON, { arm: true }),
    P('rbox', 0.28, 0.94, 0.28, 0.12, 0.2, 0.2, COPPER, { arm: true }),
  ],
  // Wettermagier: Sternenrobe, Spitzhut, Stab mit Wolkenkugel
  weather: [
    P('cone12', 0, 0, 0, 0.56, 0.68, 0.56, T),
    P('rbox', 0, 0.58, 0, 0.24, 0.22, 0.3, T),
    P('rbox', 0, 0.46, 0, 0.28, 0.05, 0.34, 0xc0e8ff),
    ...FACE(0.84, 0xe8d0c0),
    P('sphere', 0.08, 0.72, 0, 0.18, 0.2, 0.2, 0xe8e8f0),
    P('cyl12', -0.02, 0.92, 0, 0.5, 0.04, 0.5, 0x2a4a8a),
    P('cone8', -0.02, 0.93, 0, 0.32, 0.56, 0.32, 0x2a4a8a, { roll: 0.2 }),
    P('cyl6', 0.26, 0.12, 0.18, 0.04, 1.06, 0.04, WOOD, { arm: true }),
    P('sphere', 0.26, 1.24, 0.18, 0.18, 0.18, 0.18, A, { e: 0.8, bob: 0.04, glow: 0xc0e8ff }),
  ],
  // Mystikerin: schwebende Seherin mit Runenring und Kristallkrone
  mystic: [
    P('cone12', 0, 0.05, 0, 0.56, 0.66, 0.56, T),
    P('rbox', 0, 0.6, 0, 0.24, 0.22, 0.3, T),
    P('rbox', 0, 0.48, 0, 0.28, 0.05, 0.34, A),
    ...FACE(0.86, 0xc8a8e8),
    P('cone12', -0.03, 0.82, 0, 0.36, 0.36, 0.36, shadeHex(0x6a4ab0, 0.8)),
    P('torus', 0, 0.5, 0, 0.86, 0.86, 0.86, A, { e: 0.5, spin: 1.6, roll: 0.3 }),
    P('gem', 0, 1.2, 0, 0.14, 0.22, 0.14, A, { e: 0.8, spin: 2, bob: 0.06, glow: 0xc0a0ff }),
    P('leaf', -0.12, 0.62, 0, 0.42, 1, 0.56, shadeHex(0x6a4ab0, 0.7), { pitch: Math.PI, roll: -1.35 }),
  ],
  // Goldsucher: Grubenhelm mit Lampe, Rucksack, Spitzhacke
  prospector: [
    ...LEGS(0x4a3a20),
    P('rbox', 0, 0.5, 0, 0.3, 0.38, 0.34, T),
    P('rbox', 0, 0.34, 0, 0.32, 0.06, 0.36, 0x3a2a14),
    ...FACE(0.84, SKIN),
    P('sphere', -0.01, 0.92, 0, 0.32, 0.22, 0.32, 0xc09a30),
    P('cyl12', 0, 0.9, 0, 0.42, 0.04, 0.42, 0xc09a30),
    P('cyl8', 0.16, 0.94, 0, 0.08, 0.06, 0.08, 0xfff0b0, { roll: -Math.PI / 2, e: 0.8 }),
    P('rbox', -0.24, 0.52, 0, 0.18, 0.32, 0.3, 0x6a4a2a),
    P('cyl6', 0.26, 0.3, 0.22, 0.04, 0.6, 0.04, WOOD, { arm: true }),
    P('cone4', 0.26, 0.86, 0.22, 0.06, 0.34, 0.06, STEEL, { arm: true, roll: -Math.PI / 2 }),
    P('gem', -0.24, 0.74, 0.08, 0.1, 0.1, 0.1, GOLD, { e: 0.5 }),
  ],
  // Stilleweber: Mönch mit Augenbinde, Gebetsring und Glockenstab
  silence: [
    P('cyl12', 0, 0, 0, 0.56, 0.66, 0.56, T),
    P('rbox', 0, 0.62, 0, 0.26, 0.24, 0.32, T),
    P('rbox', 0, 0.48, 0, 0.3, 0.06, 0.36, 0x1a3a44),
    ...FACE(0.9, 0x9ab0b8),
    P('rbox', 0.12, 0.92, 0, 0.06, 0.06, 0.32, A, { e: 0.6 }),
    P('cone12', -0.03, 0.86, 0, 0.36, 0.3, 0.36, 0x1a3a44),
    P('torus', 0, 0.62, 0, 0.92, 0.92, 0.92, A, { e: 0.4, spin: -0.8 }),
    P('cyl6', 0.3, 0.06, 0.24, 0.05, 1.12, 0.05, 0x8ab0b8, { arm: true }),
    P('cone8', 0.3, 1.1, 0.24, 0.16, 0.14, 0.16, 0xc0a060, { arm: true, pitch: Math.PI }),
  ],
};

