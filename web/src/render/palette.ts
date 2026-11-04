import type { DamageType, StatusId } from '../core/types.ts';
import type { MeshId } from './Renderer.ts';

export const DAMAGE_COLORS: Record<DamageType, number> = {
  physical: 0xe8dcc8,
  fire: 0xff7030,
  frost: 0x90d8ff,
  lightning: 0xffe860,
  poison: 0x9ae040,
  arcane: 0xc070ff,
  sonic: 0x60e8d4,
};

export const DAMAGE_NAMES: Record<DamageType, string> = {
  physical: 'Physisch', fire: 'Feuer', frost: 'Frost', lightning: 'Blitz', poison: 'Gift', arcane: 'Arkan', sonic: 'Schall',
};

/** Status-Symbole im 3D-Raum: Form + Farbe (nie nur Farbe). */
export const STATUS_GLYPH: Partial<Record<StatusId, { mesh: MeshId; color: number; sy: number }>> = {
  burn: { mesh: 'cone6', color: 0xff7030, sy: 1.4 },
  chill: { mesh: 'octa', color: 0x90d8ff, sy: 1 },
  freeze: { mesh: 'box', color: 0xe8fbff, sy: 1 },
  wet: { mesh: 'sphereLo', color: 0x4080ff, sy: 1.2 },
  poison: { mesh: 'sphere', color: 0x9ae040, sy: 1 },
  mark: { mesh: 'octa', color: 0xc070ff, sy: 1.6 },
  brittle: { mesh: 'cone4', color: 0xe0e0e0, sy: 1 },
  silence: { mesh: 'ringThin', color: 0x60e8d4, sy: 1 },
  riftMark: { mesh: 'torus', color: 0xff60ff, sy: 1 },
  stun: { mesh: 'cone4', color: 0xffff80, sy: -1 },
  root: { mesh: 'box', color: 0xe060e0, sy: 0.5 },
  tether: { mesh: 'torus', color: 0xff9aff, sy: 1 },
  reveal: { mesh: 'ring', color: 0xfff080, sy: 1 },
  voidMark: { mesh: 'gem', color: 0x8a40ff, sy: 1 },
  shred: { mesh: 'cone4', color: 0xd03030, sy: -1 },
  pull: { mesh: 'torus', color: 0xa080ff, sy: 0.6 },
};
