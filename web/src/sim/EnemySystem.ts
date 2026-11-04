import { bossHpMul, DIFFICULTY } from '../config/balance.ts';
import { updateChallenge } from './ChallengeSystem.ts';
import { FLYER, SCALING } from '../config/scaling.ts';
import { ABILITIES, BOSSES, contactDpsOf, ELITE, ENEMIES } from '../config/enemies.ts';
import { TOWERS } from '../config/towers.ts';
import { DAMAGE_TYPES, type AbilityId, type BossId, type DamageType, type EnemyId, type EnemyInstance, type MutationId } from '../core/types.ts';
import { spawnsOf } from './WorldGenerator.ts';
import type { GameSimulation } from './GameSimulation.ts';
import { addStatus, canAct, hasStatus, isImmobile, slowFactor, tickStatuses } from './ElementSystem.ts';
import { INF, nextCell, previousCell } from './Pathfinding.ts';
import { initBossAbilities } from './BossSystem.ts';

function zeroByType(): Record<DamageType, number> {
  const r = {} as Record<DamageType, number>;
  for (const t of DAMAGE_TYPES) r[t] = 0;
  return r;
}

/** Erzeugt einen Gegner mit wellenabhängigen Werten. */
export function makeEnemy(sim: GameSimulation, id: EnemyId, elite: boolean, boss: BossId | null, mutation: MutationId | null = null): EnemyInstance {
  const s = sim.s;
  const wave = s.currentWave;
  const diff = DIFFICULTY[s.difficulty];
  const def = ENEMIES[id];
  let hp: number;
  let speed: number;
  let reward: number;
  let leak: number;
  let poise = 0;
  let shield = 0;
  if (boss) {
    const b = BOSSES[boss];
    hp = b.hp * bossHpMul(s.wave, s.rules.endless) * diff.hpMul * sim.c.mods.bossHpMul;
    speed = b.speed;
    reward = b.reward * (wave?.rewardMul ?? 1);
    leak = b.leak * (s.rules.endless && s.wave >= SCALING.pressureFrom ? SCALING.bossLeakMul : 1);
    poise = b.poise;
    shield = hp * b.shieldPct;
  } else {
    hp = def.hp * (wave?.hpMul ?? 1) * diff.hpMul * (elite ? ELITE.hpMul : 1);
    speed = def.speed * (elite ? 0.9 : 1);
    reward = def.reward * (wave?.rewardMul ?? 1) * (elite ? ELITE.rewardMul : 1);
    leak = def.leak * (elite ? ELITE.leakMul : 1);
    shield = hp * def.shieldPct;
  }
  // 3.12: Flieger profitieren nur halb von Tempo-Zuschlägen (Weltstufe, „Flink“, Wellenprofil) und haben eine Obergrenze
  const sm = wave?.speedMul ?? 1;
  if (!boss && def.flying) speed = Math.min(FLYER.maxSpeed, speed * (1 + (sm - 1) * FLYER.speedMulShare));
  else speed *= sm;
  if (mutation === 'hasty') speed *= 1.35;
  const abilities: AbilityId[] = [...def.abilities];
  if (elite && !def.flying && !abilities.includes(ELITE.ability)) abilities.push(ELITE.ability);
  const starts = spawnsOf(s.map);
  const spawnIndex = boss ? 0 : (s.nextUid % starts.length);
  const sp = starts[spawnIndex];
  const plateCount = boss ? BOSSES[boss].weakPoints ?? 0 : 0;
  const plateMax = plateCount ? hp * 0.12 : 0;
  const e: EnemyInstance = {
    uid: sim.uid(), def: boss ? 'boss' : id, boss, elite,
    x: sp.x + 0.5, y: sp.y + 0.5, nx: sp.x, ny: sp.y,
    hp, maxHp: hp, shield, maxShield: shield, speed,
    flying: boss ? false : def.flying, route: def.route,
    traveled: 0, remaining: INF, statuses: [],
    abilities: abilities.map((a) => ({ id: a, cd: 2 + sim.rand() * 3, windup: 0, tx: 0, ty: 0, r: 0 })),
    poise, maxPoise: poise, phase: 0, phaseShift: 0,
    adapt: zeroByType(), adaptHits: zeroByType(),
    reward, leak, alive: true, rewarded: false, riftCd: 0, knockCd: 0, reactCds: {}, abilityMul: 1,
    wave: s.wave, spawnedAt: s.time, hitFlash: 0, reshieldUsed: false,
    mutation, stealthed: !!def.stealth && !boss, burrowed: false, surfaced: false, spawnIndex,
    plates: new Array<number>(plateCount).fill(plateMax), plateMax, swallowed: [], blockedBy: 0, enraged: false,
  };
  // 3.0: Weltstufe und Wellen-Affixe
  if (wave?.tier) e.tier = wave.tier;
  if (wave?.affixes?.length) {
    e.affixes = wave.affixes;
    if (wave.immune) e.immune = wave.immune;
    if (wave.affixes.includes('shielded')) {
      e.shield += e.maxHp * 0.25;
      e.maxShield = e.shield;
    }
    if (wave.affixes.includes('jammer') && elite) e.jamCd = 4 + sim.rand() * 4;
  }
  if (boss) initBossAbilities(sim, e);
  return e;
}

export function spawnEnemy(sim: GameSimulation, id: EnemyId, elite: boolean, boss: BossId | null, mutation: MutationId | null = null): EnemyInstance {
  const e = makeEnemy(sim, id, elite, boss, mutation);
  sim.s.enemies.push(e);
  sim.c.enemyByUid.set(e.uid, e);
  if (boss) {
    const partner = BOSSES[boss].partner;
    if (partner && !sim.s.enemies.some((o) => o.alive && o.boss === partner)) {
      const p = makeEnemy(sim, 'boss', false, partner);
      p.x -= 0.4;
      sim.s.enemies.push(p);
      sim.c.enemyByUid.set(p.uid, p);
      if (!sim.s.stats.bossesFaced.includes(partner)) sim.s.stats.bossesFaced.push(partner);
    }
    sim.s.boss = { uid: e.uid, id: boss, phase: 0, phaseName: BOSSES[boss].phases[0].name };
    if (!sim.s.stats.bossesFaced.includes(boss)) sim.s.stats.bossesFaced.push(boss);
    sim.emit({ k: 'bossPhase', boss, phase: 0, name: BOSSES[boss].phases[0].name });
    sim.emit({ k: 'message', text: `${BOSSES[boss].name} erscheint!`, severity: 'danger' });
  }
  return e;
}

/** Erzeugt einen Gegner an der Position eines anderen (Brut, Beschwörung). */
export function spawnEnemyAt(sim: GameSimulation, id: EnemyId, elite: boolean, at: EnemyInstance): EnemyInstance {
  const e = makeEnemy(sim, id, elite, null);
  e.x = at.x;
  e.y = at.y;
  e.nx = at.boss || at.flying ? Math.floor(at.x) : at.nx;
  e.ny = at.boss || at.flying ? Math.floor(at.y) : at.ny;
  e.traveled = at.traveled;
  e.remaining = at.remaining;
  if (e.flying) {
    e.nx = Math.floor(e.x);
    e.ny = Math.floor(e.y);
  }
  sim.s.enemies.push(e);
  sim.c.enemyByUid.set(e.uid, e);
  return e;
}

export function enemyCell(e: EnemyInstance): { x: number; y: number } {
  return { x: Math.floor(e.x), y: Math.floor(e.y) };
}

/** Unterbricht Fähigkeiten. Bosse verlieren Standfestigkeit; bei 0 werden sie taumelnd unterbrochen. */
export function interruptEnemy(sim: GameSimulation, e: EnemyInstance, power: number): void {
  if (!e.alive || power <= 0) return;
  if (e.boss) {
    e.poise -= power;
    if (e.poise <= 0) {
      e.poise = e.maxPoise;
      let canceled = false;
      for (const a of e.abilities) {
        if (a.windup > 0 && ABILITIES[a.id].interruptible) {
          a.windup = 0;
          a.cd = ABILITIES[a.id].cooldown * 0.6;
          canceled = true;
          sim.emit({ k: 'ability', id: a.id, x: e.x, y: e.y, stage: 'interrupt' });
        }
      }
      addStatus(e, 'stun', 1.5, 1, 0);
      sim.s.zones = sim.s.zones.filter((z) => !(z.src === e.uid && z.armed > 0 && z.kind !== 'fireZone'));
      sim.emit({ k: 'message', text: canceled ? 'Bossfähigkeit unterbrochen! Boss taumelt.' : 'Standfestigkeit gebrochen – Boss taumelt!', severity: 'info' });
    }
    return;
  }
  let canceled = false;
  for (const a of e.abilities) {
    if (a.windup > 0 && ABILITIES[a.id].interruptible) {
      a.windup = 0;
      a.cd = ABILITIES[a.id].cooldown;
      canceled = true;
      sim.emit({ k: 'ability', id: a.id, x: e.x, y: e.y, stage: 'interrupt' });
    }
  }
  if (canceled) sim.s.zones = sim.s.zones.filter((z) => !(z.src === e.uid && z.armed > 0));
}

/** Versetzt einen Gegner entlang seiner Route zurück (Rissbinder, Rückstoß). */
export function riftTeleport(sim: GameSimulation, e: EnemyInstance, cells: number): void {
  if (!e.alive || e.boss || cells <= 0) return;
  const ox = e.x;
  const oy = e.y;
  const map = sim.s.map;
  {
    // Auch Flieger werden entlang ihres Weges zurückversetzt.
    const field = sim.c.field;
    let cx = e.nx;
    let cy = e.ny;
    let moved = 0;
    for (let i = 0; i < Math.round(cells); i++) {
      const p = previousCell(map, field, cx, cy);
      if (!p) break;
      cx = p.x;
      cy = p.y;
      moved++;
    }
    if (moved === 0) return;
    e.x = cx + 0.5;
    e.y = cy + 0.5;
    e.nx = cx;
    e.ny = cy;
    e.traveled = Math.max(0, e.traveled - moved);
  }
  sim.emit({ k: 'teleport', x: ox, y: oy, x2: e.x, y2: e.y });
}

export function updateEnemies(sim: GameSimulation, dt: number): void {
  const s = sim.s;
  // Index-Schleife: Während des Updates können neue Gegner (Brut) entstehen.
  const count = s.enemies.length;
  for (let i = 0; i < count; i++) {
    const e = s.enemies[i];
    if (!e.alive) continue;
    e.hitFlash = Math.max(0, e.hitFlash - dt);
    tickStatuses(sim, e, dt);
    if (!e.alive) continue;
    // Regeneration – unterbunden durch Gift oder Brand
    const def = ENEMIES[e.def];
    const regen = def.regenPct + (e.mutation === 'vampiric' ? 0.025 : 0) + (e.affixes?.includes('regen') ? 0.01 : 0);
    if (e.challenge) updateChallenge(sim, e, dt);
    // Affix „Störsender“: Elitegegner betäuben regelmäßig Türme in der Nähe
    if (e.jamCd !== undefined) {
      e.jamCd -= dt;
      if (e.jamCd <= 0) {
        e.jamCd = 8;
        let hit = 0;
        for (const t of s.towers) {
          if ((t.cx + 0.5 - e.x) ** 2 + (t.cy + 0.5 - e.y) ** 2 <= 1.5 * 1.5) {
            t.stunned = Math.max(t.stunned, 1);
            hit++;
          }
        }
        if (hit) sim.emit({ k: 'burst', x: e.x, y: e.y, r: 1.5 });
      }
    }
    if (regen > 0 && !hasStatus(e, 'poison') && !hasStatus(e, 'burn') && !hasStatus(e, 'shred')) {
      e.hp = Math.min(e.maxHp, e.hp + e.maxHp * regen * dt);
    }
    updateConcealment(sim, e);
    if (e.phaseShift > 0) e.phaseShift = Math.max(0, e.phaseShift - dt);
    if (e.boss && e.poise < e.maxPoise && !e.abilities.some((a) => a.windup > 0)) {
      e.poise = Math.min(e.maxPoise, e.poise + 6 * dt);
    }
    if (!e.boss) updateEnemyAbilities(sim, e, dt);
    moveEnemy(sim, e, dt);
    if (s.phase === 'defeat') return;
  }
}

function updateEnemyAbilities(sim: GameSimulation, e: EnemyInstance, dt: number): void {
  for (const a of e.abilities) {
    const def = ABILITIES[a.id];
    if (a.windup > 0) {
      if (!canAct(e)) {
        a.windup = 0;
        a.cd = def.cooldown * 0.5;
        continue;
      }
      a.windup -= dt;
      if (a.windup <= 0) {
        a.windup = 0;
        executeEnemyAbility(sim, e, a.id, a.tx, a.ty, a.r);
        a.cd = def.cooldown;
      }
      continue;
    }
    a.cd -= dt;
    if (a.cd > 0 || !canAct(e)) continue;
    // Bedingungen prüfen und ggf. Wirken beginnen (telegraphiert über windup)
    if (a.id === 'phaseShift') {
      a.windup = def.windup;
      a.tx = e.x;
      a.ty = e.y;
    } else if (a.id === 'netBite') {
      let best = 0;
      let bd = 2.0 * 2.0;
      for (const t of sim.s.towers) {
        if (!sim.c.linked.has(t.uid) || t.disrupted > 0) continue;
        const d = (t.cx + 0.5 - e.x) ** 2 + (t.cy + 0.5 - e.y) ** 2;
        if (d <= bd) {
          bd = d;
          best = t.uid;
        }
      }
      if (best) {
        const t = sim.c.towerByUid.get(best);
        if (t) {
          a.windup = def.windup;
          a.tx = t.cx + 0.5;
          a.ty = t.cy + 0.5;
          a.r = best;
          sim.emit({ k: 'ability', id: a.id, x: e.x, y: e.y, stage: 'warn' });
        }
      } else a.cd = 0.5;
    } else if (a.id === 'siegeSlam') {
      let best = 0;
      let bd = 1.9 * 1.9;
      for (const t of sim.s.towers) {
        if (t.stunned > 0 || TOWERS[t.def].attack === 'none') continue;
        const d = (t.cx + 0.5 - e.x) ** 2 + (t.cy + 0.5 - e.y) ** 2;
        if (d <= bd) {
          bd = d;
          best = t.uid;
        }
      }
      const t = best ? sim.c.towerByUid.get(best) : undefined;
      if (t) {
        a.windup = def.windup;
        a.tx = t.cx + 0.5;
        a.ty = t.cy + 0.5;
        a.r = best;
        sim.s.zones.push({ uid: sim.uid(), kind: 'siegeSlam', x: a.tx, y: a.ty, r: 0.7, t: 0.3, armed: def.windup, power: 4, tick: 0, link: 0, src: e.uid });
        sim.emit({ k: 'ability', id: a.id, x: e.x, y: e.y, stage: 'warn' });
      } else a.cd = 0.5;
    } else if (a.id === 'heal') {
      const hurt = sim.s.enemies.some((o) => o.alive && o !== e && o.hp < o.maxHp * 0.85 && (o.x - e.x) ** 2 + (o.y - e.y) ** 2 <= 6.25);
      if (hurt) {
        a.windup = def.windup;
        a.tx = e.x;
        a.ty = e.y;
        a.r = 2.5;
        sim.emit({ k: 'ability', id: a.id, x: e.x, y: e.y, stage: 'warn' });
      } else a.cd = 0.8;
    } else if (a.id === 'quake') {
      let n = 0;
      for (const t of sim.s.towers) if ((t.cx + 0.5 - e.x) ** 2 + (t.cy + 0.5 - e.y) ** 2 <= 2.2 * 2.2) n++;
      if (n >= 3) {
        a.windup = def.windup;
        a.tx = e.x;
        a.ty = e.y;
        a.r = 2.2;
        sim.s.zones.push({ uid: sim.uid(), kind: 'quake', x: e.x, y: e.y, r: 2.2, t: 0.4, armed: def.windup, power: 3, tick: 0, link: 0, src: e.uid });
        sim.emit({ k: 'ability', id: a.id, x: e.x, y: e.y, stage: 'warn' });
      } else a.cd = 0.5;
    }
  }
}

function executeEnemyAbility(sim: GameSimulation, e: EnemyInstance, id: AbilityId, tx: number, ty: number, r: number): void {
  sim.emit({ k: 'ability', id, x: e.x, y: e.y, stage: 'cast' });
  if (id === 'phaseShift') {
    e.phaseShift = 2;
  } else if (id === 'netBite') {
    const t = sim.c.towerByUid.get(r);
    if (t && (t.cx + 0.5 - e.x) ** 2 + (t.cy + 0.5 - e.y) ** 2 <= 2.6 * 2.6) {
      t.disrupted = 6;
      sim.c.networkDirty = true;
      sim.emit({ k: 'message', text: 'Ein Funkenfresser stört dein Netzwerk!', severity: 'warn' });
    }
  } else if (id === 'quake') {
    for (const t of sim.s.towers) {
      if ((t.cx + 0.5 - tx) ** 2 + (t.cy + 0.5 - ty) ** 2 <= r * r) t.stunned = Math.max(t.stunned, 3);
    }
  } else if (id === 'siegeSlam') {
    const t = sim.c.towerByUid.get(r);
    if (t && (t.cx + 0.5 - e.x) ** 2 + (t.cy + 0.5 - e.y) ** 2 <= 2.4 * 2.4) {
      t.stunned = Math.max(t.stunned, 4);
      sim.emit({ k: 'burst', x: t.cx + 0.5, y: t.cy + 0.5, r: 0.8 });
    }
  } else if (id === 'heal') {
    for (const o of sim.s.enemies) {
      if (!o.alive || (o.x - e.x) ** 2 + (o.y - e.y) ** 2 > r * r) continue;
      o.hp = Math.min(o.maxHp, o.hp + o.maxHp * (o.boss ? 0.03 : 0.12));
    }
    sim.emit({ k: 'heal', x: e.x, y: e.y, r });
  }
}

/** Tarnung und Eingraben: Detektoren (und Aufdeckung) machen Gegner sichtbar und angreifbar. */
/** Getarnte Gegner werden von normalen Türmen nur aus nächster Nähe erkannt. */
const CLOSE_DETECT = 1.6;
const HERO_DETECT = 1.8;
/** Strecke (Felder), die ein Tunnelgräber unter der Erde zurücklegt, bevor er auftaucht. */
const BURROW_DISTANCE = 7;

function updateConcealment(sim: GameSimulation, e: EnemyInstance): void {
  const def = ENEMIES[e.def];
  if (!def.stealth && !def.burrow) return;
  let detected = hasStatus(e, 'reveal');
  if (!detected) {
    for (const t of sim.s.towers) {
      if (t.stunned > 0 || TOWERS[t.def].attack === 'none') continue;
      // Detektoren decken in voller Reichweite auf, jeder andere Turm nur aus nächster Nähe.
      const st = sim.c.towerStats.get(t.uid);
      const r = TOWERS[t.def].detector ? (st ? st.range : 2.5) + 0.2 : CLOSE_DETECT;
      if ((t.cx + 0.5 - e.x) ** 2 + (t.cy + 0.5 - e.y) ** 2 <= r * r) {
        detected = true;
        break;
      }
    }
  }
  if (!detected) {
    for (const h of sim.s.heroes) {
      if (h.state === 'active' && (h.x - e.x) ** 2 + (h.y - e.y) ** 2 <= HERO_DETECT * HERO_DETECT) {
        detected = true;
        break;
      }
    }
  }
  if (def.stealth) e.stealthed = !detected;
  if (def.burrow) {
    if (!e.burrowed && !e.surfaced && e.traveled > 1.5 && !detected) {
      e.burrowed = true;
      sim.emit({ k: 'burrow', x: e.x, y: e.y, up: false });
    } else if (e.burrowed && (detected || e.remaining <= 5 || e.traveled > 1.5 + BURROW_DISTANCE)) {
      e.burrowed = false;
      e.surfaced = true;
      sim.emit({ k: 'burrow', x: e.x, y: e.y, up: true });
    }
  }
}

/** Kontaktschaden von Gegnern gegen einen Helden (pro Sekunde). */
export function enemyContactDps(e: EnemyInstance): number {
  if (e.boss) return 60;
  return contactDpsOf(ENEMIES[e.def]) * (e.elite ? 2.5 : 1);
}

function moveEnemy(sim: GameSimulation, e: EnemyInstance, dt: number): void {
  const s = sim.s;
  const map = s.map;
  let speed = e.speed * (1 - slowFactor(e));
  if (e.boss) speed *= BOSSES[e.boss].phases[Math.min(e.phase, BOSSES[e.boss].phases.length - 1)].speedMul;
  if (isImmobile(e)) speed = 0;
  if (e.blockedBy) speed = 0;
  if (e.burrowed) speed *= 1.3;
  if (e.enraged) speed *= 1.3;
  for (const a of e.abilities) {
    if (a.windup > 0) speed *= e.boss ? 0.5 : a.id === 'quake' ? 0 : 1;
  }
  let step = speed * dt;
  // Flieger folgen demselben Weg wie Bodentruppen (nur in der Luft schwebend dargestellt).
  const field = e.route === 'safe' ? sim.c.safeField : sim.c.field;
  let guard = 0;
  while (step > 1e-9 && guard++ < 8) {
    const tx = e.nx + 0.5;
    const ty = e.ny + 0.5;
    const dx = tx - e.x;
    const dy = ty - e.y;
    const d = Math.hypot(dx, dy);
    if (d > step) {
      e.x += (dx / d) * step;
      e.y += (dy / d) * step;
      e.traveled += step;
      step = 0;
      break;
    }
    e.x = tx;
    e.y = ty;
    e.traveled += d;
    step -= d;
    if (e.nx === map.goal.x && e.ny === map.goal.y) {
      leak(sim, e);
      return;
    }
    let n = nextCell(map, field, e.nx, e.ny);
    if (!n && field !== sim.c.field) n = nextCell(map, sim.c.field, e.nx, e.ny);
    if (!n) {
      // Sollte durch die Bauprüfung ausgeschlossen sein – sicherer Rückfall: direkt zum Ziel.
      n = { x: e.nx + Math.sign(map.goal.x - e.nx), y: e.ny + Math.sign(map.goal.y - e.ny) };
    }
    e.nx = n.x;
    e.ny = n.y;
  }
  const f = sim.c.field[e.ny * map.width + e.nx];
  e.remaining = (f < INF ? f : 0) + Math.hypot(e.nx + 0.5 - e.x, e.ny + 0.5 - e.y);
}

function leak(sim: GameSimulation, e: EnemyInstance): void {
  const s = sim.s;
  e.alive = false;
  e.rewarded = true; // kein Kopfgeld nach Durchbruch
  const loss = Math.max(1, Math.round(e.leak));
  s.lives = Math.max(0, s.lives - loss);
  s.stats.leaks++;
  s.stats.livesLost += loss;
  sim.emit({ k: 'leak', enemy: e.def, lives: loss });
  if (e.boss) sim.emit({ k: 'message', text: `${BOSSES[e.boss].name} hat den Kern erreicht!`, severity: 'danger' });
  if (s.lives <= 0 && s.phase !== 'defeat' && s.phase !== 'victory') {
    s.phase = 'defeat';
    sim.emit({ k: 'defeat' });
  }
}

