import { runPassive } from '../config/passiveTree.ts';
import { dropRune } from './RuneSystem.ts';
import { addResource } from './LootSystem.ts';
import { masteryRank } from '../config/mastery.ts';
import { CHALLENGE, SCALING } from '../config/scaling.ts';
import { runeKillGold } from './RuneSystem.ts';
import { NULL_SHIELD_MUL, BOSSES, ENEMIES, ELITE } from '../config/enemies.ts';
import { onEnemyLoot } from './LootSystem.ts';
import { TOWERS } from '../config/towers.ts';
import { STATUS_RULES } from '../config/elements.ts';
import type { DamageType, EnemyInstance, HitPayload } from '../core/types.ts';
import type { GameSimulation } from './GameSimulation.ts';
import { addStatus, getStatus, hasStatus, react } from './ElementSystem.ts';
import { interruptEnemy, riftTeleport, spawnEnemyAt } from './EnemySystem.ts';
import { onBossDeath, onBossShieldBroken } from './BossSystem.ts';
import { heroGoldMul, onEnemyKilled } from './HeroSystem.ts';

/** Effektive Resistenz eines Gegners gegen eine Schadensart (ohne Panzerbrechung). */
export function resistOf(sim: GameSimulation, e: EnemyInstance, type: DamageType): number {
  const base = e.boss ? BOSSES[e.boss].resist[type] ?? 0 : ENEMIES[e.def].resist[type] ?? 0;
  let r = base + (sim.s.rules.enemyResistBonus[type] ?? 0);
  if (e.elite) r += ELITE.resistBonus;
  if (e.mutation === 'armored') r += 0.25;
  // 3.0: Weltstufe (Rüstung), Affixe „Gepanzert“ und „Immunität“
  if (type === 'physical') {
    if (e.tier) r += Math.min(SCALING.tierArmorMax, SCALING.tierArmor * e.tier);
    if (e.affixes?.includes('armored')) r += 0.2;
  }
  if (e.immune === type) r = Math.max(r, 0.85);
  if (e.challenge) r += CHALLENGE.resist;
  return r;
}

/**
 * Wendet Schaden an. Reihenfolge: Resistenz (inkl. Panzerbrechung) → Markierung → Spröde/Fessel
 * → Phasenschild → Schild (adaptiv bzw. Nullschild) → Lebenspunkte → ggf. Tod.
 * @returns tatsächlich verursachter Schaden (Schild + LP).
 */
export function dealDamage(
  sim: GameSimulation, e: EnemyInstance, amount: number, type: DamageType, towerUid: number,
  armorPierce = 0,
): number {
  if (!e.alive || amount <= 0 || !Number.isFinite(amount)) return 0;
  let resist = resistOf(sim, e, type);
  if (type === 'physical' && resist > 0) resist *= 1 - Math.min(1, armorPierce);
  const mark = getStatus(e, 'mark');
  // Markierung senkt nur vorhandene Resistenzen – höchstens bis 0, sie erzeugt keine zusätzliche Schwäche (2.9.5)
  if (mark && resist > 0) resist = Math.max(0, resist - Math.min(STATUS_RULES.markHardCap, mark.power));
  if (type === 'physical') {
    const sh = getStatus(e, 'shred');
    if (sh && sh.flag > 0 && resist > STATUS_RULES.shredFloor) resist = Math.max(STATUS_RULES.shredFloor, resist - (sh.flag / 1000) * sh.stacks);
  }
  resist = Math.max(-1, Math.min(0.9, resist));
  let dmg = amount * (1 - resist);
  if (type === 'physical' && hasStatus(e, 'brittle')) dmg *= 1 + STATUS_RULES.brittleBonus;
  const tether = getStatus(e, 'tether');
  if (tether) dmg *= 1 + tether.power;
  const vm = getStatus(e, 'voidMark');
  if (vm) dmg *= 1 + vm.power;
  if (e.phaseShift > 0 && type === 'physical') {
    if (sim.s.tick % 6 === 0) sim.emit({ k: 'immune', x: e.x, y: e.y });
    return 0;
  }
  let dealt = 0;
  // Schwachpunkte (Weltenbrecher): Schaden trifft zuerst den vordersten Schwachpunkt, der Kern nur 10 %.
  if (e.plates.length > 0) {
    const toPlate = Math.min(e.plates[0], dmg);
    e.plates[0] -= dmg;
    dealt += toPlate;
    dmg *= 0.1;
    if (e.plates[0] <= 0) {
      e.plates.shift();
      sim.emit({ k: 'plateBreak', x: e.x, y: e.y, left: e.plates.length });
      sim.emit({ k: 'message', text: e.plates.length ? `Schwachpunkt zerstört! Noch ${e.plates.length}.` : 'Alle Schwachpunkte zerstört – der Kern liegt frei!', severity: 'info' });
      if (e.plates.length === 0) onBossShieldBroken(sim, e);
    }
  }
  if (e.shield > 0) {
    let mul: number;
    if (e.boss === 'nullWarden') mul = NULL_SHIELD_MUL[type] ?? 0.5;
    else {
      mul = 1 - 0.15 * e.adapt[type];
      e.adaptHits[type]++;
      if (e.adaptHits[type] >= 6 && e.adapt[type] < 3) {
        e.adaptHits[type] = 0;
        e.adapt[type]++;
      }
    }
    const eff = dmg * mul;
    const absorbed = Math.min(e.shield, eff);
    e.shield -= absorbed;
    dealt += absorbed;
    dmg = mul > 0 ? (eff - absorbed) / mul : 0;
    if (e.shield <= 0) {
      e.shield = 0;
      if (e.boss) onBossShieldBroken(sim, e);
    }
  }
  if (dmg > 0) {
    const hpDmg = Math.min(e.hp, dmg);
    e.hp -= dmg;
    dealt += hpDmg;
  }
  e.hitFlash = 0.12;
  if (towerUid) {
    const t = sim.c.towerByUid.get(towerUid);
    if (t) {
      t.damageDealt += dealt;
      sim.s.stats.damageByTower[t.def] = (sim.s.stats.damageByTower[t.def] ?? 0) + dealt;
    } else {
      const h = sim.s.heroes.find((x) => x.uid === towerUid);
      if (h) h.damageDealt += dealt;
    }
  }
  if (e.hp <= 0) killEnemy(sim, e, towerUid);
  return dealt;
}

/** Vollständiger Treffer eines Turmangriffs inkl. Kritisch, Reaktionen und Statuseffekten. */
export function applyHit(sim: GameSimulation, e: EnemyInstance, p: HitPayload, mult = 1): void {
  if (!e.alive) return;
  const st = p.stats;
  let dmg = p.damage * mult;
  if (e.flying && st.airDmgMul !== 1) dmg *= st.airDmgMul;
  if (st.critChance > 0 && sim.rand() < st.critChance) dmg *= st.critMult;
  if (st.maxHpPct > 0) dmg += e.maxHp * st.maxHpPct * (e.boss ? 0.25 : 1);
  if (dmg > 0) {
    dmg = react(sim, e, p.type, dmg, p.tower);
    dealDamage(sim, e, dmg, p.type, p.tower, st.armorPierce);
  }
  if (!e.alive) return;
  applyOnHitEffects(sim, e, p);
}

function applyOnHitEffects(sim: GameSimulation, e: EnemyInstance, p: HitPayload): void {
  const st = p.stats;
  const boss = e.boss !== null;
  if (st.burnDps > 0 && st.burnDur > 0) addStatus(e, 'burn', st.burnDur, st.burnDps, p.tower);
  if (st.chillPct > 0 && st.chillDur > 0) {
    const s = addStatus(e, 'chill', st.chillDur, st.chillPct, p.tower, 3);
    s.flag = Math.max(s.flag, Math.round(st.chillMax * 100));
  }
  if (st.freezeChance > 0 && sim.rand() < st.freezeChance) {
    if (boss) interruptEnemy(sim, e, 15);
    else if (!hasStatus(e, 'freezeImmune')) addStatus(e, 'freeze', st.freezeDur, 1, p.tower);
  }
  if (st.poisonDps > 0 && st.poisonDur > 0) {
    const s = addStatus(e, 'poison', st.poisonDur, st.poisonDps, p.tower, Math.max(1, Math.round(st.poisonMaxStacks)));
    if (st.poisonSlow > 0) s.flag = Math.max(s.flag, Math.round(st.poisonSlow * 100));
  }
  if (st.bleedDps > 0 && st.bleedDur > 0) {
    const s = addStatus(e, 'shred', st.bleedDur, st.bleedDps, p.tower, Math.max(1, Math.round(st.bleedMaxStacks)));
    s.flag = Math.max(s.flag, Math.round(st.shredPct * 1000));
  }
  if (st.markPct > 0 && st.markDur > 0) {
    addStatus(e, 'mark', st.markDur, st.markPct, p.tower);
    if (st.markSpread > 0) {
      const near = sim.s.enemies
        .filter((o) => o.alive && o !== e && (o.x - e.x) ** 2 + (o.y - e.y) ** 2 <= 2.25)
        .slice(0, Math.round(st.markSpread));
      for (const o of near) addStatus(o, 'mark', st.markDur * 0.75, st.markPct, p.tower);
    }
  }
  if (st.interrupt > 0) interruptEnemy(sim, e, st.interrupt);
  if (st.silenceDur > 0 && !boss) addStatus(e, 'silence', st.silenceDur, 1, p.tower);
  if (st.stunDur > 0) {
    if (boss) interruptEnemy(sim, e, 8);
    else addStatus(e, 'stun', st.stunDur, 1, p.tower);
  }
  if (st.brittleDur > 0) addStatus(e, 'brittle', st.brittleDur, STATUS_RULES.brittleBonus, p.tower);
  if (st.knockback > 0 && !boss && e.knockCd <= sim.s.time) {
    e.knockCd = sim.s.time + 2;
    riftTeleport(sim, e, st.knockback);
  }
}

/** Tod eines Gegners. Belohnungen werden pro Gegner garantiert nur einmal vergeben. */
export function killEnemy(sim: GameSimulation, e: EnemyInstance, towerUid: number): void {
  if (e.rewarded) return;
  e.rewarded = true;
  e.alive = false;
  e.hp = 0;
  const s = sim.s;
  // Affix „Splitter“: zerfällt in zwei Schwarmlinge (diese splitten nicht weiter)
  if (e.affixes?.includes('splitter') && !e.boss && !e.splitChild && !e.miniBoss) {
    for (let k = 0; k < 2; k++) {
      const c = spawnEnemyAt(sim, 'swarm', false, e);
      c.splitChild = true;
      c.x += (k ? 0.15 : -0.15);
    }
  }
  let gold = Math.max(1, Math.round(e.reward * sim.c.mods.killGoldMul * heroGoldMul(sim, e) * (1 + runPassive(sim.s.he).killGoldPct)));
  // Kopfgeldbüro (Goldgräber-Spezialisierung)
  for (const t of s.towers) {
    if (t.def !== 'goldmine') continue;
    const st = sim.stats(t);
    if (st.killBounty <= 0) continue;
    const dx = t.cx + 0.5 - e.x;
    const dy = t.cy + 0.5 - e.y;
    if (dx * dx + dy * dy <= st.bountyRange * st.bountyRange) {
      const b = Math.round(st.killBounty + (e.elite || e.boss ? st.eliteBounty : 0));
      gold += b;
      t.goldEarned += b;
    }
  }
  s.gold += gold;
  s.stats.goldEarned += gold;
  s.stats.kills++;
  const t = towerUid ? sim.c.towerByUid.get(towerUid) : undefined;
  if (t) t.kills++;
  // Turm-Meisterschaft (Helden-Endlos): Rangaufstieg macht die Turmwerte neu
  if (t && s.he?.mastery) {
    const before = masteryRank(s.he.mastery[t.def] ?? 0);
    s.he.mastery[t.def] = (s.he.mastery[t.def] ?? 0) + 1;
    const after = masteryRank(s.he.mastery[t.def] ?? 0);
    if (after > before) {
      sim.c.statsDirty = true;
      sim.emit({ k: 'message', text: `Meisterschaft ${TOWERS[t.def].name}: Rang ${after} (+${after} % Schaden)`, severity: 'info' });
    }
  }
  if (t && s.he?.runes?.length) {
    const g = runeKillGold(sim, t);
    if (g > 0) {
      s.gold += g;
      s.stats.goldEarned += g;
      t.goldEarned += g;
    }
  }
  if (e.boss) {
    s.stats.bossesKilled.push(e.boss);
    sim.emit({ k: 'message', text: `${BOSSES[e.boss].name} wurde besiegt! +${gold} Gold`, severity: 'info' });
  }
  sim.emit({ k: 'kill', enemy: e.def, x: e.x, y: e.y, gold, elite: e.elite, boss: e.boss !== null });
  if (s.he) onEnemyLoot(sim, e);
  sim.emit({ k: 'death', enemy: e.def, boss: e.boss, x: e.x, y: e.y, color: e.boss ? BOSSES[e.boss].color : ENEMIES[e.def].color, size: e.boss ? BOSSES[e.boss].size * 2 : ENEMIES[e.def].size * 2 * (e.elite ? 1.35 : 1) });
  onEnemyKilled(sim, e, towerUid);
  if (e.boss) onBossDeath(sim, e);
  if (e.challenge) {
    sim.emit({ k: 'message', text: '⚔ Challenge bestanden! 5× Kopfgeld, 3 Relikte und eine Rune.', severity: 'info' });
    if (s.he) {
      addResource(s.he, 'relic', 3);
      dropRune(sim, e.x, e.y);
    }
  }
  if (e.mutation) {
    s.stats.mutationsKilled++;
    if (e.mutation === 'explosive') {
      for (const tw of s.towers) {
        if (TOWERS[tw.def].attack === 'none') continue;
        if ((tw.cx + 0.5 - e.x) ** 2 + (tw.cy + 0.5 - e.y) ** 2 <= 1.6 * 1.6) tw.stunned = Math.max(tw.stunned, 2);
      }
      sim.emit({ k: 'burst', x: e.x, y: e.y, r: 1.6 });
    } else if (e.mutation === 'splitting' && !e.boss) {
      for (let i = 0; i < 2; i++) {
        const c = spawnEnemyAt(sim, e.def, false, e);
        c.maxHp = c.hp = e.maxHp * 0.3;
        c.reward = Math.max(1, e.reward * 0.15);
        c.x += (i - 0.5) * 0.25;
      }
    }
  }

  // Schwelbrand: Brand springt beim Tod über (nicht weiter übertragbar → keine Ketten).
  const burn = getStatus(e, 'burn');
  if (burn && burn.flag === 0) {
    const src = sim.c.towerByUid.get(burn.src);
    if (src && src.def === 'ember' && sim.stats(src).burnSpread > 0) {
      let n = 0;
      for (const o of s.enemies) {
        if (n >= 3) break;
        if (!o.alive || o === e) continue;
        if ((o.x - e.x) ** 2 + (o.y - e.y) ** 2 > 1.44) continue;
        addStatus(o, 'burn', Math.max(1, burn.t * 0.5 + 1), burn.power, burn.src, 1, 1);
        n++;
      }
    }
  }
  const def = ENEMIES[e.def];
  if (def.onDeathSpawn) {
    for (let i = 0; i < def.onDeathSpawn.count; i++) {
      const c = spawnEnemyAt(sim, def.onDeathSpawn.enemy, false, e);
      c.x += (i - 1.5) * 0.12;
      c.y += ((i % 2) - 0.5) * 0.12;
    }
  }
}
