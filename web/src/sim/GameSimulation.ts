import { footprintCells } from '../config/towers.ts';
import { socketRune, unsocketRune } from './RuneSystem.ts';
import { SIM } from '../config/balance.ts';
import { aggregateModifiers, type ModAggregate } from '../config/modifiers.ts';
import { step as rngStep } from '../core/rng.ts';
import type {
  EnemyInstance, GameEvent, GameState, HeroId, HeroMode, ModifierId, Stats, TargetPriority, TowerActiveId, TowerId, TowerInstance, GridPos,
} from '../core/types.ts';
import { chooseHeroPerk, deployHero, moveHeroAnchor, setHeroMode, updateHeroes, useHeroAbility, type HeroAura } from './HeroSystem.ts';
import { updateBosses } from './BossSystem.ts';
import { buildTower, canBuildHere, fuseTowers, canMoveTower, moveTower, sellAllTowers, sellTower, specializeTower, triggerActive, upgradeAllTowers, upgradeTower, type BuildCheck, respecTower, groupSpecialize, starTower, starAllTowers } from './EconomySystem.ts';
import { updateEnemies } from './EnemySystem.ts';
import { rebuildNetwork, updateNetworkDischarge } from './NetworkSystem.ts';
import { computeField, INF } from './Pathfinding.ts';
import { spawnsOf } from './WorldGenerator.ts';
import { updateProjectiles } from './ProjectileSystem.ts';
import { applyModifierChoice } from './RogueliteSystem.ts';
import { createInitialState, type RunSetup } from './setup.ts';
import { recomputeAllStats, updateTowers } from './TowerSystem.ts';
import { callNextWave, checkWaveEnd, startNextWave, updateSpawns } from './WaveDirector.ts';
import { updateZones } from './ZoneSystem.ts';
import { orderMove, rebuildChampionBuffs, resetParagon, resetSkillTree, setHoldPosition, spendParagonPoint, spendSkillPoint, updateChampion, useChampionSkill } from './ChampionSystem.ts';
import { updateHeroEndless } from './HeroEndlessSystem.ts';
import { orderMine, updateMining } from './MiningSystem.ts';
import { buyCollectionItem, buyOffer, craftRecipe, equipItem, sellItem, slotPotion, unequipSlot, upgradeItem, usePotion } from './InventorySystem.ts';
import type { ItemId, PotionId } from '../core/types.ts';

/** Abgeleitete, nicht serialisierte Daten. Werden bei Bedarf (Dirty-Flags) neu berechnet. */
export interface SimCache {
  field: Float64Array;
  safeField: Float64Array;
  blocked: Uint8Array;
  fieldDirty: boolean;
  networkDirty: boolean;
  statsDirty: boolean;
  towerStats: Map<number, Stats>;
  towerByCell: Map<number, TowerInstance>;
  towerByUid: Map<number, TowerInstance>;
  enemyByUid: Map<number, EnemyInstance>;
  mods: ModAggregate;
  linked: Map<number, number>;       // Turm-UID → Netzwerkgruppen-Index
  resonanceByTower: Map<number, string[]>;
  networkBonusByTower: Map<number, number>;
  heroAura: Map<number, HeroAura>;
  heroAuraSig: string;
  heroAuraTimer: number;
  /** Aggregierte Turmverstärkungen des Helden-Endlosmodus (Fähigkeiten + Aura). */
  champBuff: Map<number, { dmg: number; speed: number; range: number; crit: number }>;
  champBuffSig: string;
  champBuffTimer: number;
}

export type CommandResult = { ok: true; message?: string } | { ok: false; reason: string };

/**
 * Deterministische Spielsimulation mit festem Takt (SIM.tickRate).
 * Die Darstellung liest den Zustand nur; alle Änderungen laufen über Befehle oder step().
 */
export class GameSimulation {
  readonly s: GameState;
  readonly c: SimCache;
  events: GameEvent[] = [];
  /** In diesem Takt ausgelöste aktive Turmfähigkeiten (werden im nächsten Takt ausgeführt). */
  pendingActives: { uid: number; id: TowerActiveId }[] = [];
  private acc = 0;
  /** Fehler in einem Tick werden protokolliert statt die Simulation still anzuhalten. */
  lastError: string | null = null;

  constructor(state: GameState) {
    this.s = state;
    const n = state.map.width * state.map.height;
    this.c = {
      field: new Float64Array(n),
      safeField: new Float64Array(n),
      blocked: new Uint8Array(n),
      fieldDirty: true,
      networkDirty: true,
      statsDirty: true,
      towerStats: new Map(),
      towerByCell: new Map(),
      towerByUid: new Map(),
      enemyByUid: new Map(),
      mods: aggregateModifiers(state.modifiers),
      linked: new Map(),
      resonanceByTower: new Map(),
      networkBonusByTower: new Map(),
      heroAura: new Map(),
      heroAuraSig: '',
      heroAuraTimer: 0,
      champBuff: new Map(),
      champBuffSig: '',
      champBuffTimer: 0,
    };
    this.reindex();
    this.refreshCaches();
  }

  static create(setup: RunSetup): GameSimulation {
    return new GameSimulation(createInitialState(setup));
  }

  // ------------------------------------------------------------ Hilfsfunktionen

  uid(): number {
    return this.s.nextUid++;
  }

  rand(): number {
    const [v, st] = rngStep(this.s.rng.combat);
    this.s.rng.combat = st;
    return v;
  }

  emit(e: GameEvent): void {
    if (this.events.length < 2000) this.events.push(e);
  }

  drainEvents(): GameEvent[] {
    const e = this.events;
    this.events = [];
    return e;
  }

  reindex(): void {
    this.c.towerByCell.clear();
    this.c.towerByUid.clear();
    this.c.blocked.fill(0);
    for (const t of this.s.towers) {
      for (const i of footprintCells(t.def, t.cx, t.cy, this.s.map.width)) {
        this.c.towerByCell.set(i, t);
        if (this.s.map.variant === 'mazing') this.c.blocked[i] = 1;
      }
      this.c.towerByUid.set(t.uid, t);
    }
    this.c.enemyByUid.clear();
    for (const e of this.s.enemies) this.c.enemyByUid.set(e.uid, e);
  }

  /** Aktualisiert alle Caches, deren Dirty-Flag gesetzt ist. */
  refreshCaches(): void {
    if (this.c.networkDirty) {
      rebuildNetwork(this);
      this.c.networkDirty = false;
      this.c.statsDirty = true;
    }
    if (this.c.statsDirty) {
      this.c.mods = aggregateModifiers(this.s.modifiers);
      recomputeAllStats(this);
      this.c.statsDirty = false;
    }
    if (this.c.fieldDirty) {
      this.c.field = computeField(this.s.map, this.c.blocked);
      this.c.safeField = this.s.map.variant === 'mazing' ? computeSafeField(this) : this.c.field;
      this.c.fieldDirty = false;
    }
  }

  stats(t: TowerInstance): Stats {
    let st = this.c.towerStats.get(t.uid);
    if (!st) {
      this.c.statsDirty = true;
      this.refreshCaches();
      st = this.c.towerStats.get(t.uid);
    }
    if (!st) throw new Error(`Keine Werte für Turm ${t.uid}`);
    return st;
  }

  towerAt(x: number, y: number): TowerInstance | undefined {
    return this.c.towerByCell.get(y * this.s.map.width + x);
  }

  // ------------------------------------------------------------ Befehle (UI/Bot)

  canBuild(def: TowerId, cx: number, cy: number, spec: 0 | 1 | 2 = 0): BuildCheck {
    this.refreshCaches();
    return canBuildHere(this, def, cx, cy, spec);
  }

  build(def: TowerId, cx: number, cy: number, spec: 0 | 1 | 2 = 0): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    this.refreshCaches();
    const r = buildTower(this, def, cx, cy, spec);
    this.refreshCaches();
    return r;
  }

  sell(uid: number): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = sellTower(this, uid);
    this.refreshCaches();
    return r;
  }

  /** Legendären Turm versetzen (kostenlos, danach kurz außer Gefecht). */
  moveTower(uid: number, cx: number, cy: number): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = moveTower(this, uid, cx, cy);
    this.refreshCaches();
    return r;
  }

  canMoveTower(uid: number, cx: number, cy: number) {
    return canMoveTower(this, uid, cx, cy);
  }

  /** Alle (bzw. die angegebenen) Türme verkaufen. */
  sellAll(uids?: readonly number[]): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = sellAllTowers(this, uids);
    this.refreshCaches();
    return r;
  }

  upgrade(uid: number): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = upgradeTower(this, uid);
    this.refreshCaches();
    return r;
  }

  /** Alle (bzw. die angegebenen) Türme bis zur höchsten Stufe aufwerten, solange das Gold reicht. */
  upgradeAll(uids?: readonly number[]): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = upgradeAllTowers(this, uids);
    this.refreshCaches();
    return r;
  }

  specialize(uid: number, spec: 1 | 2): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = specializeTower(this, uid, spec);
    this.refreshCaches();
    return r;
  }

  /** 3.6: Veredelung, Bauplätze, Kernausbau. */
  star(uid: number): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = starTower(this, uid);
    this.refreshCaches();
    return r;
  }

  starAll(uids?: readonly number[]): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = starAllTowers(this, uids);
    this.refreshCaches();
    return r;
  }

  respec(uid: number, spec: 1 | 2): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = respecTower(this, uid, spec);
    this.refreshCaches();
    return r;
  }

  groupSpec(uids: readonly number[], spec: 1 | 2): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = groupSpecialize(this, uids, spec);
    this.refreshCaches();
    return r;
  }

  setPriority(uid: number, p: TargetPriority): CommandResult {
    const t = this.c.towerByUid.get(uid);
    if (!t) return { ok: false, reason: 'Turm nicht gefunden' };
    t.priority = p;
    t.target = 0;
    t.retarget = 0;
    return { ok: true };
  }

  fuse(uid: number, partnerUid: number): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = fuseTowers(this, uid, partnerUid);
    this.refreshCaches();
    return r;
  }

  towerActive(uid: number): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    return triggerActive(this, uid);
  }

  deployHero(id: HeroId, cx: number, cy: number): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    this.refreshCaches();
    return deployHero(this, id, cx, cy);
  }

  heroMode(id: HeroId, mode: HeroMode): CommandResult {
    return setHeroMode(this, id, mode);
  }

  heroAnchor(id: HeroId, cx: number, cy: number): CommandResult {
    return moveHeroAnchor(this, id, cx, cy);
  }

  heroAbility(id: HeroId, slot: 0 | 1 | 2): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = useHeroAbility(this, id, slot);
    this.refreshCaches();
    return r;
  }

  heroPerk(id: HeroId, choice: 0 | 1): CommandResult {
    const r = chooseHeroPerk(this, id, choice);
    this.refreshCaches();
    return r;
  }

  // ------------------------------------------------------------ Helden-Endlosmodus

  private heCmd(fn: () => CommandResult): CommandResult {
    if (!this.s.he) return { ok: false, reason: 'Nur im Helden-Endlosmodus' };
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    const r = fn();
    // Turmverstärkungen sofort wirksam machen (nicht erst im nächsten Takt)
    if (this.c.champBuffSig === '') rebuildChampionBuffs(this);
    this.refreshCaches();
    return r;
  }

  moveChampion(cx: number, cy: number): CommandResult {
    return this.heCmd(() => orderMove(this, cx, cy));
  }

  /** Hindernis abbauen (Helden-Endlos). walk = Held muss erst hinlaufen. */
  mine(cx: number, cy: number): CommandResult & { walk?: boolean } {
    if (!this.s.he) return { ok: false, reason: 'Nur im Helden-Endlosmodus' };
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    return orderMine(this, cx, cy);
  }

  holdPosition(hold: boolean): CommandResult {
    return this.heCmd(() => setHoldPosition(this, hold));
  }

  championSkill(slot: number, tx: number, ty: number): CommandResult {
    return this.heCmd(() => useChampionSkill(this, slot, tx, ty));
  }

  learnSkill(nodeId: string): CommandResult {
    return this.heCmd(() => spendSkillPoint(this, nodeId));
  }

  /** Paragonpunkt vergeben (2.7). */
  paragonPoint(nodeId: string): CommandResult {
    return this.heCmd(() => spendParagonPoint(this, nodeId));
  }

  paragonReset(): CommandResult {
    return this.heCmd(() => resetParagon(this));
  }

  /** 3.4: Skillbaum des Helden zurücksetzen. */
  treeReset(): CommandResult {
    return this.heCmd(() => resetSkillTree(this));
  }

  /** Rune in einen Turm sockeln bzw. zurück in den Beutel legen (2.7). */
  socketRune(towerUid: number, runeUid: number): CommandResult {
    return this.heCmd(() => socketRune(this, towerUid, runeUid));
  }

  unsocketRune(runeUid: number): CommandResult {
    return this.heCmd(() => unsocketRune(this, runeUid));
  }

  usePotion(potion: PotionId): CommandResult {
    return this.heCmd(() => usePotion(this, potion));
  }

  slotPotion(potion: PotionId): CommandResult {
    return this.heCmd(() => slotPotion(this, potion));
  }

  equip(uid: number, slot?: number): CommandResult {
    return this.heCmd(() => equipItem(this, uid, slot));
  }

  unequip(slot: number): CommandResult {
    return this.heCmd(() => unequipSlot(this, slot));
  }

  shopBuy(offerId: string): CommandResult {
    return this.heCmd(() => buyOffer(this, offerId));
  }

  shopBuyItem(item: ItemId): CommandResult {
    return this.heCmd(() => buyCollectionItem(this, item));
  }

  shopSell(uid: number): CommandResult {
    return this.heCmd(() => sellItem(this, uid));
  }

  craft(recipeId: string): CommandResult {
    return this.heCmd(() => craftRecipe(this, recipeId));
  }

  upgradeItem(uid: number): CommandResult {
    return this.heCmd(() => upgradeItem(this, uid));
  }

  /** Beendet einen Endloslauf freiwillig (Ergebnis wird wie eine Niederlage gewertet). */
  abandon(): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    this.s.phase = 'defeat';
    this.emit({ k: 'defeat' });
    return { ok: true };
  }

  /** Helden-Endlos: nächste Welle sofort (in der Bauphase) bzw. vorzeitig (im Kampf). */
  nextWave(): CommandResult {
    if (this.isOver()) return { ok: false, reason: 'Partie beendet' };
    this.refreshCaches();
    const r = callNextWave(this);
    if (this.s.he) this.s.he.autoTimer = -1;
    return r ? { ok: false, reason: r } : { ok: true };
  }

  startWave(): CommandResult {
    if (this.s.phase !== 'build') return { ok: false, reason: 'Eine Welle läuft bereits' };
    this.refreshCaches();
    startNextWave(this);
    return { ok: true };
  }

  chooseModifier(id: ModifierId | null): CommandResult {
    if (this.s.phase !== 'choice' || !this.s.modOffer) return { ok: false, reason: 'Keine Auswahl offen' };
    if (id !== null && !this.s.modOffer.includes(id)) return { ok: false, reason: 'Modifikator nicht im Angebot' };
    applyModifierChoice(this, id);
    this.refreshCaches();
    return { ok: true };
  }

  setSpeed(speed: number): void {
    this.s.speed = (SIM.speeds as readonly number[]).includes(speed) ? speed : 1;
  }

  setPaused(p: boolean): void {
    this.s.paused = p;
  }

  isOver(): boolean {
    return this.s.phase === 'victory' || this.s.phase === 'defeat';
  }

  /** Vorschau der Route im Mazing, falls an (cx, cy) ein Turm stünde. */
  previewRoute(cx: number, cy: number): GridPos[] {
    const chk = this.canBuild('ranger', cx, cy);
    return chk.route ?? [];
  }

  // ------------------------------------------------------------ Takt

  /**
   * Schreitet um reale Zeit voran. Spielgeschwindigkeit erhöht die Anzahl fester Ticks,
   * nicht die Schrittweite – dadurch bleiben Schaden und Statusdauern exakt.
   */
  advance(realDt: number): number {
    if (this.s.paused || this.isOver()) {
      this.acc = 0;
      return 0;
    }
    const clamped = Math.min(Math.max(realDt, 0), 0.25);
    this.acc += clamped * this.s.speed;
    let ticks = 0;
    const max = SIM.maxTicksPerFrame * this.s.speed;
    while (this.acc >= SIM.dt && ticks < max) {
      this.step();
      this.acc -= SIM.dt;
      ticks++;
    }
    if (ticks >= max) this.acc = 0;
    return ticks;
  }

  /** Ein fester Simulationsschritt. */
  step(): void {
    if (this.isOver()) return;
    try {
      this.refreshCaches();
      const dt = SIM.dt;
      this.s.tick++;
      this.s.time += dt;
      if (this.s.phase === 'combat') {
        this.s.waveTime += dt;
        this.s.stats.duration += dt;
        updateSpawns(this, dt);
      }
      updateEnemies(this, dt);
      updateBosses(this, dt);
      updateHeroes(this, dt);
      if (this.s.he) {
        updateChampion(this, dt);
        updateMining(this, dt);
      }
      this.refreshCaches();
      updateTowers(this, dt);
      updateProjectiles(this, dt);
      updateZones(this, dt);
      updateNetworkDischarge(this, dt);
      this.cleanup(dt);
      if (this.s.phase === 'combat') checkWaveEnd(this);
      if (this.s.he && !this.isOver()) updateHeroEndless(this, dt);
    } catch (err) {
      // Fehler werden sichtbar gemacht, die Simulation läuft im nächsten Tick weiter.
      this.lastError = err instanceof Error ? `${err.message}\n${err.stack ?? ''}` : String(err);
      this.emit({ k: 'message', text: 'Interner Simulationsfehler – siehe Konsole', severity: 'danger' });
      console.error('[Simulation]', err);
    }
  }

  private cleanup(dt: number): void {
    const s = this.s;
    if (s.enemies.some((e) => !e.alive)) {
      s.enemies = s.enemies.filter((e) => e.alive);
      this.c.enemyByUid.clear();
      for (const e of s.enemies) this.c.enemyByUid.set(e.uid, e);
    }
    if (s.projectiles.some((p) => !p.alive)) s.projectiles = s.projectiles.filter((p) => p.alive);
    if (s.zones.some((z) => z.t <= 0 && z.armed <= 0)) {
      if (s.zones.some((z) => z.kind === 'leyNode' && z.t <= 0 && z.armed <= 0)) this.c.statsDirty = true;
      s.zones = s.zones.filter((z) => z.t > 0 || z.armed > 0);
    }
    for (const t of s.telegraphs) t.t -= dt;
    if (s.telegraphs.some((t) => t.t <= 0)) s.telegraphs = s.telegraphs.filter((t) => t.t > 0);
  }

  /** Kopie des Zustands als JSON (für Speicherstände). */
  serialize(): string {
    return JSON.stringify(this.s);
  }
}

/** Gefahrenfeld: Pfadspäher meiden Zellen, die von Türmen abgedeckt werden. */
function computeSafeField(sim: GameSimulation): Float64Array {
  const map = sim.s.map;
  const n = map.width * map.height;
  const danger = new Float32Array(n);
  for (const t of sim.s.towers) {
    const st = sim.c.towerStats.get(t.uid);
    const r = st ? st.range : 3;
    if (!st || st.damage <= 0) continue;
    const r2 = r * r;
    const x0 = Math.max(0, Math.floor(t.cx - r));
    const x1 = Math.min(map.width - 1, Math.ceil(t.cx + r));
    const y0 = Math.max(0, Math.floor(t.cy - r));
    const y1 = Math.min(map.height - 1, Math.ceil(t.cy + r));
    for (let y = y0; y <= y1; y++) {
      for (let x = x0; x <= x1; x++) {
        const dx = x - t.cx;
        const dy = y - t.cy;
        if (dx * dx + dy * dy <= r2) danger[y * map.width + x] += 0.6;
      }
    }
  }
  const f = computeField(map, sim.c.blocked, danger);
  // Sicherheitsnetz: nur nutzen, wenn ein Weg existiert.
  return spawnsOf(map).every((sp) => f[sp.y * map.width + sp.x] < INF) ? f : sim.c.field;
}
