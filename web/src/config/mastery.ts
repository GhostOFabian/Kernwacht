import type { TowerId } from '../core/types.ts';

// 3.0 Turm-Meisterschaft (Helden-Endlos): Jeder Turmtyp sammelt dauerhaft Abschüsse über alle Läufe.
// Rang r ab 50·r² Abschüssen (50, 200, 450, 800 …), höchstens Rang 20; je Rang +1 % Schaden für diesen Turmtyp.

export const MASTERY = { perRank: 0.01, maxRank: 20, killsBase: 50 };

export function masteryRank(kills: number): number {
  return Math.min(MASTERY.maxRank, Math.floor(Math.sqrt(Math.max(0, kills) / MASTERY.killsBase)));
}

export function masteryNext(kills: number): number | null {
  const r = masteryRank(kills);
  return r >= MASTERY.maxRank ? null : MASTERY.killsBase * (r + 1) ** 2;
}

export function sanitizeMastery(raw: unknown): Partial<Record<TowerId, number>> {
  const out: Partial<Record<TowerId, number>> = {};
  if (typeof raw !== 'object' || raw === null) return out;
  for (const [k, v] of Object.entries(raw as Record<string, unknown>)) {
    const n = Math.floor(Number(v));
    if (Number.isFinite(n) && n > 0) out[k as TowerId] = Math.min(1e9, n);
  }
  return out;
}
