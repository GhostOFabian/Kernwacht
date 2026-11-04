import { HEROES } from '../config/heroes.ts';
import { TOWERS } from '../config/towers.ts';
import type { TowerId } from '../core/types.ts';
import { activeState, fusionOptions, nextUpgradeCost, specCost, towerCost } from './EconomySystem.ts';
import { abilityReady, canPlaceHero } from './HeroSystem.ts';
import type { GameSimulation } from './GameSimulation.ts';
import { isBuildableCell, tracePath } from './Pathfinding.ts';
import { spawnsOf } from './WorldGenerator.ts';

// Automatischer Testspieler für Integrations- und Balancetests. Er spielt ausschließlich über
// die öffentlichen Befehle der Simulation – wie ein menschlicher Spieler über die UI.

export interface BotProfile {
  name: string;
  rotation: TowerId[];
  econ: number;          // Anzahl Goldgräber, die früh gebaut werden
  upgradeBias: number;   // 0..1: Neigung, eher aufzuwerten als neu zu bauen
}

export const BOT_PROFILES: BotProfile[] = [
  { name: 'Ausgewogen', rotation: ['ranger', 'ember', 'frost', 'storm', 'ballista', 'rune', 'cannon', 'spore', 'resonator', 'pylon'], econ: 1, upgradeBias: 0.5 },
  { name: 'Elementar', rotation: ['ember', 'frost', 'storm', 'pylon', 'storm', 'ember', 'rune', 'spore'], econ: 0, upgradeBias: 0.45 },
  { name: 'Physisch', rotation: ['ranger', 'cannon', 'ballista', 'ranger', 'cannon', 'pylon', 'ballista'], econ: 0, upgradeBias: 0.55 },
  { name: 'Wirtschaft', rotation: ['ranger', 'storm', 'ember', 'ballista', 'frost', 'rune'], econ: 3, upgradeBias: 0.5 },
  { name: 'Kontrolle', rotation: ['ranger', 'frost', 'riftbinder', 'resonator', 'storm', 'rune', 'spore', 'cannon'], econ: 1, upgradeBias: 0.5 },
  { name: 'Neue Türme', rotation: ['ranger', 'tesla', 'tesla', 'meteor', 'gravity', 'laser', 'dragon', 'totem', 'resonator'], econ: 0, upgradeBias: 0.5 },
];

export class Bot {
  private rot = 0;
  constructor(readonly sim: GameSimulation, readonly profile: BotProfile) {}

  /** Coverage-Score einer Zelle: Anzahl Routenzellen in Reichweite. */
  private coverage(cx: number, cy: number, r: number, route: { x: number; y: number }[]): number {
    let n = 0;
    const r2 = r * r;
    for (const p of route) if ((p.x - cx) ** 2 + (p.y - cy) ** 2 <= r2) n++;
    return n;
  }

  private bestCell(def: TowerId): { x: number; y: number } | null {
    const sim = this.sim;
    const map = sim.s.map;
    // Alle Risse berücksichtigen (Weggabelungen auf großen Karten)
    const route = spawnsOf(map).flatMap((sp) => tracePath(map, sim.c.field, sp));
    const r = (TOWERS[def].base.range ?? 3) || 2.5;
    let best: { x: number; y: number } | null = null;
    let bs = -1;
    for (let y = 0; y < map.height; y++) {
      for (let x = 0; x < map.width; x++) {
        const i = y * map.width + x;
        if (!isBuildableCell(map, i) || sim.c.towerByCell.has(i)) continue;
        // Im Mazing bleiben die Spalten des Labyrinthplans (inkl. Durchgänge) frei.
        if (map.variant === 'mazing' && x >= 3 && x < map.width - 3 && x % 3 === 0) continue;
        let sc: number;
        if (def === 'goldmine') sc = ([i, i + 1, i + map.width, i + map.width + 1].some((k) => map.veins.includes(k)) ? 100 : 0) - this.coverage(x + 0.5, y + 0.5, 2, route);
        else if (def === 'pylon') {
          sc = 0;
          for (const t of sim.s.towers) if ((t.cx - x) ** 2 + (t.cy - y) ** 2 <= 9) sc += 3;
          sc += this.coverage(x, y, 2, route) * 0.1;
        } else sc = this.coverage(x, y, r, route) + (map.heights[i] >= 2 ? 2 : 0) + (map.ley.includes(i) ? 2 : 0);
        // Leichte Spannweite für deterministische Vielfalt
        sc += ((x * 7 + y * 13) % 5) * 0.01;
        if (sc <= bs) continue;
        if (!sim.canBuild(def, x, y).ok) continue;
        bs = sc;
        best = { x, y };
      }
    }
    return best;
  }

  private wallPlan: { x: number; y: number }[] | null = null;

  /** Mazing: Serpentinen-Plan aus Barrikadenreihen mit abwechselnden Durchgängen. */
  private nextWall(): { x: number; y: number } | null {
    const sim = this.sim;
    const map = sim.s.map;
    if (!this.wallPlan) {
      this.wallPlan = [];
      let top = true;
      for (let x = 3; x < map.width - 3; x += 3) {
        const ys: number[] = [];
        for (let y = 0; y < map.height; y++) ys.push(y);
        const gap = top ? [0, 1] : [map.height - 1, map.height - 2];
        // Zuerst die Zellen nahe der aktuellen Route, damit frühe Wände sofort wirken
        ys.sort((a, b) => Math.abs(a - map.spawn.y) - Math.abs(b - map.spawn.y));
        for (const y of ys) if (!gap.includes(y)) this.wallPlan.push({ x, y });
        top = !top;
      }
    }
    while (this.wallPlan.length > 0) {
      const c = this.wallPlan[0];
      const chk = sim.canBuild('barricade', c.x, c.y);
      if (chk.ok) return c;
      if (!chk.affordable) return null;
      this.wallPlan.shift();
    }
    return null;
  }

  /** Nutzt das verfügbare Gold für Bau und Upgrades. */
  spend(): void {
    const sim = this.sim;
    if (sim.s.map.variant === 'mazing') {
      // Erst Grundfeuerkraft sichern, dann eine Serpentine aus Barrikaden, danach Türme an die Gänge.
      this.spendTowers(2 + Math.floor(sim.s.wave / 2));
      const wallsWanted = 30 + sim.s.wave * 8;
      const floor = sim.s.gold * 0.35;
      for (let k = 0; k < 60; k++) {
        const walls = sim.s.towers.filter((t) => t.def === 'barricade').length;
        if (walls >= wallsWanted || sim.s.gold < 4 || sim.s.gold < floor) break;
        const c = this.nextWall();
        if (!c || !sim.build('barricade', c.x, c.y).ok) break;
        this.wallPlan?.shift();
      }
    }
    this.spendTowers(Infinity);
  }

  /** Bau- und Ausbauschleife bis maxFighters Kampftürme stehen oder das Gold aufgebraucht ist. */
  private spendTowers(maxFighters: number): void {
    const sim = this.sim;
    for (let guard = 0; guard < 40; guard++) {
      const s = sim.s;
      const fighters = s.towers.filter((t) => t.def !== 'goldmine' && t.def !== 'barricade');
      if (fighters.length >= maxFighters) return;
      // Ab Welle 7 Detektoren gegen getarnte Gegner einplanen
      const detectors = s.towers.filter((t) => TOWERS[t.def].detector).length;
      if (s.wave >= 7 && detectors < 1 + Math.floor((s.wave - 7) / 6) && s.gold >= towerCost(sim, 'rune')) {
        const c = this.bestCell('rune');
        if (c && sim.build('rune', c.x, c.y).ok) continue;
      }
      const mines = s.towers.filter((t) => t.def === 'goldmine').length;
      if (mines < this.profile.econ && s.wave >= 1 && s.wave <= 6 && s.gold >= towerCost(sim, 'goldmine')) {
        const c = this.bestCell('goldmine');
        if (c && sim.build('goldmine', c.x, c.y).ok) continue;
      }
      const minTowers = 4 + s.wave * 0.7;
      const wantUpgrade = fighters.length >= minTowers && ((s.wave * 7 + guard) % 10) / 10 < this.profile.upgradeBias + 0.3;
      if (wantUpgrade && fighters.length > 0) {
        const cand = [...fighters].sort((a, b) => a.level - b.level || b.damageDealt - a.damageDealt)[0];
        const cost = nextUpgradeCost(cand);
        if (cost !== null && s.gold >= cost) {
          if (sim.upgrade(cand.uid).ok) continue;
        } else if (cost === null && cand.spec === 0 && TOWERS[cand.def].specs.length === 2) {
          const sp: 1 | 2 = cand.uid % 2 === 0 ? 1 : 2;
          if (s.gold >= specCost(cand, sp) && sim.specialize(cand.uid, sp).ok) continue;
        }
      }
      let def = this.profile.rotation[this.rot % this.profile.rotation.length];
      if (s.gold < towerCost(sim, def)) {
        if (fighters.length < minTowers && s.gold >= towerCost(sim, 'ranger')) def = 'ranger';
        else return;
      }
      const c = this.bestCell(def);
      if (!c) {
        this.rot++;
        continue;
      }
      if (sim.build(def, c.x, c.y).ok) this.rot++;
      else return;
    }
  }

  /** Helden einsetzen, Fähigkeiten nutzen, Perks wählen; Fusionen und Turmfähigkeiten. */
  tactics(): void {
    const sim = this.sim;
    const s = sim.s;
    const route = tracePath(s.map, sim.c.field, s.map.spawn);
    for (const h of s.heroes) {
      if (h.pendingPerk) sim.heroPerk(h.id, (h.level % 2) as 0 | 1);
      if (h.state === 'ready' && s.phase === 'combat' && s.riftEnergy >= HEROES[h.id].cost && route.length > 4) {
        const frac = HEROES[h.id].support ? 0.45 : 0.7;
        for (let k = 0; k < 6; k++) {
          const c = route[Math.min(route.length - 2, Math.floor(route.length * frac) + k)];
          if (canPlaceHero(sim, c.x, c.y).ok) {
            sim.deployHero(h.id, c.x, c.y);
            break;
          }
        }
      }
      if (h.state === 'active') {
        const near = s.enemies.some((e) => e.alive && (e.x - h.x) ** 2 + (e.y - h.y) ** 2 < 9);
        for (const slot of [0, 1, 2] as const) {
          if (abilityReady(sim, h, slot).ok && (near || HEROES[h.id].support)) sim.heroAbility(h.id, slot);
        }
      }
    }
    if (s.phase === 'combat') {
      const busy = s.enemies.filter((e) => e.alive).length >= 6;
      for (const t of s.towers) if (busy && activeState(sim, t).available) sim.towerActive(t.uid);
    } else {
      for (const t of s.towers) {
        for (const o of fusionOptions(sim, t)) {
          if (o.affordable && sim.fuse(t.uid, o.partner.uid).ok) return;
        }
      }
    }
  }

  /** Eine Entscheidungsrunde ohne Simulationsschritt (für die lebendige Menüwelt). */
  act(): void {
    const sim = this.sim;
    if (sim.isOver()) return;
    if (sim.s.phase === 'choice') {
      const offer = sim.s.modOffer ?? [];
      sim.chooseModifier(offer.length ? offer[0] : null);
    }
    if (sim.s.phase === 'build') {
      this.tactics();
      this.spend();
      sim.startWave();
    } else if (sim.s.phase === 'combat') {
      this.spend();
      this.tactics();
    }
  }

  /** Spielt die Partie bis zum Ende (oder bis maxWaves). */
  play(maxTicks = 30 * 60 * 90, maxWaves = Infinity): void {
    const sim = this.sim;
    let ticks = 0;
    while (!sim.isOver() && ticks < maxTicks) {
      if (sim.s.phase === 'choice') {
        const offer = sim.s.modOffer ?? [];
        sim.chooseModifier(offer.length ? offer[0] : null);
      }
      if (sim.s.phase === 'build') {
        if (sim.s.wave >= maxWaves) return;
        this.tactics();
        this.spend();
        sim.startWave();
      }
      sim.step();
      ticks++;
      if (sim.s.phase === 'combat' && sim.s.tick % 90 === 0) this.spend();
      if (sim.s.phase === 'combat' && sim.s.tick % 15 === 0) this.tactics();
    }
  }
}
