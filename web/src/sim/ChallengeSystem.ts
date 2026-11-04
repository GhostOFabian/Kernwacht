import { BOSSES } from '../config/enemies.ts';
import { CHALLENGE, challengeHpMul } from '../config/scaling.ts';
import type { EnemyId, EnemyInstance } from '../core/types.ts';
import type { GameSimulation } from './GameSimulation.ts';
import { spawnEnemyAt } from './EnemySystem.ts';

// 3.1 Challenge-Bosse: stark verstärkte Bosse mit eigenen Mechaniken – eine echte Prüfung für den Aufbau.
//  · 3,5× LP, +15 % auf alle Resistenzen, Kontrolleffekte wirken nur zu 35 %
//  · alle 15 s Schildschub (+8 % LP, höchstens 30 %), alle 12 s 3 Elite-Begleiter
//  · nach 60 s Raserei (+40 % Tempo)
//  · Durchbruch kostet mindestens 10 bzw. 25 % der maximalen Kernenergie; Sieg: 5× Kopfgeld

export function makeChallenge(sim: GameSimulation, e: EnemyInstance): void {
  e.challenge = true;
  const mul = challengeHpMul(sim.s.wave);
  e.maxHp *= mul;
  e.hp = e.maxHp;
  e.shield *= mul;
  e.maxShield = Math.max(e.maxShield * mul, e.shield);
  e.reward *= CHALLENGE.rewardMul;
  e.leak = Math.max(CHALLENGE.leakMin, Math.ceil(sim.s.maxLives * CHALLENGE.leakPct), e.leak);
  e.chShieldCd = CHALLENGE.shieldEvery;
  e.chSummonCd = CHALLENGE.summonEvery * 0.5;
  const name = e.boss ? BOSSES[e.boss].name : 'Boss';
  sim.emit({ k: 'message', text: `⚔ CHALLENGE: ${name} – Schildschübe, Begleiter, Raserei nach ${CHALLENGE.enrageAfter} s. Ein Durchbruch kostet ${e.leak} Kernenergie!`, severity: 'danger' });
}

const SUMMONS: EnemyId[] = ['brute', 'shield', 'runner', 'golem'];

export function updateChallenge(sim: GameSimulation, e: EnemyInstance, dt: number): void {
  if (!e.challenge || !e.alive) return;
  e.chShieldCd = (e.chShieldCd ?? CHALLENGE.shieldEvery) - dt;
  if (e.chShieldCd <= 0) {
    e.chShieldCd = CHALLENGE.shieldEvery;
    const cap = e.maxHp * CHALLENGE.shieldMax;
    e.shield = Math.min(cap, e.shield + e.maxHp * CHALLENGE.shieldPct);
    e.maxShield = Math.max(e.maxShield, e.shield);
    sim.emit({ k: 'burst', x: e.x, y: e.y, r: 1.4 });
  }
  e.chSummonCd = (e.chSummonCd ?? CHALLENGE.summonEvery) - dt;
  if (e.chSummonCd <= 0) {
    e.chSummonCd = CHALLENGE.summonEvery;
    for (let k = 0; k < CHALLENGE.summonCount; k++) {
      const id = SUMMONS[(sim.s.tick + k) % SUMMONS.length];
      const m = spawnEnemyAt(sim, id, true, e);
      m.x += (k - 1) * 0.3;
    }
    sim.emit({ k: 'message', text: 'Der Challenge-Boss ruft Begleiter!', severity: 'warn' });
  }
  if (!e.chEnraged && sim.s.time - e.spawnedAt >= CHALLENGE.enrageAfter) {
    e.chEnraged = true;
    e.speed *= CHALLENGE.enrageSpeed;
    sim.emit({ k: 'message', text: 'Der Challenge-Boss rast!', severity: 'danger' });
  }
}
