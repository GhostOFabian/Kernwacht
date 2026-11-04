import { DIFFICULTY } from '../config/balance.ts';
import type { GameState } from '../core/types.ts';

// Punktewertung einer Partie. Rein abgeleitet aus dem Spielzustand (keine zweite Datenquelle).

const DIFF_MUL = { easy: 0.8, normal: 1, hard: 1.3 } as const;

export interface ScoreBreakdown {
  waves: number;
  kills: number;
  bosses: number;
  lives: number;
  victory: number;
  multiplier: number;
  total: number;
}

export function scoreBreakdown(s: GameState): ScoreBreakdown {
  const waves = s.stats.wavesCleared * 100;
  const kills = s.stats.kills * 2;
  const bosses = s.stats.bossesKilled.length * 500;
  const victory = s.phase === 'victory' ? 1500 : 0;
  const lives = s.phase === 'victory' ? Math.max(0, s.lives) * 40 : 0;
  const multiplier = DIFF_MUL[s.difficulty] * (s.map.variant === 'mazing' ? 1.1 : 1);
  const total = Math.round((waves + kills + bosses + victory + lives) * multiplier);
  return { waves, kills, bosses, lives, victory, multiplier, total };
}

export function computeScore(s: GameState): number {
  return scoreBreakdown(s).total;
}

/** Kampagnensterne: 3 ab 80 % Kernenergie, 2 ab 40 %, sonst 1 (nur bei Sieg). */
export function campaignStars(s: GameState): number {
  if (s.phase !== 'victory') return 0;
  const f = s.lives / Math.max(1, s.maxLives);
  return f >= 0.8 ? 3 : f >= 0.4 ? 2 : 1;
}

export function difficultyName(s: GameState): string {
  return DIFFICULTY[s.difficulty].name;
}
