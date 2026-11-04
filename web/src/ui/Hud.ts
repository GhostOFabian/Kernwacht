import { STARS, starText } from '../config/goldsinks.ts';
import { decorKindAt, mineDuration, mineState } from '../sim/MiningSystem.ts';
import { AFFIXES, countsTowardLimit, focusBonus, LANE_BARRICADE, nextTowerSlotWave, SCALING, TOWER_LIMIT, type AffixId } from '../config/scaling.ts';
import { MASTERY, masteryNext, masteryRank } from '../config/mastery.ts';
import { nextRuneSlotWave, RUNE_ORDER, RUNE_RULES, RUNES, runeEffectText, runeEligible, runeLabel, runeSockets } from '../config/runes.ts';
import { bagRunes, towerRunes } from '../sim/RuneSystem.ts';
import { boundKey, keyName } from '../config/keys.ts';
import { HERO_ENDLESS } from '../config/heroEndless.ts';
import { towerUnlocked, towerUnlockWave } from '../config/passiveTree.ts';
import { ABILITIES, BOSSES, ENEMIES } from '../config/enemies.ts';
import { RESONANCES } from '../config/elements.ts';
import { BIOMES, TERRAIN_EVENTS } from '../config/biomes.ts';
import { DUOS, HERO_RULES, HEROES } from '../config/heroes.ts';
import { MODIFIERS } from '../config/modifiers.ts';
import { ACTIVE_MIN_LEVEL, BUILD_ORDER, LEGENDARY_ORDER, footprintCells, FUSION_RANGE, FUSIONS, TOWER_ACTIVES, TOWERS, towerSize } from '../config/towers.ts';
import { Cell, type BossId, type EnemyId, type HeroId, type HeroInstance, type HeroMode, type ModifierId, type TargetPriority, type TowerId, type TowerInstance } from '../core/types.ts';
import type { IconFactory } from '../render/IconFactory.ts';
import { iconImg, turntableEl } from '../render/IconFactory.ts';
import { DAMAGE_NAMES } from '../render/palette.ts';
import { activeState, fusionOptions, fusionPairs, fusionPreview, veinBonus, nextUpgradeCost, refundValue, sellAllValue, specCost, towerCost, upgradableTowers, upgradeAllCost, countedTowers, currentTowerLimit, starCost, fullyBuilt, laneCharges, respecCost, specTargetCost, RESPEC_MUL, prespecAvailable, prespecCost, PRESPEC, type BuildCheck } from '../sim/EconomySystem.ts';
import type { GameSimulation } from '../sim/GameSimulation.ts';
import { abilityReady, activeDuos, heroStatsOf } from '../sim/HeroSystem.ts';
import { SKIP_GOLD } from '../sim/RogueliteSystem.ts';
import { earlyCallState, EARLY_CALL_GOLD, previewNextWave } from '../sim/WaveDirector.ts';
import { resistOf } from '../sim/DamageSystem.ts';
import { slowFactor } from '../sim/ElementSystem.ts';
import { enemyContactDps } from '../sim/EnemySystem.ts';
import { MUTATIONS } from '../config/enemies.ts';
import { STATUS_DEFS } from '../config/elements.ts';
import type { DamageType, EnemyInstance } from '../core/types.ts';
import { spawnsOf } from '../sim/WorldGenerator.ts';
import { clear, fmt, fmtNum, h, pct, put, setText, toggleClass } from './dom.ts';
import { svgIcon } from './icons.ts';
import { baseStatsOf, damageMatchups, towersOfType, MODE_NAMES, PRIORITY_NAMES, PRIORITY_ORDER, statLines, waveSummary } from './text.ts';

// HUD der Hauptspielansicht. Liest den Simulationszustand nur; alle Aktionen laufen über HudActions.

export interface HudActions {
  selectBuild(def: TowerId | null, spec?: 0 | 1 | 2): void;
  confirmBuild(): void;
  cancel(): void;
  selectTower(uid: number): void;
  upgrade(uid: number): void;
  /** Alle bzw. die angegebenen Türme bis zur höchsten Stufe aufwerten (ohne Spezialisierung). */
  upgradeAll(uids?: number[]): void;
  /** Gruppenauswahl (Doppelklick) aufheben. */
  clearGroup(): void;
  resetView(): void;
  /** Legendären Turm versetzen (Zielfeld anklicken). */
  startMove(uid: number): void;
  /** Alle bzw. die angegebenen Türme verkaufen. */
  sellAll(uids?: number[]): void;
  socketRune(towerUid: number, runeUid: number): void;
  unsocketRune(runeUid: number): void;
  specialize(uid: number, spec: 1 | 2): void;
  respec(uid: number, spec: 1 | 2): void;
  star(uid: number): void;
  starAll(uids?: number[]): void;
  groupSpec(uids: number[], spec: 1 | 2): void;
  sell(uid: number): void;
  setPriority(uid: number, p: TargetPriority): void;
  startWave(): void;
  togglePause(): void;
  cycleSpeed(): void;
  chooseModifier(id: ModifierId | null): void;
  rotate(dir: number): void;
  zoom(f: number): void;
  fit(): void;
  restart(): void;
  quit(): void;
  help(): void;
  settings(): void;
  toggleRanges(): void;
  tutorialDone(): void;
  heroCard(id: HeroId): void;
  heroMode(id: HeroId, mode: HeroMode): void;
  heroAbility(id: HeroId, slot: 0 | 1 | 2): void;
  heroPerk(id: HeroId, choice: 0 | 1): void;
  towerActive(uid: number): void;
  fuse(uid: number, partner: number): void;
  lookAt(x: number, z: number): void;
  toggleRotationLock(): void;
  miniPause(): void;
  mine(cell: number): void;
  selectEnemy(uid: number): void;
}

export interface HudView {
  buildDef: TowerId | null;
  /** 3.1: gewählte Spezialisierung beim Bau (0 = normal). */
  buildSpec?: 0 | 1 | 2;
  selectedTower: number;
  pendingCell: { x: number; y: number } | null;
  pendingCheck: BuildCheck | null;
  touchMode: boolean;
  infoEnemy: EnemyId | null;
  showRanges: boolean;
  placingHero: HeroId | null;
  selectedHero: number;
  /** Angeklickter Gegner (UID) für das Live-Infopanel. */
  selectedEnemy: number;
  rotationLocked: boolean;
  /** Mini-Pause: Simulation angehalten, aber ohne Pausenmenü – Bauen und Aufwerten bleiben möglich. */
  miniPaused: boolean;
  /** Angeklicktes Hindernis (Zellindex) im Helden-Endlos, -1 = keins. */
  selectedObstacle: number;
  /** Doppelklick auf einen Turm: alle Türme dieser Sorte sind gemeinsam ausgewählt. */
  groupDef: TowerId | null;
}

type Bar = { root: HTMLElement; fill: HTMLElement; extra?: HTMLElement; label: HTMLElement };

function bar(cls: string): Bar {
  const fill = h('div', { class: 'bar-fill' });
  const extra = h('div', { class: 'bar-extra' });
  const label = h('span', { class: 'bar-label' });
  return { root: h('div', { class: `bar ${cls}` }, fill, extra, label), fill, extra, label };
}

interface HeroCardEls {
  root: HTMLElement;
  sig: string;
  hp?: HTMLElement;
  time?: HTMLElement;
  cd?: HTMLElement;
  cdText?: HTMLElement;
  abil: HTMLButtonElement[];
  abilFill: HTMLElement[];
  ult?: HTMLElement;
  cost?: HTMLButtonElement;
}

const MODE_LABEL: Record<HeroMode, string> = { hold: 'Halten', advance: 'Vorrücken', retreat: 'Rückzug' };

export class Hud {
  readonly root: HTMLElement;
  readonly stage: HTMLElement;
  readonly floaters: HTMLElement;
  /** Container für zusätzliche Kopfzeilenwerte (z. B. Ressourcen im Helden-Endlosmodus). */
  readonly statsBox: HTMLElement;
  readonly stageLeft: HTMLElement;
  private gold = h('span', { class: 'v', 'data-testid': 'gold' });
  private goldStat: HTMLElement;
  private goldShown = 0;
  private goldLast = 0;
  private lives = h('span', { class: 'v', 'data-testid': 'lives' });
  private limitV = h('span', { class: 'v', 'data-testid': 'tower-limit' });
  private limitB = h('span', { class: 'b', 'data-testid': 'focus-bonus' });
  private limitStat = h('button', { class: 'tlimit', title: '', 'data-testid': 'tower-limit-btn', onclick: () => this.toggleLimitPop() }, h('span', { class: 'ic' }, '🏰'), this.limitV, this.limitB);
  private limitPop: HTMLElement | null = null;

  private toggleLimitPop(): void {
    if (this.limitPop) { this.limitPop.remove(); this.limitPop = null; return; }
    this.limitPop = h('div', { class: 'specpop limitpop', 'data-testid': 'limit-pop' });
    const r = this.limitStat.getBoundingClientRect();
    this.limitPop.style.left = `${Math.max(8, r.left)}px`;
    this.limitPop.style.bottom = `${Math.max(8, innerHeight - r.top + 8)}px`;
    this.fillLimitPop();
    this.root.appendChild(this.limitPop);
  }

  private fillLimitPop(): void {
    const pop = this.limitPop;
    if (!pop) return;
    const s = this.sim.s;
    const lim = currentTowerLimit(this.sim);
    const n = countedTowers(this.sim);
    const free = Math.max(0, lim - n);
    const fb = focusBonus(n, lim);
    const per = Math.round(TOWER_LIMIT.focusPer * 100);
    const max = Math.round(TOWER_LIMIT.focusMax * 100);
    const sig = `${n}/${lim}/${s.wave}/${s.bossSlots ?? 0}`;
    if (pop.dataset.sig === sig) return;
    pop.dataset.sig = sig;
    pop.replaceChildren(
      h('div', { class: 'sh' }, `🏰 Bauplätze: ${n} von ${lim} belegt`),
      h('p', { class: 'small' }, 'Im Endlosmodus kannst du nur begrenzt viele Türme bauen. Nicht mit gezählt werden Barrikaden, Goldgräber und Unterstützungstürme, die andere Türme verstärken (Leitpylon, Totemschrein) – die kannst du frei bauen. Eine Fusion macht aus zwei Türmen einen.'),
      h('div', { class: 'sh' }, `✦ Fokusbonus: +${Math.round(fb * 100)} % Schaden`),
      h('p', { class: 'small' }, `Weniger ist mehr: Jeder Bauplatz, den du frei lässt, macht ALLE deine Türme um ${per} % stärker (höchstens +${max} %).`),
      h('p', { class: 'small' }, `Jetzt: ${free} freie Plätze × ${per} % = +${Math.min(max, free * per)} % Schaden${free * per > max ? ` (gedeckelt auf ${max} %)` : ''}. Baust du einen Turm mehr, sinkt der Bonus um ${per} %.`),
      h('div', { class: 'sh' }, 'Neue Bauplätze'),
      h('p', { class: 'small' }, `+1 alle ${TOWER_LIMIT.perWaves} Wellen (nächster ab Welle ${nextTowerSlotWave(s.wave)}) und +1 nach jeder geschafften Bosswelle (bisher +${s.bossSlots ?? 0}).`),
      h('button', { class: 'btn sm', onclick: () => this.toggleLimitPop() }, 'Verstanden'));
  }
  private wave = h('span', { class: 'v', 'data-testid': 'wave' });
  private energy = h('span', { class: 'v', 'data-testid': 'energy' });
  private energyStat: HTMLElement;
  private startBtn: HTMLButtonElement;
  private startLabel = h('span', {});
  private speedBtn: HTMLButtonElement;
  private speedLabel = h('span', { class: 'speed-label' });
  private pauseBtn: HTMLButtonElement;
  private strip = h('div', { class: 'wave-strip', 'data-testid': 'wave-strip' });
  private mods = h('div', { class: 'mods' });
  private heroBar = h('div', { class: 'herobar', 'data-testid': 'herobar' });
  private heroCards = new Map<HeroId, HeroCardEls>();
  private bossBox = h('div', { class: 'boss hidden', 'data-testid': 'boss' });
  private bossName = h('div', { class: 'boss-name' });
  private bossPhase = h('div', { class: 'boss-phase' });
  private bossHp = bar('hp');
  private bossPoise = bar('poise');
  private warnings = h('div', { class: 'warnings', 'aria-live': 'assertive' });
  private banners = h('div', { class: 'banners', 'aria-live': 'polite' });
  private towerBar = h('div', { class: 'towerbar', role: 'toolbar', 'aria-label': 'Türme' });
  private towerBtns = new Map<TowerId, HTMLButtonElement>();
  /** 3.1: Auswahl normal / spezialisiert beim Turmkauf (ab Welle 250). */
  private specPop: HTMLElement | null = null;

  private closeSpecPop(): void {
    this.specPop?.remove();
    this.specPop = null;
  }

  private openSpecPop(id: TowerId): void {
    this.closeSpecPop();
    const btn = this.towerBtns.get(id);
    if (!btn) return;
    const d = TOWERS[id];
    const gold = this.sim.s.gold;
    const opt = (spec: 0 | 1 | 2, title: string, desc: string, cost: number) => h('button', {
      class: `specopt${gold < cost ? ' poor' : ''}`, 'data-testid': `prespec-${spec}`,
      onclick: (e: Event) => { e.stopPropagation(); this.closeSpecPop(); this.a.selectBuild(id, spec); },
    }, h('b', {}, title), h('span', { class: 'sd' }, desc), h('span', { class: 'sc' }, `${fmt(cost)} Gold`));
    const pop = h('div', { class: 'specpop', 'data-testid': 'prespec-pop', 'data-tower': id },
      h('div', { class: 'sh' }, `${d.icon} ${d.name} kaufen`),
      opt(0, 'Normal (Stufe 1)', d.role, towerCost(this.sim, id)),
      ...d.specs.slice(0, 2).map((sp, i) => opt((i + 1) as 1 | 2, `★ ${sp.name} (Höchststufe)`, sp.description, prespecCost(this.sim, id, (i + 1) as 1 | 2))),
      h('div', { class: 'sf' }, `Spezialisierte Türme werden direkt auf Höchststufe gebaut (+${Math.round((PRESPEC.premium - 1) * 100)} % Aufpreis).`),
    );
    const r = btn.getBoundingClientRect();
    pop.style.left = `${Math.max(8, Math.min(innerWidth - 300, r.left + r.width / 2 - 140))}px`;
    pop.style.bottom = `${Math.max(8, innerHeight - r.top + 8)}px`;
    this.root.appendChild(pop);
    this.specPop = pop;
    const off = (e: PointerEvent) => {
      const t = e.target as Node;
      if (!this.specPop) { document.removeEventListener('pointerdown', off, true); return; }
      if (this.specPop.contains(t) || btn.contains(t)) return;
      document.removeEventListener('pointerdown', off, true);
      this.closeSpecPop();
    };
    document.addEventListener('pointerdown', off, true);
  }
  private panel = h('aside', { class: 'panel', 'aria-label': 'Details' });
  private panelBody = h('div', { class: 'panel-body' });
  private modal = h('div', { class: 'modal-layer' });
  private mini: HTMLCanvasElement = h('canvas', { class: 'minimap-hud', 'aria-label': 'Minikarte – antippen, um die Kamera zu verschieben', 'data-testid': 'minimap' });
  private miniBase: HTMLCanvasElement | null = null;
  private miniTimer = 0;
  private portraitHint = h('div', { class: 'portrait-hint' }, '↻ Querformat empfohlen – das Spiel ist im Hochformat aber voll bedienbar.');
  private panelSig = '';
  private panelVol = '';
  private panelBuiltAt = 0;
  /** Maustaste/Finger gerade im Panel oder in der Turmübersicht gedrückt. */
  private pointerHeld = false;
  /** Fenster „Turmübersicht“ (Taste O). */
  private towerWin = h('div', { class: 'he-win tower-list hidden', role: 'dialog', 'aria-label': 'Turmübersicht', 'data-testid': 'tower-list' });
  private towerWinSig = '';
  towerListOpen = false;
  private towerSort: 'type' | 'damage' | 'level' = 'type';
  private lockBtn!: HTMLButtonElement;
  private miniBtn!: HTMLButtonElement;
  private upAllBtn!: HTMLButtonElement;
  private upAllSig = '';
  /** Turmübersicht: voll ausgebaute Türme ausblenden. */
  private hideMaxed = true;
  private miniBadge = h('div', { class: 'mini-pause hidden', 'data-testid': 'mini-pause-badge' }, '⏸ Mini-Pause – Bauen, Aufwerten und Fähigkeiten weiter möglich · Leertaste setzt fort');
  private stripSig = '';
  private modsSig = '';
  private modalKind: '' | 'choice' | 'pause' | 'tutorial' | 'confirm' = '';
  private modalDirty = true;
  private sellArmed = 0;
  /** Zweistufige Bestätigung für „Alle verkaufen“: Schlüssel der scharf geschalteten Schaltfläche. */
  private sellAllArmed = '';
  private sellAllArmedAt = 0;
  private tutorialStep = 0;
  private lastFrame = performance.now();
  showMinimap: boolean;
  /** Kamera folgt dem aktiven Boss. */
  followBoss = false;
  /** Kameraausschnitt in Weltkoordinaten (für die Minikarte), von der Sitzung gesetzt. */
  camQuad: [number, number][] = [];

  constructor(private readonly sim: GameSimulation, private readonly a: HudActions, private readonly v: HudView, mazing: boolean, private readonly icons: IconFactory) {
    const s = sim.s;
    this.showMinimap = s.map.width * s.map.height > 320;
    this.startBtn = h('button', { class: 'btn primary start', 'data-testid': 'start-wave', onclick: () => a.startWave(), title: 'Welle starten (Leertaste)' }, svgIcon('play'), this.startLabel);
    this.speedBtn = h('button', { class: 'btn icon', 'data-testid': 'speed', onclick: () => a.cycleSpeed(), title: 'Spielgeschwindigkeit 1× / 2× / 3× / 5× / 8× (F)', 'aria-label': 'Spielgeschwindigkeit' }, svgIcon('speed'), this.speedLabel);
    this.pauseBtn = h('button', { class: 'btn icon', 'data-testid': 'pause', onclick: () => a.togglePause(), title: 'Pause (P)', 'aria-label': 'Pause' }, svgIcon('pause'));
    this.goldStat = h('div', { class: 'stat gold', title: 'Gold' }, h('span', { class: 'ic' }, svgIcon('coin')), this.gold);
    this.energyStat = h('div', { class: 'stat energy', title: `Rissenergie für Helden (max. ${HERO_RULES.maxEnergy})` }, h('span', { class: 'ic' }, svgIcon('energy')), this.energy);
    this.statsBox = h('div', { class: 'stats' },
        h('button', { class: 'btn icon', onclick: () => a.togglePause(), title: 'Menü', 'aria-label': 'Menü' }, svgIcon('menu')),
        this.goldStat,
        h('div', { class: 'stat lives', title: 'Kernenergie' }, h('span', { class: 'ic' }, svgIcon('heart')), this.lives),
        h('div', { class: 'stat wave', title: 'Welle' }, h('span', { class: 'ic' }, svgIcon('wave')), this.wave),
        s.heroes.length ? this.energyStat : null,
      );
    const top = h('header', { class: 'topbar' },
      this.statsBox,
      h('div', { class: 'controls' },
        h('button', { class: 'btn sm', 'data-testid': 'open-tower-list', title: 'Turmübersicht: alle Türme mit Aufwerten und Infos (O)', onclick: () => this.toggleTowerList() }, svgIcon('up'), h('span', { class: 'lbl' }, 'Türme')),
        (this.upAllBtn = h('button', { class: 'btn sm', 'data-testid': 'upgrade-all', title: 'Alle Türme aufwerten', onclick: () => a.upgradeAll() }, svgIcon('up'), h('span', { class: 'lbl' }, 'Alle ▲'))),
        this.speedBtn,
        (this.miniBtn = h('button', { class: 'btn sm', 'data-testid': 'mini-pause', title: 'Mini-Pause ohne Menü: Spiel anhalten und weiter bauen/aufwerten (Leertaste im Kampf)', onclick: () => a.miniPause() }, svgIcon('pause'), h('span', { class: 'lbl' }, 'Mini'))),
        this.pauseBtn, this.startBtn),
    );
    this.bossBox.append(this.bossName, this.bossPhase, this.bossHp.root, this.bossPoise.root);
    if (s.rules.endless) this.towerBar.appendChild(this.limitStat);
    for (const id of BUILD_ORDER) {
      const d = TOWERS[id];
      if (d.mazingOnly && !mazing) continue;
      if (d.laneOnly && (mazing || !s.rules.endless)) continue;
      const banned = s.rules.bannedTowers.includes(id);
      const btn = h('button', {
        class: 'tbtn', 'data-tower': id, 'data-testid': `tower-${id}`, disabled: banned,
        title: `${d.name} (${keyName(boundKey(`tower:${d.id === 'lanebarricade' ? 'barricade' : d.id}`))}) – ${d.role}${d.damageType ? ` · ${DAMAGE_NAMES[d.damageType]}` : ''}`, 'aria-label': `${d.name}, ${d.cost} Gold`,
        onclick: () => {
          if (this.specPop?.dataset.tower === id) { this.closeSpecPop(); return; }
          if (prespecAvailable(this.sim, id) && !(this.sim.s.he && !towerUnlocked(id, this.sim.s.he, this.sim.s.wave))) { this.openSpecPop(id); return; }
          a.selectBuild(v.buildDef === id ? null : id);
        },
        onpointerenter: (e: PointerEvent) => {
          if (e.pointerType === 'mouse' && !v.buildDef && !v.selectedTower) this.hoverInfo = id;
        },
        onpointerleave: () => { this.hoverInfo = null; },
      },
      iconImg(icons.tower(id, 72), d.icon, 'ti'),
      h('span', { class: 'tc' }, String(d.cost)),
      h('span', { class: 'tk', 'aria-hidden': 'true' }, keyName(boundKey(`tower:${d.id === 'lanebarricade' ? 'barricade' : d.id}`))));
      this.towerBtns.set(id, btn);
      this.towerBar.appendChild(btn);
    }
    const followBtn = h('button', { class: 'btn icon sm', title: 'Kamera folgt dem Boss', 'aria-label': 'Kamera folgt dem Boss', 'aria-pressed': 'false', onclick: () => {
      this.followBoss = !this.followBoss;
      followBtn.setAttribute('aria-pressed', String(this.followBoss));
      toggleClass(followBtn, 'on', this.followBoss);
    } }, svgIcon('crown'));
    const miniBtn = h('button', { class: 'btn icon sm', title: 'Minikarte', 'aria-label': 'Minikarte ein/aus', onclick: () => { this.showMinimap = !this.showMinimap; } }, svgIcon('minimap'));
    const camCtl = h('div', { class: 'camctl' },
      (this.lockBtn = h('button', { class: `btn icon sm${v.rotationLocked ? ' on' : ''}`, 'data-testid': 'lock-rotation', title: 'Kameradrehung sperren/freigeben (Q/E wirkungslos, wenn gesperrt)', 'aria-label': 'Kameradrehung sperren', 'aria-pressed': String(v.rotationLocked), onclick: () => a.toggleRotationLock() }, svgIcon('lock'))),
      h('button', { class: 'btn icon sm', title: 'Drehen links (Q, 90°). Frei drehen: mittlere Maustaste oder Strg/Alt + Ziehen, Zwei-Finger-Drehung, Tasten , und .', 'aria-label': 'Kamera links drehen', onclick: () => a.rotate(-1) }, svgIcon('rotL')),
      h('button', { class: 'btn icon sm', title: 'Drehen rechts (E, 90°). Neigung: mittlere Maustaste/Strg + hoch/runter ziehen oder Bild↑/Bild↓', 'aria-label': 'Kamera rechts drehen', onclick: () => a.rotate(1) }, svgIcon('rotR')),
      h('button', { class: 'btn icon sm', 'data-testid': 'reset-view', title: 'Blickwinkel zurücksetzen (Pos1)', 'aria-label': 'Blickwinkel zurücksetzen', onclick: () => a.resetView() }, '⟲'),
      h('button', { class: 'btn icon sm', title: 'Hineinzoomen', 'aria-label': 'Hineinzoomen', onclick: () => a.zoom(0.8) }, svgIcon('plus')),
      h('button', { class: 'btn icon sm', title: 'Herauszoomen', 'aria-label': 'Herauszoomen', onclick: () => a.zoom(1.25) }, svgIcon('minus')),
      h('button', { class: 'btn icon sm', title: 'Ganze Karte', 'aria-label': 'Ganze Karte zeigen', onclick: () => a.fit() }, svgIcon('fit')),
      miniBtn,
      followBtn,
      h('button', { class: 'btn icon sm', title: 'Alle Reichweiten (R)', 'aria-label': 'Alle Reichweiten anzeigen', 'data-testid': 'ranges', onclick: () => a.toggleRanges() }, svgIcon('range')),
      h('button', { class: 'btn icon sm', title: 'Hilfe (H)', 'aria-label': 'Hilfe', onclick: () => a.help() }, svgIcon('help')),
    );
    this.mini.addEventListener('pointerdown', (e) => this.miniClick(e));
    this.floaters = h('div', { class: 'floaters', 'aria-hidden': 'true' });
    this.buildHeroBar();
    this.stage = h('div', { class: 'stage' }, this.floaters, this.banners,
      h('div', { class: 'stage-top' }, this.miniBadge, this.strip, this.bossBox, this.warnings),
      (this.stageLeft = h('div', { class: 'stage-left' }, this.mods, this.heroBar)),
      this.mini, camCtl, this.towerWin, h('div', { class: 'ver-tag', 'data-testid': 'version', 'aria-label': `Version ${__APP_VERSION__}` }, `v${__APP_VERSION__}`));
    this.panel.append(this.panelBody);
    const hold = (): void => { this.pointerHeld = true; };
    const release = (): void => { this.pointerHeld = false; };
    this.panel.addEventListener('pointerdown', hold);
    this.towerWin.addEventListener('pointerdown', hold);
    window.addEventListener('pointerup', release);
    window.addEventListener('pointercancel', release);
    this.root = h('div', { class: 'game-layout', 'data-testid': 'game' },
      top,
      h('div', { class: 'middle' }, this.stage, this.panel),
      this.towerBar,
      this.portraitHint,
      this.modal,
    );
    this.goldShown = s.gold;
    this.goldLast = s.gold;
  }

  hoverInfo: TowerId | null = null;

  // ------------------------------------------------------------ Banner

  banner(title: string, sub = '', kind: 'wave' | 'boss' | 'clear' | 'win' | 'lose' | 'info' = 'wave', ms = 2600): void {
    const el = h('div', { class: `banner ${kind}` },
      kind === 'boss' ? h('div', { class: 'banner-kicker' }, 'Ein Boss erscheint') : null,
      h('div', { class: 'banner-title' }, title),
      sub ? h('div', { class: 'banner-sub' }, sub) : null);
    if (kind === 'boss') this.stage.classList.add('letterbox');
    this.banners.appendChild(el);
    while (this.banners.children.length > 2) this.banners.firstElementChild?.remove();
    window.setTimeout(() => el.classList.add('out'), ms);
    window.setTimeout(() => {
      el.remove();
      if (kind === 'boss') this.stage.classList.remove('letterbox');
    }, ms + 500);
  }

  bossBanner(id: BossId): void {
    const b = BOSSES[id];
    this.banner(b.name, b.title, 'boss', 3000);
  }

  // ------------------------------------------------------------ Aktualisierung (pro Frame, nur bei Änderung)

  update(): void {
    const s = this.sim.s;
    const now = performance.now();
    const dt = Math.min(0.1, (now - this.lastFrame) / 1000);
    this.lastFrame = now;
    // Gold zählt sichtbar hoch/runter
    if (s.gold > this.goldLast) {
      this.goldStat.classList.remove('bump');
      void this.goldStat.offsetWidth;
      this.goldStat.classList.add('bump');
    }
    this.goldLast = s.gold;
    const diff = s.gold - this.goldShown;
    // Einnahmen zählen sichtbar hoch, Ausgaben werden sofort angezeigt.
    this.goldShown = diff <= 0 || Math.abs(diff) < 1 ? s.gold : this.goldShown + Math.max(1, diff * Math.min(1, dt * 9));
    setText(this.gold, fmt(this.goldShown));
    setText(this.lives, `${s.lives}/${s.maxLives}`);
    if (s.rules.endless) {
      const lim = currentTowerLimit(this.sim);
      const n = countedTowers(this.sim);
      const fb = focusBonus(n, lim);
      setText(this.limitV, Number.isFinite(lim) ? `${n}/${lim}` : `${n}`);
      setText(this.limitB, fb > 0 ? `+${Math.round(fb * 100)} %` : '±0 %');
      toggleClass(this.limitStat, 'focus', fb > 0);
      toggleClass(this.limitStat, 'full', n >= lim);
      this.limitStat.title = `Bauplätze ${n}/${lim} · freie Plätze machen alle Türme stärker: jetzt +${Math.round(fb * 100)} % Schaden. Klicken für Erklärung.`;

    }
    toggleClass(this.lives.parentElement as HTMLElement, 'low', s.lives <= Math.max(3, s.maxLives * 0.25));
    setText(this.wave, s.rules.endless ? `${s.wave} · Endlos` : `${s.wave}/${s.rules.waves}`);
    if (s.heroes.length) setText(this.energy, String(Math.floor(s.riftEnergy)));
    const build = s.phase === 'build';
    this.startBtn.disabled = !build;
    const auto = s.he && build && s.he.autoTimer > 0 ? ` · ${Math.ceil(s.he.autoTimer)} s` : '';
    if (s.he && s.phase === 'combat') {
      // Helden-Endlos: nächste Welle vorzeitig rufen, sobald die aktuelle vollständig erschienen ist
      const why = earlyCallState(s);
      this.startBtn.disabled = why !== null;
      this.startBtn.title = why ?? `Nächste Welle jetzt rufen (Enter) – +${EARLY_CALL_GOLD} Gold je Gegner auf dem Feld`;
      setText(this.startLabel, why ? 'Kampf' : `Welle ${s.wave + 1} rufen`);
    } else {
      this.startBtn.title = s.he ? 'Nächste Welle sofort starten (Leertaste/Enter)' : 'Welle starten (Leertaste)';
      setText(this.startLabel, build ? `Welle ${s.wave + 1}${auto}` : s.phase === 'combat' ? 'Kampf' : '…');
    }
    toggleClass(this.startBtn, 'countdown', !!auto);
    const upSig = `${Math.floor(s.gold)}|${s.towers.map((t) => t.level).join('')}|${s.towers.length}`;
    if (upSig !== this.upAllSig) {
      this.upAllSig = upSig;
      const ups = upgradableTowers(this.sim);
      const cheapest = Math.min(...ups.map((t) => nextUpgradeCost(t) ?? Infinity));
      const all = upgradeAllCost(this.sim);
      this.upAllBtn.disabled = !ups.length || cheapest > s.gold;
      this.upAllBtn.title = ups.length
        ? `Alle Türme bis zur höchsten Stufe aufwerten, solange das Gold reicht (günstigste zuerst, ohne Spezialisierung). ${ups.length} Türme offen, komplett ${fmt(all.total)} Gold.`
        : 'Alle Türme sind auf der höchsten Stufe (Spezialisierung im Turmpanel wählen)';
    }
    toggleClass(this.lockBtn, 'on', this.v.rotationLocked);
    const mini = s.paused && this.v.miniPaused && !this.sim.isOver();
    toggleClass(this.miniBtn, 'on', mini);
    toggleClass(this.miniBadge, 'hidden', !mini);
    this.lockBtn.setAttribute('aria-pressed', String(this.v.rotationLocked));
    setText(this.speedLabel, `${s.speed}×`);
    this.pauseBtn.replaceChildren(svgIcon(s.paused ? 'play' : 'pause'));
    this.pauseBtn.setAttribute('aria-pressed', String(s.paused));
    for (const [id, btn] of this.towerBtns) {
      const cost = towerCost(this.sim, id);
      const tc = btn.querySelector('.tc');
      const locked = !!s.he && !towerUnlocked(id, s.he, s.wave);
      toggleClass(btn, 'locked', locked);
      if (locked) {
        if (tc) setText(tc, `W${towerUnlockWave(id, s.he)}`);
        btn.title = `${TOWERS[id].name} – wird ab Welle ${towerUnlockWave(id, s.he)} freigeschaltet`;
        continue;
      }
      if (s.he) btn.title = `${TOWERS[id].name} (${keyName(boundKey(`tower:${id}`))}) – ${TOWERS[id].role}${TOWERS[id].damageType ? ` · ${DAMAGE_NAMES[TOWERS[id].damageType!]}` : ''}${s.rules.endless && !countsTowardLimit(id) ? ' · zählt nicht zum Turmlimit' : ''}`;
      if (id === 'lanebarricade') {
        const ch = laneCharges(this.sim);
        if (tc) setText(tc, ch.left > 0 ? `${ch.left}/${LANE_BARRICADE.maxCharges}` : `${Math.floor(ch.next / 60)}:${String(Math.floor(ch.next % 60)).padStart(2, '0')}`);
        toggleClass(btn, 'poor', ch.left <= 0);
        btn.title = `Lane-Barrikade (${keyName(boundKey('tower:barricade'))}) – auf die Lane setzen, verlangsamt Bodengegner um 50 %. ${ch.left > 0 ? `${ch.left} verfügbar` : 'Abklingzeit läuft'} (höchstens ${LANE_BARRICADE.maxCharges} je ${LANE_BARRICADE.window / 60} Minuten, nicht direkt vor einem Riss).`;
        toggleClass(btn, 'active', this.v.buildDef === id);
        continue;
      }
      if (tc) setText(tc, String(cost));
      toggleClass(btn, 'poor', s.gold < cost);
      toggleClass(btn, 'active', this.v.buildDef === id);
      toggleClass(btn, 'prespec', prespecAvailable(this.sim, id));
      toggleClass(btn, 'limitfree', s.rules.endless && !countsTowardLimit(id) && TOWERS[id].attack !== 'none');
    }
    this.updateStrip();
    this.updateBoss();
    this.updateWarnings();
    this.updateMods();
    this.updateHeroBar();
    this.updatePanel();
    this.updateTowerList();
    this.updateModal();
    this.updateMinimap(dt);
  }

  /** 3.0: Weltstufe und Wellen-Affixe als Chips. */
  private appendWaveTraits(w: { tier?: number; affixes?: AffixId[]; immune?: DamageType }): void {
    if (w.tier) this.strip.append(h('span', { class: 'chip tier', 'data-testid': 'world-tier', title: `Weltstufe ${w.tier}: Gegner +${Math.round(SCALING.tierHp * w.tier * 100)} % LP, mehr Rüstung und Tempo – dafür +${Math.round(SCALING.tierReward * w.tier * 100)} % Kopfgeld` }, `🌐 Weltstufe ${w.tier}`));
    for (const a of w.affixes ?? []) {
      const d = AFFIXES[a];
      const extra = a === 'immune' && w.immune ? ` gegen ${DAMAGE_NAMES[w.immune]}` : '';
      this.strip.append(h('span', { class: 'chip affix', 'data-testid': `affix-${a}`, title: `${d.description}${extra}` }, `${d.icon} ${d.name}${extra}`));
    }
  }

  private updateStrip(): void {
    const s = this.sim.s;
    let sig: string;
    if (s.phase === 'build' || s.phase === 'choice') {
      const w = previewNextWave(s);
      sig = `b${s.wave}-${w ? w.index : 'x'}`;
      if (sig === this.stripSig) return;
      this.stripSig = sig;
      clear(this.strip);
      if (!w) return;
      const ch = w.groups.some((g) => g.challenge);
      this.strip.append(h('span', { class: 'strip-label' }, `Nächste Welle ${w.index}${ch ? ' · ⚔ CHALLENGE-BOSS' : w.isBoss ? ' · BOSS' : ''}:`));
      for (const g of w.groups) {
        const sum = waveSummary({ ...w, groups: [g] })[0];
        const id = g.enemy;
        const img = g.boss ? this.icons.boss(g.boss, 48) : id !== 'boss' ? this.icons.enemy(id, 48) : null;
        this.strip.append(h('button', {
          class: `chip${sum.elite ? ' elite' : ''}${sum.boss ? ' boss' : ''}${g.mutation ? ' mutated' : ''}`, title: sum.label,
          onclick: () => { this.v.infoEnemy = this.v.infoEnemy === id && !g.boss ? null : id; this.bossInfo = g.boss; this.panelSig = ''; },
        }, iconImg(img, sum.icon, 'chip-ic'), ` ${sum.label} ×${sum.count}`));
      }
      if (w.event) this.strip.append(h('span', { class: 'chip event', title: TERRAIN_EVENTS[w.event].description }, `☁ ${TERRAIN_EVENTS[w.event].name}`));
      this.appendWaveTraits(w);
    } else {
      const left = s.spawnQueue.length + s.enemies.filter((e) => e.alive).length;
      const over = s.he && s.wave >= SCALING.overrunFrom && s.he.overrunWave === s.wave ? Math.max(0, Math.ceil(SCALING.overrunAfter - (s.he.overrunT ?? 0))) : -1;
      sig = `c${left}-${over}-${s.wave}`;
      if (sig === this.stripSig) return;
      this.stripSig = sig;
      clear(this.strip);
      if (s.phase === 'combat') this.strip.append(h('span', { class: 'strip-label' }, `Welle ${s.wave} · verbleibende Gegner: ${left}${over >= 0 ? ` · nächste rückt nach in ${over} s` : ''}`));
      if (s.currentWave) this.appendWaveTraits(s.currentWave);
    }
  }

  bossInfo: BossId | null = null;

  private updateBoss(): void {
    const s = this.sim.s;
    const b = s.boss ? this.sim.c.enemyByUid.get(s.boss.uid) : undefined;
    toggleClass(this.bossBox, 'hidden', !b || !s.boss);
    if (!b || !s.boss) return;
    const def = BOSSES[s.boss.id];
    setText(this.bossName, `${b.challenge ? '⚔ CHALLENGE · ' : ''}${def.name} – ${def.title}`);
    toggleClass(this.bossBox, 'challenge', !!b.challenge);
    const casting = b.abilities.find((x) => x.windup > 0);
    const plates = b.plates.length ? ` · Schwachpunkte ${b.plates.filter((p) => p > 0).length}/${b.plates.length}` : '';
    const partner = s.enemies.find((e) => e.alive && e.boss && e.uid !== b.uid);
    setText(this.bossPhase, `${s.boss.phaseName}${plates}${partner ? ` · ${BOSSES[partner.boss as BossId].name}: ${Math.round((100 * partner.hp) / partner.maxHp)} %` : ''}${b.enraged ? ' · RASEND' : ''}${casting ? ` · wirkt: ${ABILITIES[casting.id].name}` : ''}`);
    this.bossHp.fill.style.width = `${(100 * b.hp) / b.maxHp}%`;
    if (this.bossHp.extra) this.bossHp.extra.style.width = b.maxShield > 0 ? `${(100 * b.shield) / b.maxShield}%` : '0%';
    setText(this.bossHp.label, `${fmt(b.hp)} / ${fmt(b.maxHp)}${b.shield > 0 ? ` · Schild ${fmt(b.shield)}` : ''}`);
    this.bossPoise.fill.style.width = `${(100 * b.poise) / Math.max(1, b.maxPoise)}%`;
    setText(this.bossPoise.label, `Standfestigkeit ${Math.round((100 * b.poise) / Math.max(1, b.maxPoise))} %`);
  }

  private warnSig = '';
  private updateWarnings(): void {
    const s = this.sim.s;
    const list = s.telegraphs.slice(0, 3);
    const sig = list.map((t) => `${t.uid}:${Math.ceil(t.t * 2)}`).join('|');
    if (sig === this.warnSig) return;
    this.warnSig = sig;
    clear(this.warnings);
    for (const t of list) {
      this.warnings.append(h('div', { class: `warn ${t.severity}` },
        h('span', {}, `${t.severity === 'danger' ? '⚠' : '!'} ${t.text}`),
        h('span', { class: 'cd' }, `${fmtNum(Math.max(0, t.t), 1)} s`)));
    }
  }

  private updateMods(): void {
    const s = this.sim.s;
    const sig = s.modifiers.join(',');
    if (sig === this.modsSig) return;
    this.modsSig = sig;
    clear(this.mods);
    for (const id of s.modifiers) {
      const m = MODIFIERS[id];
      this.mods.append(h('button', { class: 'mod-chip', title: `${m.name}: ${m.description}`, 'aria-label': `Modifikator ${m.name}: ${m.description}`, onclick: () => this.onModInfo?.(id) },
        h('span', { 'aria-hidden': 'true' }, m.icon), h('span', { class: 'mn' }, m.name)));
    }
  }

  onModInfo: ((id: ModifierId) => void) | null = null;

  // ------------------------------------------------------------ Heldenleiste

  private buildHeroBar(): void {
    const s = this.sim.s;
    toggleClass(this.heroBar, 'hidden', s.heroes.length === 0);
    s.heroes.forEach((hero) => {
      const card: HeroCardEls = { root: h('div', { class: 'hero-card', 'data-hero': hero.id, 'data-testid': `hero-${hero.id}` }), sig: '', abil: [], abilFill: [] };
      this.heroCards.set(hero.id, card);
      this.heroBar.appendChild(card.root);
    });
  }

  private heroKey(i: number): string {
    return i === 0 ? 'Z' : 'X';
  }

  private updateHeroBar(): void {
    const s = this.sim.s;
    s.heroes.forEach((hero, i) => {
      const card = this.heroCards.get(hero.id);
      if (!card) return;
      const def = HEROES[hero.id];
      const st = heroStatsOf(this.sim, hero);
      const placing = this.v.placingHero === hero.id;
      const selected = this.v.selectedHero === hero.uid;
      const sig = `${hero.state}-${hero.level}-${hero.pendingPerk}-${hero.mode}-${placing}-${selected}-${s.phase}`;
      if (sig !== card.sig) {
        card.sig = sig;
        card.abil = [];
        card.abilFill = [];
        card.hp = card.time = card.cd = card.cdText = card.ult = card.cost = undefined;
        const root = card.root;
        clear(root);
        root.className = `hero-card ${hero.state}${placing ? ' placing' : ''}${selected ? ' selected' : ''}${hero.pendingPerk ? ' perk' : ''}`;
        root.style.setProperty('--hc', `#${def.accent.toString(16).padStart(6, '0')}`);
        const portrait = h('button', {
          class: 'hero-portrait', title: `${def.name} (${this.heroKey(i)})`, 'aria-label': `${def.name}: ${hero.state === 'ready' ? 'einsetzen' : hero.state === 'active' ? 'auswählen' : 'nicht bereit'}`,
          onclick: () => this.a.heroCard(hero.id),
        }, iconImg(this.icons.hero(hero.id, 96), def.icon, 'hp-img'), h('span', { class: 'hero-key' }, this.heroKey(i)), h('span', { class: 'hero-lvl' }, String(hero.level)));
        const name = h('div', { class: 'hero-name' }, def.name.split(' ').slice(-1)[0], hero.pendingPerk ? h('span', { class: 'perk-badge', title: 'Perk wählen' }, '★') : null);
        const body = h('div', { class: 'hero-body' }, name);
        if (hero.state === 'ready') {
          card.cost = h('button', { class: 'btn sm primary hero-deploy', onclick: () => this.a.heroCard(hero.id) }, placing ? 'Feld wählen…' : `Einsetzen`, h('span', { class: 'cost' }, svgIcon('energy'), String(def.cost)));
          put(body, card.cost);
        } else if (hero.state === 'active') {
          card.hp = h('div', { class: 'hb-fill' });
          card.time = h('div', { class: 'hb-fill time' });
          put(body, h('div', { class: 'hb' }, card.hp), h('div', { class: 'hb thin' }, card.time));
          const row = h('div', { class: 'hero-abils' });
          const keys = ['A', 'S', 'D'];
          ([0, 1, 2] as const).forEach((slot) => {
            const ab = slot === 2 ? def.ultimate : def.abilities[slot];
            const fill = h('span', { class: 'ab-fill' });
            const b = h('button', {
              class: `ab${slot === 2 ? ' ult' : ''}`, title: `${ab.name} (${keys[slot]}): ${ab.description}`, 'aria-label': `${ab.name}`,
              onclick: () => this.a.heroAbility(hero.id, slot),
            }, fill, h('span', { class: 'ab-k' }, keys[slot]));
            card.abil.push(b);
            card.abilFill.push(fill);
            row.appendChild(b);
          });
          put(body, row);
          if (selected) {
            put(body, h('div', { class: 'hero-modes' }, (['hold', 'advance', 'retreat'] as HeroMode[]).map((m) =>
              h('button', { class: `mode${hero.mode === m ? ' on' : ''}`, title: MODE_LABEL[m], 'aria-label': MODE_LABEL[m], 'aria-pressed': String(hero.mode === m), onclick: () => this.a.heroMode(hero.id, m) }, svgIcon(m)))));
          }
        } else {
          card.cd = h('div', { class: 'hb-fill cd' });
          card.cdText = h('span', { class: 'cd-t' });
          put(body, h('div', { class: 'muted tiny' }, hero.state === 'down' ? 'Gefallen' : 'Erholt sich', ' ', card.cdText), h('div', { class: 'hb thin' }, card.cd));
        }
        put(root, portrait, body);
      }
      // dynamische Werte
      if (card.hp) card.hp.style.width = `${Math.max(0, (100 * hero.hp) / Math.max(1, st.maxHp))}%`;
      if (card.time) card.time.style.width = `${Math.max(0, Math.min(100, (100 * hero.activeLeft) / Math.max(1, st.activeTime)))}%`;
      if (card.cd && card.cdText) {
        const total = hero.state === 'down' ? HERO_RULES.downCooldown : HERO_RULES.cooldown;
        card.cd.style.width = `${Math.max(0, 100 - (100 * hero.cooldown) / total)}%`;
        setText(card.cdText, `${Math.ceil(hero.cooldown)} s`);
      }
      if (card.cost) toggleClass(card.cost, 'poor', s.riftEnergy < def.cost);
      card.abil.forEach((b, slot) => {
        const r = abilityReady(this.sim, hero, slot as 0 | 1 | 2);
        toggleClass(b, 'ready', r.ok);
        let p = 1;
        if (slot === 2) p = hero.ult / 100;
        else {
          const ab = def.abilities[slot];
          p = 1 - hero.abilityCd[slot] / Math.max(1, ab.cooldown * st.abilityCdMul);
        }
        card.abilFill[slot].style.height = `${Math.round(Math.max(0, Math.min(1, p)) * 100)}%`;
      });
    });
  }

  // ------------------------------------------------------------ Minikarte

  private updateMinimap(dt: number): void {
    toggleClass(this.mini, 'hidden', !this.showMinimap);
    if (!this.showMinimap) return;
    this.miniTimer -= dt;
    if (this.miniTimer > 0) return;
    this.miniTimer = 0.15;
    const s = this.sim.s;
    const m = s.map;
    const cell = Math.max(3, Math.floor(168 / Math.max(m.width, m.height * 1.2)));
    const w = m.width * cell;
    const hh = m.height * cell;
    if (!this.miniBase || this.miniBase.width !== w) {
      const c = document.createElement('canvas');
      c.width = w;
      c.height = hh;
      const ctx = c.getContext('2d');
      if (ctx) {
        const pal = BIOMES[m.biome].palette;
        const hex = (n: number): string => `#${n.toString(16).padStart(6, '0')}`;
        for (let y = 0; y < m.height; y++) {
          for (let x = 0; x < m.width; x++) {
            const cc = m.cells[y * m.width + x];
            ctx.fillStyle = cc === Cell.Path || cc === Cell.Spawn || cc === Cell.Goal ? hex(pal.path) : cc === Cell.Blocked ? hex(pal.obstacle) : m.heights[y * m.width + x] >= 2 ? hex(pal.high) : hex(pal.ground);
            ctx.fillRect(x * cell, y * cell, cell, cell);
          }
        }
        ctx.fillStyle = '#c060ff';
        for (const sp of spawnsOf(m)) ctx.fillRect(sp.x * cell - 1, sp.y * cell - 1, cell + 2, cell + 2);
        ctx.fillStyle = '#60f0ff';
        ctx.fillRect(m.goal.x * cell - 1, m.goal.y * cell - 1, cell + 2, cell + 2);
      }
      this.miniBase = c;
      this.mini.width = w;
      this.mini.height = hh;
      this.mini.style.width = `${w}px`;
      this.mini.style.height = `${hh}px`;
    }
    const ctx = this.mini.getContext('2d');
    if (!ctx) return;
    ctx.drawImage(this.miniBase, 0, 0);
    for (const t of s.towers) {
      ctx.fillStyle = `#${TOWERS[t.def].color.toString(16).padStart(6, '0')}`;
      ctx.fillRect(t.cx * cell + 1, t.cy * cell + 1, cell - 2, cell - 2);
    }
    for (const e of s.enemies) {
      if (!e.alive || e.stealthed) continue;
      ctx.fillStyle = e.boss ? '#ff3040' : e.elite ? '#ffd84a' : '#ff6a6a';
      const r = e.boss ? cell * 0.9 : cell * 0.35;
      ctx.beginPath();
      ctx.arc(e.x * cell, e.y * cell, r, 0, Math.PI * 2);
      ctx.fill();
    }
    for (const hero of s.heroes) {
      if (hero.state !== 'active') continue;
      ctx.fillStyle = `#${HEROES[hero.id].accent.toString(16).padStart(6, '0')}`;
      ctx.strokeStyle = '#000';
      ctx.beginPath();
      ctx.arc(hero.x * cell, hero.y * cell, cell * 0.6, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
    }
    if (this.camQuad.length === 4) {
      ctx.strokeStyle = 'rgba(255,255,255,0.85)';
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      this.camQuad.forEach(([x, y], i) => (i ? ctx.lineTo(x * cell, y * cell) : ctx.moveTo(x * cell, y * cell)));
      ctx.closePath();
      ctx.stroke();
    }
  }

  private miniClick(e: PointerEvent): void {
    e.preventDefault();
    e.stopPropagation();
    const r = this.mini.getBoundingClientRect();
    const m = this.sim.s.map;
    const x = ((e.clientX - r.left) / r.width) * m.width;
    const z = ((e.clientY - r.top) / r.height) * m.height;
    this.a.lookAt(x, z);
  }

  // ------------------------------------------------------------ Seitenleiste

  private updatePanel(): void {
    const s = this.sim.s;
    const v = this.v;
    const t = v.selectedTower ? this.sim.c.towerByUid.get(v.selectedTower) : undefined;
    const hero = v.selectedHero ? s.heroes.find((x) => x.uid === v.selectedHero) : undefined;
    const foe = v.selectedEnemy ? this.sim.c.enemyByUid.get(v.selectedEnemy) : undefined;
    if (v.selectedEnemy && (!foe || !foe.alive)) v.selectedEnemy = 0;
    let sig: string;
    let vol = '';
    if (t) {
      const st = this.sim.c.towerStats.get(t.uid);
      const act = activeState(this.sim, t);
      const fus = `${fusionOptions(this.sim, t).map((f) => `${f.partner.uid}:${f.affordable}:${f.partner.level}:${f.partner.spec}`).join(',')}|${s.towers.length}|${s.wave}`;
      // Strukturteil (Neuaufbau sofort) – Gold zählt nur als „bezahlbar ja/nein“ der Kosten in diesem Panel
      const costs = [nextUpgradeCost(t) ?? -1, specCost(t, 1), specCost(t, 2), starCost(t), ...(this.v.groupDef === t.def ? [Math.min(...s.towers.filter((o) => o.def === t.def && fullyBuilt(o)).map((o) => starCost(o)))] : []), respecCost(t, 1), respecCost(t, 2), ...(this.v.groupDef === t.def ? [1, 2].map((sp) => Math.min(...s.towers.filter((o) => o.def === t.def && o.spec !== sp).map((o) => specTargetCost(o, sp as 1 | 2)))) : []), ...FUSIONS.filter((r) => r.a === t.def || r.b === t.def).map((r) => r.cost)];
      const aff = costs.map((c) => (c >= 0 && c <= s.gold ? 1 : 0)).join('');
      // Live-Teil (Neuaufbau höchstens 1× je Sekunde)
      vol = `${Math.floor(t.damageDealt / 200)}-${t.kills}-${st ? st.damage.toFixed(1) : ''}-${Math.ceil(t.activeCd)}-${Math.floor(s.gold / 50)}`;
      sig = `t${t.uid}-${t.stars ?? 0}-${aff}-${this.sellAllArmed}-${this.runeSel}-${s.he?.runes ? s.he.runes.map((r) => `${r.uid}:${r.def ?? ''}`).join(',') : ''}-${this.v.groupDef === t.def ? s.towers.filter((o) => o.def === t.def).map((o) => `${o.level}${o.spec}${o.stars ?? 0}`).join('') : ''}-${t.def}-${t.level}-${t.spec}-${t.priority}-${s.phase}-${this.sellArmed === t.uid}-${t.disrupted > 0}-${t.stunned > 0}-${s.network.version}-${act.available}-${fus}-${s.paused && this.v.miniPaused}`;
    } else if (v.selectedObstacle >= 0 && s.he) {
      sig = `x${v.selectedObstacle}-${s.he.mining ? Math.ceil(s.he.mining.t) : 'n'}-${s.map.cells[v.selectedObstacle]}-${s.wave}-${s.he.champion.dead > 0}`;
    } else if (foe && foe.alive) {
      sig = `n${foe.uid}-${Math.ceil(foe.hp)}-${Math.ceil(foe.shield)}-${foe.phase}-${foe.statuses.map((x) => `${x.id}${x.stacks}${Math.ceil(x.t)}`).join(',')}-${foe.stealthed}-${foe.burrowed}-${Math.round(slowFactor(foe) * 20)}`;
    } else if (hero) {
      const st = heroStatsOf(this.sim, hero);
      sig = `h${hero.uid}-${hero.state}-${hero.level}-${Math.floor(hero.xp)}-${hero.pendingPerk}-${hero.perks.join('')}-${hero.mode}-${hero.kills}-${st.damage.toFixed(0)}-${activeDuos(this.sim).join(',')}`;
    } else if (v.placingHero) {
      sig = `p${v.placingHero}-${s.riftEnergy >= HEROES[v.placingHero].cost}`;
    } else if (v.buildDef) {
      sig = `b${v.buildDef}-${v.buildSpec ?? 0}-${v.pendingCell ? `${v.pendingCell.x},${v.pendingCell.y}` : ''}-${v.pendingCheck?.ok}-${v.pendingCheck?.reason}-${s.gold >= (v.buildSpec ? prespecCost(this.sim, v.buildDef, v.buildSpec) : towerCost(this.sim, v.buildDef))}-${v.touchMode}`;
    } else if (v.infoEnemy) {
      sig = `e${v.infoEnemy}-${this.bossInfo}`;
    } else if (this.hoverInfo) {
      sig = `o${this.hoverInfo}`;
    } else {
      sig = `i${s.phase}-${s.wave}-${s.heroes.map((x) => x.state).join(',')}`;
    }
    // Während eine Maustaste/ein Finger im Panel gedrückt ist, nie neu aufbauen (sonst geht der Klick verloren).
    // Reine Live-Werte (Schaden, Abschüsse, Abklingzeit) bauen höchstens einmal pro Sekunde neu auf.
    const now = performance.now();
    if (sig === this.panelSig && (vol === this.panelVol || now - this.panelBuiltAt < 1000)) return;
    if (this.pointerHeld && this.panelSig !== '') return;
    // Beim Neuaufbau desselben Inhalts die Scrollposition behalten (sonst springt das Panel nach oben)
    const sameSubject = sig.split('-')[0] === this.panelSig.split('-')[0];
    const keepScroll = sameSubject ? [this.panel.scrollTop, this.panelBody.scrollTop] : [0, 0];
    this.panelSig = sig;
    this.panelVol = vol;
    this.panelBuiltAt = now;
    clear(this.panelBody);
    const open = !!(t || foe || hero || (v.selectedObstacle >= 0 && s.he) || v.placingHero || v.buildDef || v.infoEnemy || this.hoverInfo);
    toggleClass(this.panel, 'open', open);
    if (t) this.renderTower(t);
    else if (v.selectedObstacle >= 0 && s.he) this.renderObstacle(v.selectedObstacle);
    else if (foe) this.renderEnemyInstance(foe);
    else if (hero) this.renderHero(hero);
    else if (v.placingHero) this.renderPlacing(v.placingHero);
    else if (v.buildDef) this.renderBuild(v.buildDef);
    else if (v.infoEnemy) this.renderEnemy(v.infoEnemy);
    else if (this.hoverInfo) this.renderBuild(this.hoverInfo, true);
    else this.renderIdle();
    this.panel.scrollTop = keepScroll[0];
    this.panelBody.scrollTop = keepScroll[1];
  }

  private closeBtn(): HTMLElement {
    return h('button', { class: 'btn icon sm close', 'aria-label': 'Schließen', title: 'Schließen (Esc)', onclick: () => { this.v.infoEnemy = null; this.a.cancel(); this.panelSig = ''; } }, svgIcon('close'));
  }

  private renderIdle(): void {
    const s = this.sim.s;
    const mode = MODE_NAMES[s.mode];
    put(this.panelBody,
      h('h3', {}, mode),
      h('p', { class: 'muted' }, `${s.map.name} · Seed ${s.seed.text}`),
      s.he ? h('p', { class: 'small' }, `Helden-Endlos: Wellen starten automatisch (Countdown auf der Startschaltfläche). Klick/Rechtsklick bewegt den Helden, A/S/D/W wirken Fähigkeiten, Tab öffnet den Charakter, X den Laden (am Laden nahe dem Kern).`) : null,
      h('p', {}, s.phase === 'build'
        ? 'Bauphase: Wähle unten einen Turm und platziere ihn. Starte die Welle, wenn du bereit bist.'
        : 'Kampf läuft. Tippe Türme an, um sie aufzuwerten, Fähigkeiten auszulösen oder ihre Zielwahl zu ändern.'),
      s.heroes.length ? h('p', { class: 'small' }, `Helden: ${s.heroes.map((x) => HEROES[x.id].name).join(' & ')}. Mit Z/X (oder Porträt antippen) auf dem Weg einsetzen – das kostet Rissenergie, die du durch Abschüsse, Reaktionen und Wellen sammelst.`) : null,
      h('p', { class: 'muted small' }, s.map.variant === 'mazing'
        ? 'Mazing: Türme und Barrikaden lenken die Route. Der Weg kann nie vollständig blockiert werden.'
        : 'Classic: Gegner folgen dem markierten Weg. Anhöhen geben +0,4 Reichweite, Leylinien (Ring) +15 % Schaden, Goldadern +50 % Minenertrag.'),
      spawnsOf(s.map).length > 1 ? h('p', { class: 'small warnline' }, `⚠ Weggabelung: Gegner kommen aus ${spawnsOf(s.map).length} Rissen.`) : null,
    );
  }

  private renderBuild(id: TowerId, hover = false): void {
    const d = TOWERS[id];
    const v = this.v;
    const spec = !hover && v.buildDef === id ? (v.buildSpec ?? 0) : 0;
    const cost = spec ? prespecCost(this.sim, id, spec) : towerCost(this.sim, id);
    const st = baseStatsOf(id);
    const body = this.panelBody;
    put(body, h('div', { class: 'ph' }, iconImg(this.icons.tower(id, 96, spec ? 4 : 1, spec), d.icon, 'big-ic'),
      h('div', {}, h('h3', {}, d.name), h('div', { class: 'muted small', 'data-testid': spec ? 'prespec-build' : undefined }, spec ? `★ ${d.specs[spec - 1]?.name} · Höchststufe · ${fmt(cost)} Gold` : `${d.role}${d.damageType ? ` · ${DAMAGE_NAMES[d.damageType]}` : ''} · ${cost} Gold`)), hover ? null : this.closeBtn()));
    if (!hover) {
      const chk = v.pendingCheck;
      if (v.touchMode) {
        if (v.pendingCell && chk) {
          put(body, chk.ok
            ? h('button', { class: 'btn primary wide', 'data-testid': 'confirm-build', onclick: () => this.a.confirmBuild() }, `✔ Hier bauen (${cost} Gold)`)
            : h('p', { class: 'bad' }, `✖ ${chk.reason ?? 'Nicht möglich'}`));
          if (chk.ok) put(body, h('p', { class: 'muted small' }, 'Tipp: dasselbe Feld erneut antippen baut ebenfalls.'));
        } else {
          put(body, h('p', { class: 'hint' }, '👆 Tippe ein freies Feld an, um die Platzierung zu prüfen.'));
        }
      } else {
        put(body, h('p', { class: 'hint' }, 'Klicke auf ein freies Feld. Umschalt+Klick baut mehrere. Rechtsklick oder Esc bricht ab.'));
        if (chk && !chk.ok && v.pendingCell) put(body, h('p', { class: 'bad' }, `✖ ${chk.reason ?? ''}`));
      }
      if (this.sim.s.gold < cost) put(body, h('p', { class: 'bad' }, `Es fehlen ${cost - this.sim.s.gold} Gold.`));
    }
    put(body,
      h('div', { class: 'preview3d' }, turntableEl(this.icons.towerTurntable(id, 130, 4, 1), iconImg(this.icons.tower(id, 130, 4, 1), '', 'p3d')), iconImg(this.icons.tower(id, 130, 4, 2), '', 'p3d')),
      h('p', { class: 'muted tiny center' }, 'Endausbau: Spezialisierung A (drehend) und B'),
      h('p', {}, d.description),
      this.statTable(statLines(st, d)),
      d.active ? h('p', { class: 'small' }, h('b', {}, `Aktiv ab Stufe ${ACTIVE_MIN_LEVEL}: ${TOWER_ACTIVES[d.active].name}`), ` – ${TOWER_ACTIVES[d.active].description}`) : null,
      this.fieldHint(id, hover ? null : v.pendingCell),
      this.fusionHints(id, hover ? null : v.pendingCell),
      h('details', {}, h('summary', {}, 'Synergien & Konter'), h('p', { class: 'small' }, '✚ ', d.synergies), h('p', { class: 'small' }, '✖ ', d.counters), this.matchupLines(d)),
    );
    if (!hover) put(body, h('button', { class: 'btn wide', onclick: () => this.a.cancel() }, 'Abbrechen'));
  }

  /** Spezialfelder unter dem gewählten Feld (Goldvorkommen, Kristallader, Leylinie). */
  private fieldHint(id: TowerId, cell: { x: number; y: number } | null): HTMLElement | null {
    const m = this.sim.s.map;
    const lines: string[] = [];
    if (cell) {
      const cells = footprintCells(id, cell.x, cell.y, m.width).filter((k) => k >= 0 && k < m.cells.length);
      const i = cell.y * m.width + cell.x;
      if (towerSize(id) > 1) lines.push(`▦ Belegt ${towerSize(id)}×${towerSize(id)} Felder (ab dem gewählten Feld nach rechts/unten)`);
      if (cells.some((k) => m.veins.includes(k))) lines.push(id === 'goldmine' ? `💰 Goldvorkommen: +50 % Ertrag und zusätzlich +${veinBonus(this.sim.s.wave + 1)} Gold je Welle` : '💰 Goldvorkommen – ideal für einen Goldgräber (zusätzlicher Goldabbau)');
      if (cells.some((k) => m.deposits?.includes(k))) lines.push(id === 'goldmine' ? `◆ Kristallader: Goldgräber fördert zusätzlich ${1 + Math.floor((this.sim.s.wave + 1) / 8)}+ Kristall je Welle` : '◆ Kristallader – ein Goldgräber fördert hier zusätzlich Kristall');
      if (m.ley.includes(i)) lines.push('✦ Leylinie: +15 % Schaden für Türme auf diesem Feld');
    } else if (id === 'goldmine') {
      lines.push(`Belegt 2×2 Felder. Spezialfelder: Goldvorkommen (gelbe Kristalle) geben +50 % Ertrag und zusätzlich Gold je Welle${m.deposits?.length ? ', Kristalladern (blau) zusätzlich Kristall' : ''}.`);
    }
    return lines.length ? h('div', { class: 'fusion-hint field-hint', 'data-testid': 'field-hint' }, lines.map((l) => h('div', { class: 'small' }, l))) : null;
  }

  /** Fusionsmöglichkeiten schon beim Platzieren: Rezept, Partner in Reichweite des gewählten Feldes. */
  private fusionHints(id: TowerId, cell: { x: number; y: number } | null): HTMLElement | null {
    const s = this.sim.s;
    const recipes = cell ? fusionPreview(this.sim, id, cell.x, cell.y) : FUSIONS.filter((r) => r.a === id || r.b === id).map((r) => ({ recipe: r, partnerDef: r.a === id ? r.b : r.a, partners: [] as TowerInstance[] }));
    if (!recipes.length) return null;
    return h('div', { class: 'fusion-hint', 'data-testid': 'fusion-hint' },
      h('b', {}, '✦ Fusionen mit diesem Turm'),
      h('ul', { class: 'fusion-req' }, recipes.map((f) => {
        const ld = TOWERS[f.recipe.id];
        const pd = TOWERS[f.partnerDef];
        const locked = s.he && !towerUnlocked(f.partnerDef, s.he, s.wave);
        let status = '';
        if (cell) status = f.partners.length ? ` – ✔ ${f.partners.length}× ${pd.name} in Reichweite (gold markiert)` : ` – kein ${pd.name} im Umkreis von ${fmtNum(FUSION_RANGE, 1)} Feldern`;
        return h('li', { class: cell ? (f.partners.length ? 'good' : 'bad') : '' },
          iconImg(this.icons.tower(f.recipe.id, 32), ld.icon, 'chip-ic'), ` ${pd.name} → `, h('b', {}, ld.name), ` (${f.recipe.cost} Gold)`,
          locked ? ` · ${pd.name} ab Welle ${towerUnlockWave(f.partnerDef, s.he)}` : '', status);
      })),
      h('p', { class: 'tiny muted' }, `Beide Türme brauchen Stufe 4 und eine Spezialisierung, höchstens ${fmtNum(FUSION_RANGE, 1)} Felder Abstand.`));
  }

  private statTable(rows: [string, string][]): HTMLElement {
    return h('table', { class: 'stats-table' }, h('tbody', {}, rows.map(([k, val]) => h('tr', {}, h('th', {}, k), h('td', {}, val)))));
  }

  /** „Alle verkaufen“ mit zweitem Klick zur Bestätigung (innerhalb von 4 s). */
  private sellAllButton(key: string, uids: number[] | undefined, label: string, testid: string): HTMLElement {
    const s = this.sim.s;
    const v = sellAllValue(this.sim, uids);
    const mini = s.paused && this.v.miniPaused;
    const armed = this.sellAllArmed === key && performance.now() - this.sellAllArmedAt < 4000;
    return h('button', {
      class: `btn sm danger${armed ? ' armed' : ''}`, 'data-testid': testid, disabled: v.count === 0 || mini,
      title: mini ? 'In der Mini-Pause kann nicht verkauft werden' : `${v.count} Türme verkaufen für +${v.gold} Gold (zweimal klicken)`,
      onclick: () => {
        if (this.sellAllArmed === key && performance.now() - this.sellAllArmedAt < 4000) {
          this.sellAllArmed = '';
          this.a.sellAll(uids);
        } else {
          this.sellAllArmed = key;
          this.sellAllArmedAt = performance.now();
        }
        this.towerWinSig = '';
        this.panelSig = '';
      },
    }, armed ? `Wirklich? +${v.gold} Gold` : `${label} (+${v.gold})`);
  }

  /** 3.1: ausgewählte Rune (Klick = Info), Doppelklick = sockeln bzw. herausnehmen. */
  private runeSel: string | null = null;
  private runeClick = { key: '', t: 0 };

  /** Klick zeigt die Wirkung; zweiter Klick auf dieselbe Rune innerhalb von 450 ms führt die Aktion aus (robust auch bei Neuaufbau). */
  private runeTap(e: MouseEvent, key: string, act: () => void): void {
    const now = e.timeStamp || performance.now();
    const dbl = this.runeClick.key === key && now - this.runeClick.t < 450;
    this.runeClick = dbl ? { key: '', t: 0 } : { key, t: now };
    if (dbl) {
      this.runeSel = null;
      act();
    } else {
      this.runeSel = key;
    }
    this.forcePanelRefresh();
  }

  private renderRuneSockets(body: HTMLElement, t: TowerInstance): void {
    const sim = this.sim;
    if (!runeEligible(t.def)) return;
    const cap = runeSockets(t.def, sim.s.wave);
    const inT = towerRunes(sim, t);
    const sortCount = sim.s.towers.filter((o) => o.def === t.def).length;
    const nextW = nextRuneSlotWave(sim.s.wave);
    const bag = bagRunes(sim);
    if (cap === 0 && !bag.length && sim.s.wave < RUNE_RULES.splinterFrom) return;
    let info: HTMLElement | null = null;
    const infoBox = (rune: (typeof bag)[number]['rune'], tier: number, inTower: boolean, act: () => void, extra = '') =>
      h('div', { class: 'rune-info', 'data-testid': 'rune-info', style: `--rc:${RUNES[rune].color}` },
        h('div', {}, h('span', { class: 'r-ic' }, RUNES[rune].icon), ' ', h('b', {}, runeLabel(rune, tier)), extra ? h('span', { class: 'muted' }, ` ${extra}`) : null),
        h('div', { class: 'small' }, runeEffectText(rune, tier)),
        h('div', { class: 'row tight' },
          h('span', { class: 'tiny muted' }, inTower ? 'Doppelklick: zurück in den Beutel' : 'Doppelklick: in diese Turmsorte sockeln'),
          h('button', { class: 'btn sm', 'data-testid': inTower ? 'rune-info-unsocket' : 'rune-info-socket', disabled: !inTower && inT.length >= cap, onclick: () => { this.runeSel = null; act(); this.forcePanelRefresh(); } }, inTower ? 'Herausnehmen' : 'Sockeln')));
    const box = h('div', { class: 'rune-box', 'data-testid': 'rune-box' },
      h('b', {}, `ᚱ Runen der Sorte ${TOWERS[t.def].name} ${inT.length}/${cap}`),
      h('div', { class: 'tiny muted', 'data-testid': 'rune-sort-note' }, `Gilt für alle ${sortCount} ${TOWERS[t.def].name}${TOWERS[t.def].legendary ? ' (Fusion)' : ''}. Wechsel wirkt sofort auf alle.${nextW ? ` Nächster Sockel ab Welle ${nextW}.` : ''}${TOWERS[t.def].legendary ? ' Legendär: +1 Sockel.' : ''}`),
      h('div', { class: 'rune-slots' }, Array.from({ length: cap }, (_, i) => {
        const r = inT[i];
        if (!r) return h('div', { class: 'rune empty', 'data-testid': `rune-slot-${i}` }, '+');
        const key = `s:${r.uid}`;
        const act = () => this.a.unsocketRune(r.uid);
        if (this.runeSel === key) info = infoBox(r.rune, r.tier, true, act);
        return h('button', { class: `rune on${this.runeSel === key ? ' sel' : ''}`, style: `--rc:${RUNES[r.rune].color}`, 'data-testid': `rune-slot-${i}`, title: `${runeLabel(r.rune, r.tier)} – Klick: Wirkung · Doppelklick: herausnehmen`, onclick: (e: MouseEvent) => this.runeTap(e, key, act) }, h('span', { class: 'r-ic' }, RUNES[r.rune].icon), h('span', { class: 'r-t' }, 'I'.repeat(r.tier)));
      })));
    if (bag.length) {
      // Beutel: gleiche Runen gleicher Qualität zusammenfassen
      const groups = new Map<string, typeof bag>();
      for (const r of bag) {
        const k = `${r.rune}:${r.tier}`;
        groups.set(k, [...(groups.get(k) ?? []), r]);
      }
      put(box, h('div', { class: 'tiny muted' }, inT.length < cap ? 'Beutel – Klick: Wirkung · Doppelklick: sockeln' : 'Beutel – Klick: Wirkung (alle Sockel belegt)'),
        h('div', { class: 'rune-bag' }, [...groups.values()].sort((a, b) => b[0].tier - a[0].tier || RUNE_ORDER.indexOf(a[0].rune) - RUNE_ORDER.indexOf(b[0].rune)).map((g) => {
          const r = g[0];
          const key = `b:${r.rune}:${r.tier}`;
          const act = () => this.a.socketRune(t.uid, r.uid);
          if (this.runeSel === key) info = infoBox(r.rune, r.tier, false, act, g.length > 1 ? `×${g.length}` : '');
          return h('button', { class: `rune${this.runeSel === key ? ' sel' : ''}`, style: `--rc:${RUNES[r.rune].color}`, 'data-testid': `rune-bag-${r.rune}-${r.tier}`, title: `${runeLabel(r.rune, r.tier)} – Klick: Wirkung · Doppelklick: sockeln`, onclick: (e: MouseEvent) => this.runeTap(e, key, act) },
            h('span', { class: 'r-ic' }, RUNES[r.rune].icon), h('span', { class: 'r-t' }, 'I'.repeat(r.tier)), g.length > 1 ? h('span', { class: 'r-n' }, `×${g.length}`) : null);
        })));
    } else if (sim.s.wave < RUNE_RULES.splinterFrom) {
      put(box, h('div', { class: 'tiny muted' }, `Runen sind nur findbar – Splitter ab Welle ${RUNE_RULES.splinterFrom}, volle Runen ab Welle ${RUNE_RULES.fromWave}.`));
    }
    if (info) put(box, info);
    put(body, box);
  }

  /** 3.6: Die wichtigste nächste Aktion direkt unter dem Kopf – ohne Scrollen. */
  private quickActions(body: HTMLElement, t: TowerInstance): void {
    const s = this.sim.s;
    const d = TOWERS[t.def];
    const next = nextUpgradeCost(t);
    if (next !== null) {
      const up = d.upgrades[t.level - 1];
      put(body, h('div', { class: 'quick' }, h('button', { class: 'btn primary wide', 'data-testid': 'quick-upgrade', disabled: s.gold < next, title: up.description, onclick: () => this.a.upgrade(t.uid) }, `▲ Stufe ${t.level + 1}: ${up.name} · ${fmt(next)} Gold`)));
      return;
    }
    if (d.specs.length === 2 && t.spec === 0 && !d.legendary) {
      put(body, h('div', { class: 'quick two' }, d.specs.map((sp, i) => {
        const c = specCost(t, (i + 1) as 1 | 2);
        return h('button', { class: 'btn primary', 'data-testid': `quick-spec-${i + 1}`, disabled: s.gold < c, title: sp.description, onclick: () => this.a.specialize(t.uid, (i + 1) as 1 | 2) }, `${i === 0 ? 'Ⓐ' : 'Ⓑ'} ${sp.name} · ${fmt(c)}`);
      })));
      return;
    }
    if (!s.rules.endless || d.attack === 'none') return;
    const n = t.stars ?? 0;
    if (n >= STARS.max) {
      put(body, h('div', { class: 'quick' }, h('div', { class: 'small star-max' }, `★${STARS.max} – höchste Veredelung erreicht`)));
      return;
    }
    const c = starCost(t);
    put(body, h('div', { class: 'quick' },
      h('button', { class: 'btn primary wide star-btn', 'data-testid': 'star-btn', disabled: s.gold < c, title: `Veredeln: ${starText(t.def)} je Stern (bis ★${STARS.max})`, onclick: () => this.a.star(t.uid) }, `★ Veredeln ★${n} → ★${n + 1} · ${fmt(c)} Gold`),
      h('div', { class: 'tiny muted' }, `Je Stern: ${starText(t.def)}`)));
  }

  private renderTower(t: TowerInstance): void {
    const sim = this.sim;
    const s = sim.s;
    const d = TOWERS[t.def];
    const st = sim.c.towerStats.get(t.uid);
    const body = this.panelBody;
    const specName = t.spec ? d.specs[t.spec - 1]?.name : null;
    put(body, h('div', { class: 'ph' }, iconImg(this.icons.tower(t.def, 96, t.level, t.spec), d.icon, 'big-ic'),
      h('div', {}, h('h3', {}, d.name, d.legendary ? h('span', { class: 'legend-tag' }, 'Legendär') : null), h('div', { class: 'muted small' }, d.upgrades.length ? `Stufe ${t.level}/4${specName ? ` · ${specName}` : ''}` : d.role, t.stars ? h('span', { class: 'star-tag', 'data-testid': 'tower-stars' }, ` ★${t.stars}`) : null)),
      this.closeBtn()));
    this.quickActions(body, t);
    if (this.v.groupDef === t.def) {
      const group = s.towers.filter((o) => o.def === t.def);
      const uids = group.map((o) => o.uid);
      const open = upgradableTowers(sim, uids);
      const cost = upgradeAllCost(sim, uids);
      const cheapest = Math.min(...open.map((o) => nextUpgradeCost(o) ?? Infinity));
      put(body, h('div', { class: 'group-box', 'data-testid': 'group-box' },
        h('b', {}, `▦ ${group.length}× ${d.name} ausgewählt`),
        h('div', { class: 'tiny muted' }, open.length ? `${open.length} davon noch aufwertbar · komplett ${fmt(cost.total)} Gold` : 'Alle auf der höchsten Stufe'),
        h('div', { class: 'row tight' },
          h('button', { class: 'btn primary sm', 'data-testid': 'group-upgrade', disabled: !open.length || cheapest > s.gold, title: 'Alle Türme dieser Sorte aufwerten, solange das Gold reicht (ohne Spezialisierung)', onclick: () => this.a.upgradeAll(uids) }, '▲ Alle aufwerten'),
          h('button', { class: 'btn sm', onclick: () => this.a.clearGroup() }, 'Nur diesen'),
          this.sellAllButton(`g:${t.def}`, uids, 'Alle verkaufen', 'group-sell')),
        s.rules.endless ? (() => {
          const st2 = group.map((o) => ({ o, c: starCost(o) })).filter((x) => fullyBuilt(x.o) && Number.isFinite(x.c));
          const total = st2.reduce((a, x) => a + x.c, 0);
          const cheapest = Math.min(...st2.map((x) => x.c));
          return h('button', { class: 'btn sm star-btn', 'data-testid': 'group-star', disabled: !st2.length || cheapest > s.gold, title: st2.length ? `Alle voll ausgebauten ${d.name} um einen Stern veredeln (zusammen ${fmt(total)} Gold) – bei zu wenig Gold so viele wie möglich. Je Stern: ${starText(t.def)}.` : 'Erst voll ausbauen (Stufe 4 und Spezialisierung)', onclick: () => this.a.starAll(uids) }, `★ Alle veredeln${st2.length ? ` (${fmt(total)})` : ''}`);
        })() : null,
        d.specs.length === 2 && !d.legendary ? h('div', { class: 'row tight group-spec' }, ([1, 2] as const).map((sp) => {
          const need = group.filter((o) => o.spec !== sp);
          const total = need.reduce((a, o) => a + specTargetCost(o, sp), 0);
          const cheapestSp = Math.min(...need.map((o) => specTargetCost(o, sp)));
          return h('button', {
            class: `btn sm${need.length ? '' : ' on'}`, 'data-testid': `group-spec-${sp}`, disabled: !need.length || cheapestSp > s.gold,
            title: need.length ? `Alle ${group.length} auf Höchststufe bringen und auf „${d.specs[sp - 1].name}“ stellen (Wechsel ${Math.round(RESPEC_MUL * 100)} % des Preises). Gesamt ${fmt(total)} Gold – bei zu wenig Gold so viele wie möglich.` : 'Alle bereits so spezialisiert',
            onclick: () => this.a.groupSpec(uids, sp),
          }, `${sp === 1 ? 'Ⓐ' : 'Ⓑ'} Alle: ${d.specs[sp - 1].name}${need.length ? ` (${fmt(total)})` : ' ✓'}`);
        })) : null));
    }
    if (s.rules.endless && d.attack !== 'economy' && d.attack !== 'none') {
      const lim = currentTowerLimit(sim);
      const n = countedTowers(sim);
      const fb = focusBonus(n, lim);
      if (Number.isFinite(lim)) put(body, h('p', { class: 'small', 'data-testid': 'focus-line', title: 'Jeder freie Bauplatz macht alle Türme stärker. Klick auf 🏰 in der Turmleiste erklärt das genauer.' },
        fb > 0 ? `✦ Fokusbonus: +${Math.round(fb * 100)} % Schaden, weil ${lim - n} Bauplätze frei sind (je freier Platz +${Math.round(TOWER_LIMIT.focusPer * 100)} %).` : `Fokusbonus: 0 % – alle ${lim} Bauplätze belegt. Freie Plätze geben je +${Math.round(TOWER_LIMIT.focusPer * 100)} % Schaden.`));
    }
    if (s.he) this.renderRuneSockets(body, t);
    if (s.he?.mastery && TOWERS[t.def].attack !== 'economy' && TOWERS[t.def].attack !== 'none') {
      const k = s.he.mastery[t.def] ?? 0;
      const r = masteryRank(k);
      const nx = masteryNext(k);
      put(body, h('p', { class: 'small mastery', 'data-testid': 'mastery' }, `🎖 Meisterschaft ${d.name}: Rang ${r}/${MASTERY.maxRank} (+${r} % Schaden) · ${fmt(k)}${nx ? ` / ${fmt(nx)}` : ''} Abschüsse`));
    }
    if (t.stunned > 0) put(body, h('p', { class: 'bad' }, `✷ Außer Gefecht (${fmtNum(t.stunned)} s)`));
    if (t.disrupted > 0) put(body, h('p', { class: 'bad' }, `✖ Netzwerk gestört (${fmtNum(t.disrupted)} s)`));
    // Fusion: für jedes passende Rezept direkt zeigen, was noch fehlt
    const fus = fusionOptions(sim, t);
    for (const r of FUSIONS.filter((x) => x.a === t.def || x.b === t.def)) {
      const ld = TOWERS[r.id];
      const otherDef = r.a === t.def ? r.b : r.a;
      const near = fus.find((f) => f.recipe.id === r.id)?.partner;
      const others = s.towers.filter((o) => o !== t && o.def === otherDef);
      const selfOk = t.level >= 4 && t.spec !== 0;
      const partnerOk = !!near && near.level >= 4 && near.spec !== 0;
      const goldOk = s.gold >= r.cost;
      const ok = (yes: boolean): string => (yes ? '✔' : '✖');
      let partnerText: string;
      if (near) partnerText = `${TOWERS[otherDef].name} in der Nähe (gold markiert) – ${near.level >= 4 ? 'Stufe 4 ✓' : `Stufe ${near.level}/4`}${near.spec ? ', spezialisiert ✓' : ', Spezialisierung fehlt'}`;
      else if (others.length) partnerText = `${TOWERS[otherDef].name} steht zu weit weg – höchstens ${fmtNum(FUSION_RANGE, 1)} Felder Abstand nötig`;
      else if (s.he && !towerUnlocked(otherDef, s.he, s.wave)) partnerText = `${TOWERS[otherDef].name} bauen (wird ab Welle ${towerUnlockWave(otherDef, s.he)} freigeschaltet), höchstens ${fmtNum(FUSION_RANGE, 1)} Felder entfernt`;
      else partnerText = `${TOWERS[otherDef].name} in höchstens ${fmtNum(FUSION_RANGE, 1)} Feldern Abstand bauen`;
      const ready = selfOk && partnerOk && goldOk;
      put(body, h('div', { class: `upg fusion${ready ? ' ready' : ''}`, 'data-testid': `fusion-${r.id}` },
        iconImg(this.icons.tower(r.id, 64), ld.icon, 'spec-ic'),
        h('div', {},
          h('b', {}, `✦ Fusion: ${ld.name}`),
          h('div', { class: 'tiny muted' }, ld.description),
          h('ul', { class: 'fusion-req' },
            h('li', { class: selfOk ? 'good' : 'bad' }, `${ok(selfOk)} Dieser Turm: ${t.level >= 4 ? 'Stufe 4 ✓' : `Stufe ${t.level}/4 – noch ${4 - t.level}× aufwerten`}${t.level >= 4 ? (t.spec ? ', spezialisiert ✓' : ', Spezialisierung fehlt') : ', danach spezialisieren'}`),
            h('li', { class: partnerOk ? 'good' : 'bad' }, `${ok(partnerOk)} Partner: ${partnerText}`),
            h('li', { class: goldOk ? 'good' : 'bad' }, `${ok(goldOk)} ${r.cost} Gold`))),
        near ? h('button', { class: 'btn primary', 'data-testid': 'fuse', disabled: !ready, onclick: () => this.a.fuse(t.uid, near.uid) }, 'Verschmelzen') : null));
    }
    // Aktive Fähigkeit
    if (d.active) {
      const a = TOWER_ACTIVES[d.active];
      const as = activeState(sim, t);
      put(body, h('div', { class: 'upg active-ab' },
        h('div', {}, h('b', {}, `⚡ ${a.name}`), h('div', { class: 'small' }, a.description), h('div', { class: 'tiny muted' }, `Abklingzeit ${a.cooldown} s · löst ab Stufe ${ACTIVE_MIN_LEVEL} automatisch aus, sobald Gegner in Reichweite sind`)),
        h('button', { class: `btn ${as.available ? 'primary' : ''}`, 'data-testid': 'tower-active', disabled: !as.available, title: as.reason ?? 'Auslösen (C)', onclick: () => this.a.towerActive(t.uid) }, as.available ? 'Auslösen (C)' : (as.reason ?? '–'))));
    }
    if (st) put(body, this.statTable(statLines(st, d)));
    // Netzwerk
    const gi = sim.c.linked.get(t.uid);
    if (gi !== undefined) {
      const g = s.network.groups[gi];
      const links = s.network.links.filter((l) => l.a === t.uid || l.b === t.uid).length;
      const res = (g?.resonances ?? []).map((id) => RESONANCES.find((r) => r.id === id)?.name ?? id);
      put(body, h('div', { class: 'net' },
        h('div', {}, `📡 Netzwerk: ${g?.members.length ?? 0} Türme, ${links} Verbindung(en) · Bonus +${pct(g?.bonus ?? 0)} Schaden & Tempo`),
        res.length ? h('div', { class: 'res' }, `✦ Resonanz aktiv: ${res.join(', ')}`) : null));
    } else if (d.networkable && t.def !== 'pylon') {
      put(body, h('p', { class: 'muted small' }, 'Nicht vernetzt – ein Leitpylon in Reichweite verbindet diesen Turm.'));
    }
    if (t.def === 'goldmine') {
      put(body, h('p', { class: 'small' }, `Bisher gefördert: ${fmt(t.goldEarned)} Gold. Investiert: ${fmt(t.invested)} Gold.${t.builtWave >= s.wave && s.phase === 'build' ? ' Zahlt ab Ende der nächsten Welle.' : ''}`));
    } else if (d.attack !== 'none') {
      put(body, h('p', { class: 'small muted' }, `Schaden gesamt: ${fmt(t.damageDealt)} · Abschüsse: ${t.kills}`));
    }
    // Zielwahl
    if (st && st.damage > 0 && d.attack !== 'pulse' && d.attack !== 'aura') {
      put(body, h('div', { class: 'prio' }, h('div', { class: 'small muted' }, 'Zielwahl (T):'),
        h('div', { class: 'seg' }, PRIORITY_ORDER.filter((p) => p !== 'random' || d.attack === 'rift').map((p) =>
          h('button', { class: `btn sm${t.priority === p ? ' on' : ''}`, 'aria-pressed': String(t.priority === p), onclick: () => this.a.setPriority(t.uid, p) }, PRIORITY_NAMES[p])))));
    }
    // Upgrades
    const next = nextUpgradeCost(t);
    if (next !== null) {
      const up = d.upgrades[t.level - 1];
      put(body, h('div', { class: 'upg' },
        h('div', {}, h('b', {}, `Stufe ${t.level + 1}: ${up.name}`), h('div', { class: 'small' }, up.description)),
        h('button', { class: 'btn primary', 'data-testid': 'upgrade', disabled: s.gold < next, onclick: () => this.a.upgrade(t.uid) }, `▲ ${next} Gold (U)`)));
    } else if (d.specs.length === 2 && t.spec === 0 && !d.legendary) {
      put(body, h('div', { class: 'small muted' }, 'Spezialisierung wählen (später gegen Aufpreis wechselbar):'));
      d.specs.forEach((sp, i) => {
        const c = specCost(t, (i + 1) as 1 | 2);
        put(body, h('div', { class: 'upg spec' },
          iconImg(this.icons.tower(t.def, 64, 4, (i + 1) as 1 | 2), '', 'spec-ic'),
          h('div', {}, h('b', {}, `${i === 0 ? 'Ⓐ' : 'Ⓑ'} ${sp.name}`), h('div', { class: 'small' }, sp.description)),
          h('button', { class: 'btn primary', 'data-testid': `spec-${i + 1}`, disabled: s.gold < c, onclick: () => this.a.specialize(t.uid, (i + 1) as 1 | 2) }, `${c} Gold`)));
      });
    } else if (t.spec) {
      put(body, h('p', { class: 'small' }, `Spezialisiert: ${d.specs[t.spec - 1].name} – ${d.specs[t.spec - 1].description}`));
      if (d.specs.length === 2 && !d.legendary) {
        const other = (t.spec === 1 ? 2 : 1) as 1 | 2;
        const c = respecCost(t, other);
        put(body, h('div', { class: 'upg spec' },
          iconImg(this.icons.tower(t.def, 64, 4, other), '', 'spec-ic'),
          h('div', {}, h('b', {}, `⇄ Wechseln zu ${other === 1 ? 'Ⓐ' : 'Ⓑ'} ${d.specs[other - 1].name}`), h('div', { class: 'small' }, `${d.specs[other - 1].description} (Wechsel kostet ${Math.round(RESPEC_MUL * 100)} % des Preises)`)),
          h('button', { class: 'btn', 'data-testid': 'respec', disabled: s.gold < c, onclick: () => this.a.respec(t.uid, other) }, `${c} Gold`)));
      }
    }
    if (d.legendary) {
      put(body, h('button', { class: 'btn wide', 'data-testid': 'move-tower', title: 'Kostenlos auf ein anderes freies Feld versetzen – danach 3 s außer Gefecht', onclick: () => this.a.startMove(t.uid) }, '⇄ Versetzen'));
    }
    // Verkauf
    const refund = refundValue(sim, t);
    const full = refund >= t.invested;
    const armed = this.sellArmed === t.uid;
    const noSell = s.paused && this.v.miniPaused;
    put(body, h('button', {
      class: `btn wide ${armed ? 'danger' : ''}`, 'data-testid': 'sell', disabled: noSell,
      title: noSell ? 'In der Mini-Pause kann nicht verkauft werden' : '',
      onclick: () => {
        if (armed) {
          this.sellArmed = 0;
          this.a.sell(t.uid);
        } else {
          this.sellArmed = t.uid;
          this.panelSig = '';
        }
      },
    }, noSell ? 'Verkaufen in der Mini-Pause gesperrt' : armed ? `Wirklich verkaufen? +${refund} Gold` : `Verkaufen: +${refund} Gold${full ? ' (volle Erstattung)' : ' (70 %)'}`));
    put(body, h('details', {}, h('summary', {}, 'Synergien & Konter'), h('p', { class: 'small' }, '✚ ', d.synergies), h('p', { class: 'small' }, '✖ ', d.counters), this.matchupLines(d)));
  }

  disarmSell(): void {
    if (this.sellArmed) {
      this.sellArmed = 0;
      this.panelSig = '';
    }
  }

  private heroHeader(id: HeroId, extra: HTMLElement | null): HTMLElement {
    const def = HEROES[id];
    return h('div', { class: 'ph' }, iconImg(this.icons.hero(id, 96), def.icon, 'big-ic'),
      h('div', {}, h('h3', {}, def.name), h('div', { class: 'muted small' }, `${def.title} · ${def.role}`)), extra);
  }

  private renderPlacing(id: HeroId): void {
    const def = HEROES[id];
    const s = this.sim.s;
    put(this.panelBody,
      this.heroHeader(id, this.closeBtn()),
      h('p', { class: 'hint' }, 'Tippe eine Wegzelle an, um den Helden dort abzusetzen. Er hält diese Stelle, bis du ihm einen neuen Punkt zuweist.'),
      s.riftEnergy < def.cost ? h('p', { class: 'bad' }, `Es fehlen ${Math.ceil(def.cost - s.riftEnergy)} Rissenergie.`) : null,
      h('p', {}, def.description),
    );
  }

  private renderHero(hero: HeroInstance): void {
    const def = HEROES[hero.id];
    const st = heroStatsOf(this.sim, hero);
    const body = this.panelBody;
    const nextXp = HERO_RULES.xpPerLevel[hero.level] ?? null;
    put(body, this.heroHeader(hero.id, this.closeBtn()),
      h('div', { class: 'xp' }, h('span', {}, `Stufe ${hero.level}/${HERO_RULES.maxLevel}`),
        h('div', { class: 'hb' }, h('div', { class: 'hb-fill xp', style: `width:${nextXp ? Math.min(100, (100 * hero.xp) / nextXp) : 100}%` })),
        h('span', { class: 'tiny muted' }, nextXp ? `${Math.floor(hero.xp)}/${nextXp} EP` : 'max.')));
    if (hero.pendingPerk) {
      const idx = hero.perks.length;
      const pair = def.perks[idx];
      if (pair) {
        put(body, h('div', { class: 'perk-choice', 'data-testid': 'perk-choice' }, h('b', {}, `★ Stufe ${idx + 2}: Wähle eine Verbesserung`),
          h('div', { class: 'perk-opts' }, pair.map((p, i) => h('button', { class: 'card perk', 'data-testid': `perk-${i}`, onclick: () => this.a.heroPerk(hero.id, i as 0 | 1) },
            h('b', {}, p.name), h('div', { class: 'small' }, p.description))))));
      }
    }
    if (hero.state === 'active') {
      put(body, h('div', { class: 'small muted' }, 'Verhalten:'),
        h('div', { class: 'seg' }, (['hold', 'advance', 'retreat'] as HeroMode[]).map((m) =>
          h('button', { class: `btn sm${hero.mode === m ? ' on' : ''}`, 'aria-pressed': String(hero.mode === m), onclick: () => this.a.heroMode(hero.id, m) }, svgIcon(m), MODE_LABEL[m]))),
        h('p', { class: 'tiny muted' }, 'Halten: bleibt am Ankerpunkt (Wegzelle antippen versetzt ihn). Vorrücken: greift nahe Gegner an. Rückzug: kehrt zum Kern zurück und erhält einen Teil der Abklingzeit erstattet.'));
    }
    const rows: [string, string][] = [];
    if (st.damage > 0) rows.push(['Schaden', `${fmtNum(st.damage)} (${DAMAGE_NAMES[def.damageType]})`]);
    rows.push(['Reichweite', `${fmtNum(st.range)} Felder`]);
    rows.push(['Lebenspunkte', `${fmt(hero.hp)} / ${fmt(st.maxHp)}`]);
    if (st.armor > 0) rows.push(['Rüstung', pct(st.armor)]);
    if (st.blockCount > 0) rows.push(['Hält fest', `${Math.round(st.blockCount)} Gegner`]);
    if (st.auraRadius > 0) rows.push(['Aura', `${fmtNum(st.auraRadius)} Felder · ${pct(st.auraPower)}`]);
    if (st.goldBonus > 0) rows.push(['Goldbonus', pct(st.goldBonus)]);
    rows.push(['Einsatzzeit', `${Math.ceil(hero.activeLeft)} / ${Math.round(st.activeTime)} s`]);
    rows.push(['Abschüsse', String(hero.kills)]);
    put(body, this.statTable(rows));
    const keys = ['A', 'S', 'D'];
    ([0, 1, 2] as const).forEach((slot) => {
      const ab = slot === 2 ? def.ultimate : def.abilities[slot];
      const r = abilityReady(this.sim, hero, slot);
      put(body, h('div', { class: `upg${slot === 2 ? ' spec' : ''}` },
        h('div', {}, h('b', {}, `${keys[slot]} · ${ab.name}${slot === 2 ? ' (Ultimate)' : ''}`), h('div', { class: 'small' }, ab.description)),
        h('button', { class: `btn ${r.ok ? 'primary' : ''}`, disabled: !r.ok, title: r.reason ?? '', onclick: () => this.a.heroAbility(hero.id, slot) }, r.ok ? 'Wirken' : (r.reason ?? '–'))));
    });
    const duos = activeDuos(this.sim);
    const myDuos = DUOS.filter((d) => d.a === hero.id || d.b === hero.id);
    if (myDuos.length) {
      put(body, h('div', { class: 'small' }, h('b', {}, 'Duo-Synergien: '),
        myDuos.map((d) => h('div', { class: duos.includes(d.id) ? 'good' : 'muted' }, `${duos.includes(d.id) ? '✔' : '○'} ${d.name} (mit ${HEROES[d.a === hero.id ? d.b : d.a].name.split(' ').slice(-1)[0]}): ${d.description}`))));
    }
    if (hero.perks.length) {
      put(body, h('p', { class: 'small muted' }, 'Gewählt: ', hero.perks.map((c, i) => def.perks[i]?.[c]?.name ?? '?').join(', ')));
    }
  }

  private renderEnemy(id: EnemyId): void {
    const body = this.panelBody;
    if (id === 'boss' && this.bossInfo) {
      const b = BOSSES[this.bossInfo];
      put(body, h('div', { class: 'ph' }, iconImg(this.icons.boss(this.bossInfo, 96), '👑', 'big-ic'), h('div', {}, h('h3', {}, b.name), h('div', { class: 'muted small' }, b.title)), this.closeBtn()),
        h('p', {}, b.description), h('p', { class: 'small' }, '🛡 Konter: ', b.counter),
        h('ul', { class: 'small' }, b.phases.map((p) => h('li', {}, `${p.name}${p.abilities.length ? `: ${p.abilities.map((a) => ABILITIES[a].name).join(', ')}` : ''}`))),
        this.resistLine(b.resist));
      return;
    }
    if (id === 'boss') return;
    const d = ENEMIES[id];
    put(body,
      h('div', { class: 'ph' }, iconImg(this.icons.enemy(id, 96), d.icon, 'big-ic'), h('div', {}, h('h3', {}, d.name), h('div', { class: 'muted small' }, d.role)), this.closeBtn()),
      h('p', {}, d.description),
      this.statTable([['Lebenspunkte (Basis)', fmt(d.hp)], ['Tempo', `${fmtNum(d.speed)} Felder/s`], ['Kopfgeld', `${d.reward} Gold`], ['Kernschaden', String(d.leak)], ['Fliegt', d.flying ? 'ja' : 'nein'],
        ...(d.stealth ? [['Getarnt', 'nur durch Detektoren sichtbar'] as [string, string]] : []),
        ...(d.burrow ? [['Gräbt sich ein', 'zeitweise unangreifbar'] as [string, string]] : [])]),
      this.resistLine(d.resist),
      d.abilities.length ? h('p', { class: 'small' }, 'Fähigkeiten: ', d.abilities.map((a) => `${ABILITIES[a].name} – ${ABILITIES[a].description}`).join(' ')) : null,
      h('p', { class: 'small' }, '🛡 Konter: ', d.counter),
    );
  }

  /** Hindernis im Helden-Endlos: abbauen (dauert eine Welle, nicht unterbrechbar, zufällige Ausbeute). */
  private renderObstacle(idx: number): void {
    const s = this.sim.s;
    const he = s.he;
    if (!he) return;
    const w = s.map.width;
    const kind = decorKindAt(this.sim, idx);
    const names: Record<number, string> = { 0: 'Baum', 1: 'Fels', 2: 'Kristallgruppe', 3: 'Lavafeld', 4: 'Eisblock', 5: 'Ruine', 6: 'Riesenpilz', 7: 'Wolke', 8: 'Schilf', 9: 'Leuchtpilz' };
    const chk = mineState(this.sim, idx % w, Math.floor(idx / w));
    const busy = he.mining;
    const dur = mineDuration(this.sim);
    const close = h('button', { class: 'btn icon sm close', 'aria-label': 'Schließen', onclick: () => { this.v.selectedObstacle = -1; this.panelSig = ''; } }, svgIcon('close'));
    put(this.panelBody,
      h('div', { class: 'ph' }, h('div', { class: 'big-ic fallback' }, kind === 0 ? '🌳' : kind === 2 ? '💎' : kind === 6 || kind === 9 ? '🍄' : '🪨'),
        h('div', {}, h('h3', {}, kind !== null ? names[kind] ?? 'Hindernis' : 'Freies Feld'), h('div', { class: 'muted small' }, 'Hindernis – mit dem Helden abbaubar')), close),
      busy && busy.cell === idx
        ? h('div', {}, h('p', {}, `⛏ Wird abgebaut … noch ${Math.ceil(busy.t)} s`), h('div', { class: 'bar poise' }, h('div', { class: 'bar-fill', style: `width:${100 * (1 - busy.t / busy.total)}%` })))
        : h('div', {},
          h('p', { class: 'small' }, `Abbau dauert ungefähr eine Welle (≈ ${dur} s) und kann nicht unterbrochen werden: Der Held steht still, greift nicht an und kann in dieser Zeit nicht fallen.`),
          h('p', { class: 'small' }, `Ausbeute (zufällig): ${HERO_ENDLESS.mineGoldBase + HERO_ENDLESS.mineGoldPerWave * Math.max(1, s.wave)} Gold (±50 %), meist Metall, oft Kristall${kind === 2 || kind === 4 ? ' (hier besonders viel)' : ''}, selten Essenz oder ein Relikt. Danach ist das Feld frei und bebaubar.`),
          h('button', { class: 'btn primary wide', 'data-testid': 'mine', disabled: !chk.ok, title: chk.reason ?? '', onclick: () => this.a.mine(idx) }, chk.ok ? `⛏ Abbauen (≈ ${dur} s)` : chk.reason ?? 'Nicht möglich')));
  }

  /** Live-Werte eines angeklickten Gegners: LP, Schild, Tempo, Resistenzen, Effekte, Fähigkeiten. */
  private renderEnemyInstance(e: EnemyInstance): void {
    const body = this.panelBody;
    const sim = this.sim;
    const boss = e.boss ? BOSSES[e.boss] : null;
    const d = ENEMIES[e.def];
    const name = boss ? boss.name : d.name;
    const tags: string[] = [];
    if (e.miniBoss) tags.push('Mini-Boss');
    else if (e.elite) tags.push('Elite');
    if (boss) tags.push(`Boss · Phase ${e.phase + 1}${boss.phases[e.phase] ? `: ${boss.phases[e.phase].name}` : ''}`);
    if (e.mutation) tags.push(`${MUTATIONS[e.mutation].icon} ${MUTATIONS[e.mutation].name}`);
    if (e.enraged) tags.push('Raserei');
    const img = e.boss ? this.icons.boss(e.boss, 96) : e.def !== 'boss' ? this.icons.enemy(e.def, 96) : null;
    const slow = slowFactor(e);
    const speed = e.speed * (1 - slow);
    const types: DamageType[] = ['physical', 'fire', 'frost', 'lightning', 'poison', 'arcane', 'sonic'];
    const res = types.map((k) => [k, resistOf(sim, e, k)] as const).filter(([, v]) => Math.abs(v) > 0.001);
    const close = h('button', { class: 'btn icon sm close', 'aria-label': 'Schließen', title: 'Schließen (Esc)', onclick: () => { this.a.selectEnemy(0); this.panelSig = ''; } }, svgIcon('close'));
    const hpPct = Math.max(0, e.hp / Math.max(1, e.maxHp));
    put(body,
      h('div', { class: 'ph' }, iconImg(img, boss ? '👑' : d.icon, 'big-ic'), h('div', {}, h('h3', {}, name), h('div', { class: 'muted small' }, tags.join(' · ') || (boss ? boss.title : d.role))), close),
      h('div', { class: 'enemy-hp', 'data-testid': 'enemy-hp' },
        h('div', { class: 'bar hp' }, h('div', { class: 'bar-fill', style: `width:${hpPct * 100}%` }), h('span', { class: 'bar-label' }, `${fmt(Math.ceil(e.hp))} / ${fmt(e.maxHp)} LP`)),
        e.maxShield > 0 ? h('div', { class: 'bar poise' }, h('div', { class: 'bar-fill', style: `width:${(100 * e.shield) / e.maxShield}%` }), h('span', { class: 'bar-label' }, `Schild ${fmt(e.shield)} / ${fmt(e.maxShield)}`)) : null),
      this.statTable([
        ['Tempo', `${fmtNum(speed, 2)} Felder/s${slow > 0.005 ? ` (−${Math.round(slow * 100)} % verlangsamt)` : ''}`],
        ['Kernschaden bei Durchbruch', `${Math.max(1, Math.round(e.leak))} ♥`],
        ['Kopfgeld', `${Math.round(e.reward)} Gold`],
        ...(sim.s.he ? [['Schaden am Helden', `${fmtNum(enemyContactDps(e) * (e.miniBoss ? 2 : 1) * (1 + 0.05 * sim.s.wave), 0)}/s bei Kontakt`] as [string, string]] : []),
        ['Bewegung', e.flying ? 'fliegt' : e.burrowed ? 'eingegraben (unangreifbar)' : 'Boden'],
        ...(e.stealthed ? [['Tarnung', 'getarnt – nur Detektoren sehen ihn'] as [string, string]] : []),
        ...(e.plates.length ? [['Schwachpunkte', e.plates.map((p) => (p > 0 ? fmt(p) : '✖')).join(' · ')] as [string, string]] : []),
      ]),
      h('p', { class: 'small' }, 'Resistenzen: ', res.length ? res.map(([k, v]) => h('span', { class: v > 0 ? 'res-up' : 'res-down' }, `${DAMAGE_NAMES[k]} ${v > 0 ? '−' : '+'}${Math.round(Math.abs(v) * 100)} % `)) : 'keine'),
      h('h4', {}, 'Effekte'),
      e.statuses.length
        ? h('ul', { class: 'small status-list', 'data-testid': 'enemy-statuses' }, e.statuses.map((x) => {
          const sd = STATUS_DEFS[x.id];
          return h('li', {}, `${sd.icon} ${sd.name}${x.stacks > 1 ? ` ×${x.stacks}` : ''} – ${fmtNum(x.t, 1)} s · `, h('span', { class: 'muted' }, sd.description));
        }))
        : h('p', { class: 'small muted' }, 'Keine Effekte aktiv.'),
      e.abilities.length ? h('p', { class: 'small' }, 'Fähigkeiten: ', e.abilities.map((a) => `${ABILITIES[a.id].name}${a.windup > 0 ? ' (wird gewirkt!)' : a.cd > 0 ? ` (${Math.ceil(a.cd)} s)` : ''}`).join(', ')) : null,
      e.mutation ? h('p', { class: 'small' }, `${MUTATIONS[e.mutation].icon} ${MUTATIONS[e.mutation].description}`) : null,
      h('p', { class: 'small' }, '🛡 Konter: ', boss ? boss.counter : d.counter),
    );
  }

  // ------------------------------------------------------------ Turmübersicht

  toggleTowerList(force?: boolean): void {
    this.towerListOpen = force ?? !this.towerListOpen;
    this.towerWinSig = '';
  }

  /** Live-Aktualisierungen der Turmübersicht (Texte, Sperren) ohne Neuaufbau der Knöpfe. */
  private tlLive: (() => void)[] = [];

  private updateTowerList(): void {
    toggleClass(this.towerWin, 'hidden', !this.towerListOpen);
    if (!this.towerListOpen) return;
    const s = this.sim.s;
    // Nur strukturelle Änderungen bauen die Liste neu auf. Gold, Abschüsse, Schaden und Abklingzeiten werden
    // live in die bestehenden Elemente geschrieben – sonst würden Knöpfe in jedem Bild ersetzt und Klicks gingen verloren.
    const pairs = fusionPairs(this.sim);
    const sig = `${this.towerSort}|${this.hideMaxed}|${this.sellAllArmed}|${s.paused && this.v.miniPaused}|${s.phase}|${s.towers.map((t) => `${t.uid}:${t.def}:${t.level}:${t.spec}:${t.stars ?? 0}:${t.cx},${t.cy}`).join(',')}|${this.v.selectedTower}|${pairs.map((p) => `${p.a.uid}-${p.b.uid}-${p.ready}`).join(',')}`;
    if (sig === this.towerWinSig || (this.pointerHeld && this.towerWinSig !== '')) {
      for (const f of this.tlLive) f();
      return;
    }
    this.towerWinSig = sig;
    this.tlLive = [];
    const live = (f: () => void): void => { this.tlLive.push(f); f(); };
    // Scrollposition über das Neuzeichnen hinweg erhalten
    const prevScroll = this.towerWin.querySelector('.tl-rows')?.scrollTop ?? 0;
    clear(this.towerWin);
    // Türme mit Fusionspartner gelten nie als „ausgebaut“ – sie sollen sichtbar bleiben
    const inPair = new Set(pairs.flatMap((p) => [p.a.uid, p.b.uid]));
    const endless = s.rules.endless;
    const isMaxed = (t: TowerInstance): boolean => !inPair.has(t.uid) && nextUpgradeCost(t) === null && !(TOWERS[t.def].specs.length === 2 && t.spec === 0 && !TOWERS[t.def].legendary) && (!endless || TOWERS[t.def].attack === 'none' || (t.stars ?? 0) >= STARS.max);
    const maxedCount = s.towers.filter(isMaxed).length;
    const list = s.towers.filter((t) => !this.hideMaxed || !isMaxed(t) || t.uid === this.v.selectedTower);
    if (this.towerSort === 'damage') list.sort((a, b) => b.damageDealt - a.damageDealt);
    else if (this.towerSort === 'level') list.sort((a, b) => b.level - a.level || a.uid - b.uid);
    else list.sort((a, b) => BUILD_ORDER.indexOf(a.def) - BUILD_ORDER.indexOf(b.def) || a.uid - b.uid);
    const sorter = (id: typeof this.towerSort, name: string) => h('button', { class: `btn sm${this.towerSort === id ? ' on' : ''}`, onclick: () => { this.towerSort = id; this.towerWinSig = ''; } }, name);
    const upAll = h('button', { class: 'btn sm primary', 'data-testid': 'tl-upgrade-all', title: endless ? 'Alle Türme aufwerten und danach veredeln (★), solange das Gold reicht – günstigste Schritte zuerst (Spezialisierung wählst du selbst)' : 'Alle Türme bis zur höchsten Stufe aufwerten, solange das Gold reicht (ohne Spezialisierung)', onclick: () => this.a.upgradeAll() }) as HTMLButtonElement;
    live(() => {
      const c = upgradeAllCost(this.sim);
      const starC = endless ? s.towers.filter(fullyBuilt).map((t) => starCost(t)).filter(Number.isFinite) : [];
      const cheapest = Math.min(...upgradableTowers(this.sim).map((t) => nextUpgradeCost(t) ?? Infinity), ...starC);
      upAll.disabled = (c.steps === 0 && !starC.length) || cheapest > s.gold;
      setText(upAll, c.steps ? `▲ Alle (${fmt(c.total)})` : starC.length ? `★ Alle veredeln (ab ${fmt(Math.min(...starC))})` : '▲ Alle');
    });
    const sellAll = this.sellAllButton('all', undefined, 'Alle verkaufen', 'tl-sell-all') as HTMLButtonElement;
    live(() => {
      const v = sellAllValue(this.sim);
      const armed = this.sellAllArmed === 'all' && performance.now() - this.sellAllArmedAt < 4000;
      setText(sellAll, armed ? `Wirklich? +${v.gold} Gold` : `Alle verkaufen (+${v.gold})`);
      toggleClass(sellAll, 'armed', armed);
    });
    put(this.towerWin,
      h('div', { class: 'he-win-head' }, h('h2', {}, `Türme (${s.towers.length})`),
        h('div', { class: 'seg' }, sorter('type', 'Typ'), sorter('level', 'Stufe'), sorter('damage', 'Schaden')),
        upAll,
        sellAll,
        h('label', { class: 'check tiny' }, h('input', { type: 'checkbox', 'data-testid': 'tl-hide-maxed', checked: this.hideMaxed, onchange: (e: Event) => { this.hideMaxed = (e.target as HTMLInputElement).checked; this.towerWinSig = ''; } }), ` Ausgebaute ausblenden (${maxedCount})`),
        h('button', { class: 'btn icon sm', 'aria-label': 'Schließen', title: 'Schließen (O/Esc)', onclick: () => this.toggleTowerList(false) }, svgIcon('close'))),
      pairs.length ? h('div', { class: 'tl-fusions', 'data-testid': 'tl-fusions' },
        h('b', { class: 'small' }, `✦ Fusionen (${pairs.filter((p) => p.ready).length}/${pairs.length} bereit)`),
        pairs.map((p) => {
          const ld = TOWERS[p.recipe.id];
          const miss: string[] = [];
          for (const x of [p.a, p.b]) {
            if (x.level < 4) miss.push(`${TOWERS[x.def].name} Stufe ${x.level}/4`);
            else if (!x.spec) miss.push(`${TOWERS[x.def].name} Spezialisierung fehlt`);
          }
          const btn = h('button', {
            class: 'btn sm primary', 'data-testid': `tl-fuse-${p.a.uid}-${p.b.uid}`,
            title: `${ld.name} entsteht auf dem Feld näher an der Lane (${TOWERS[p.target.def].name})`,
            onclick: () => this.a.fuse(p.a.uid, p.b.uid),
          }, `Verschmelzen (${p.recipe.cost})`) as HTMLButtonElement;
          if (p.ready) this.tlLive.push(() => { btn.disabled = s.gold < p.recipe.cost; });
          btn.disabled = !p.ready || s.gold < p.recipe.cost;
          return h('div', { class: `tl-fusion${p.ready ? ' ready' : ''}` },
            iconImg(this.icons.tower(p.a.def, 32, p.a.level, p.a.spec), '', 'tl-fic'), h('span', {}, '+'),
            iconImg(this.icons.tower(p.b.def, 32, p.b.level, p.b.spec), '', 'tl-fic'), h('span', {}, '→'),
            iconImg(this.icons.tower(p.recipe.id, 32), ld.icon, 'tl-fic'),
            h('div', { class: 'tl-main' }, h('b', {}, ld.name), h('div', { class: 'tiny muted' }, p.ready ? 'Bereit – entsteht in Richtung Lane' : `Fehlt: ${miss.join(', ')}`)),
            p.ready ? btn : h('button', { class: 'btn sm', title: 'Turm anzeigen', onclick: () => { const x = p.a.level < 4 || !p.a.spec ? p.a : p.b; this.a.selectTower(x.uid); this.a.lookAt(x.cx + 0.5, x.cy + 0.5); } }, 'Zeigen'));
        })) : null,
      s.towers.length ? (() => {
        // 3.6: je Turmsorte ein Knopf – alle dieser Sorte aufwerten bzw. veredeln, ohne durch die Liste zu scrollen
        const defs = [...new Set(s.towers.map((t) => t.def))].filter((d) => TOWERS[d].attack !== 'none').sort((a, b) => BUILD_ORDER.indexOf(a) - BUILD_ORDER.indexOf(b) || LEGENDARY_ORDER.indexOf(a) - LEGENDARY_ORDER.indexOf(b));
        return h('div', { class: 'tl-types', 'data-testid': 'tl-types' }, defs.map((def) => {
          const group = s.towers.filter((t) => t.def === def);
          const uids = group.map((t) => t.uid);
          const d = TOWERS[def];
          const lv = upgradableTowers(this.sim, uids).map((t) => nextUpgradeCost(t) ?? Infinity);
          const stc = endless ? group.filter(fullyBuilt).map((t) => starCost(t)).filter(Number.isFinite) : [];
          const needSpec = group.filter((t) => nextUpgradeCost(t) === null && d.specs.length === 2 && t.spec === 0 && !d.legendary).length;
          const minStars = Math.min(...group.map((t) => t.stars ?? 0));
          const cheapest = Math.min(...lv, ...stc);
          const label = lv.length ? `▲ ${fmt(Math.min(...lv))}` : stc.length ? `★ ${fmt(Math.min(...stc))}` : needSpec ? 'Spezial.' : '✓';
          const b = h('button', {
            class: `btn sm${lv.length ? ' primary' : stc.length ? ' star-btn' : ''}`, 'data-testid': `tl-type-${def}`,
            disabled: !lv.length && !stc.length && !needSpec,
            title: lv.length ? `Alle ${group.length} ${d.name} aufwerten (danach veredeln), solange das Gold reicht` : stc.length ? `Alle ${group.length} ${d.name} veredeln, solange das Gold reicht – je Stern ${starText(def)}` : needSpec ? 'Spezialisierung wählen – öffnet die Gruppenauswahl' : 'Alles erledigt',
            onclick: () => {
              if (!lv.length && !stc.length && needSpec) { const x = group.find((t) => t.spec === 0)!; this.a.selectTower(x.uid); this.a.lookAt(x.cx + 0.5, x.cy + 0.5); return; }
              this.a.upgradeAll(uids);
            },
          }, label) as HTMLButtonElement;
          if (lv.length || stc.length) this.tlLive.push(() => { b.disabled = cheapest > s.gold; });
          if (lv.length || stc.length) b.disabled = cheapest > s.gold;
          return h('div', { class: 'tl-type' }, iconImg(this.icons.tower(def, 32, 4, group[0].spec), d.icon, 'tl-fic'),
            h('div', { class: 'tl-main' }, h('b', { class: 'small' }, `${group.length}× ${d.name}`), h('div', { class: 'tiny muted' }, endless ? `★${minStars}${needSpec ? ` · ${needSpec} ohne Spez.` : ''}` : `${lv.length} aufwertbar`)),
            b);
        }));
      })() : null,
      list.length ? h('div', { class: 'he-win-body tl-rows' }, list.map((t) => {
        const d = TOWERS[t.def];
        const next = nextUpgradeCost(t);
        const needSpec = next === null && d.specs.length === 2 && t.spec === 0 && !d.legendary;
        const specName = t.spec ? d.specs[t.spec - 1]?.name : '';
        const head = h('span', { class: 'muted small' });
        const info = h('div', { class: 'tiny muted' });
        live(() => {
          const st = this.sim.c.towerStats.get(t.uid);
          setText(head, ` St. ${t.level}${specName ? ` · ${specName}` : ''}${t.stars ? ` · ★${t.stars}` : ''}${t.stunned > 0 ? ' · ✷ außer Gefecht' : ''}`);
          setText(info, `${st ? `Schaden ${fmtNum(st.damage, 1)} · Reichweite ${fmtNum(st.range, 1)} · ` : ''}Abschüsse ${t.kills} · Gesamtschaden ${fmt(t.damageDealt)}`);
        });
        let up: HTMLElement;
        if (next !== null) {
          const b = h('button', { class: 'btn sm primary', 'data-testid': `tl-up-${t.uid}`, title: `${d.upgrades[t.level - 1].name}: ${d.upgrades[t.level - 1].description}`, onclick: () => this.a.upgrade(t.uid) }, `▲ ${next}`) as HTMLButtonElement;
          live(() => { b.disabled = s.gold < next; });
          up = b;
        } else if (needSpec) {
          up = h('button', { class: 'btn sm', 'data-testid': `tl-spec-${t.uid}`, title: 'Spezialisierung wählen – öffnet die Turmdetails', onclick: () => { this.a.selectTower(t.uid); this.a.lookAt(t.cx + 0.5, t.cy + 0.5); } }, 'Spezial.');
        } else if (endless && d.attack !== 'none' && (t.stars ?? 0) < STARS.max) {
          const c = starCost(t);
          const b = h('button', { class: 'btn sm star-btn', 'data-testid': `tl-star-${t.uid}`, title: `Veredeln ★${(t.stars ?? 0) + 1}: ${starText(t.def)}`, onclick: () => this.a.star(t.uid) }, `★ ${fmt(c)}`) as HTMLButtonElement;
          live(() => { b.disabled = s.gold < c; });
          up = b;
        } else up = h('span', { class: 'tiny muted' }, endless ? `★${STARS.max}` : 'max.');
        let actBtn: HTMLElement | null = null;
        if (d.active) {
          const b = h('button', { class: 'btn sm', onclick: () => this.a.towerActive(t.uid) }, '⚡') as HTMLButtonElement;
          live(() => {
            const act = activeState(this.sim, t);
            b.disabled = !act.available;
            const title = act.reason ?? `${TOWER_ACTIVES[d.active!].name} auslösen`;
            if (b.title !== title) b.title = title;
          });
          actBtn = b;
        }
        return h('div', { class: `tl-row${this.v.selectedTower === t.uid ? ' on' : ''}`, 'data-testid': `tl-${t.uid}` },
          iconImg(this.icons.tower(t.def, 48, t.level, t.spec), d.icon, 'tl-ic'),
          h('div', { class: 'tl-main' }, h('b', {}, d.name), head, info),
          h('div', { class: 'tl-btns' }, up, actBtn,
            h('button', { class: 'btn sm', 'data-testid': `tl-info-${t.uid}`, title: 'Details öffnen und Kamera hinbewegen', onclick: () => { this.a.selectTower(t.uid); this.a.lookAt(t.cx + 0.5, t.cy + 0.5); } }, 'Info')));
      })) : h('p', { class: 'muted' }, s.towers.length ? 'Alle Türme sind voll ausgebaut.' : 'Noch keine Türme gebaut.'));
    const rows = this.towerWin.querySelector('.tl-rows');
    if (rows) rows.scrollTop = prevScroll;
  }

  private resistLine(r: Partial<Record<string, number>>): HTMLElement {
    const parts = Object.entries(r).filter(([, v]) => v).map(([k, v]) => `${DAMAGE_NAMES[k as keyof typeof DAMAGE_NAMES]} ${(v as number) > 0 ? '−' : '+'}${Math.round(Math.abs(v as number) * 100)} %`);
    // 3.3: Konter direkt aus den Werten – passt dadurch immer zu den Resistenzen
    const weak = Object.entries(r).filter(([, v]) => (v ?? 0) <= -0.1).map(([k]) => `${DAMAGE_NAMES[k as DamageType]} (${towersOfType(k as DamageType).join(', ')})`);
    const strong = Object.entries(r).filter(([, v]) => (v ?? 0) >= 0.2).map(([k]) => DAMAGE_NAMES[k as DamageType]);
    return h('div', { class: 'small', 'data-testid': 'resist-line' },
      h('p', {}, 'Resistenzen: ', parts.length ? parts.join(', ') : 'keine'),
      weak.length ? h('p', { class: 'res-down' }, '▲ Schwach gegen: ', weak.join(' · ')) : null,
      strong.length ? h('p', { class: 'res-up' }, '▼ Meiden: ', strong.join(', ')) : null);
  }

  /** 3.3: Stärken/Schwächen eines Turms gegen konkrete Gegner, aus den Resistenzwerten berechnet. */
  private matchupLines(d: { damageType: DamageType | null }): HTMLElement | null {
    if (!d.damageType) return null;
    const m = damageMatchups(d.damageType);
    return h('div', { class: 'small', 'data-testid': 'matchups' },
      m.good.length ? h('p', { class: 'res-down' }, `▲ ${DAMAGE_NAMES[d.damageType]} wirkt stark gegen: `, m.good.join(', ')) : null,
      m.bad.length ? h('p', { class: 'res-up' }, `▼ ${DAMAGE_NAMES[d.damageType]} wird gedämpft von: `, m.bad.join(', ')) : null);
  }

  // ------------------------------------------------------------ Modale Dialoge

  private updateModal(): void {
    const s = this.sim.s;
    let want: typeof this.modalKind = '';
    if (this.tutorialActive) want = 'tutorial';
    else if (s.phase === 'choice' && s.modOffer) want = 'choice';
    else if (s.paused && !this.sim.isOver() && !this.v.miniPaused) want = 'pause';
    if (this.modalKind === 'confirm' && want === 'pause' && !this.modalDirty) return;
    if (want === this.modalKind && !this.modalDirty) return;
    this.modalDirty = false;
    this.modalKind = want;
    clear(this.modal);
    toggleClass(this.modal, 'show', want !== '');
    if (want === 'choice') this.renderChoice();
    else if (want === 'pause') this.renderPause();
    else if (want === 'tutorial') this.renderTutorial();
  }

  private renderChoice(): void {
    const s = this.sim.s;
    const offer = s.modOffer ?? [];
    const box = h('div', { class: 'dialog choice', role: 'dialog', 'aria-label': 'Modifikator wählen', 'data-testid': 'mod-choice' },
      h('h2', {}, 'Wähle einen Riss-Modifikator'),
      h('p', { class: 'muted' }, 'Gilt für den Rest der Partie.'),
      h('div', { class: 'cards' }, offer.map((id, i) => {
        const m = MODIFIERS[id];
        return h('button', { class: 'card mod', 'data-testid': `mod-${i}`, onclick: () => this.a.chooseModifier(id) },
          h('div', { class: 'card-ic', 'aria-hidden': 'true' }, m.icon), h('h3', {}, m.name), h('p', {}, m.description),
          m.tags.includes('tradeoff') ? h('div', { class: 'tag' }, '⚖ Abwägung') : null);
      })),
      h('button', { class: 'btn', onclick: () => this.a.chooseModifier(null) }, `Überspringen (+${SKIP_GOLD} Gold)`));
    this.modal.append(box);
    (box.querySelector('.card') as HTMLElement | null)?.focus();
  }

  private renderPause(): void {
    const s = this.sim.s;
    const box = h('div', { class: 'dialog', role: 'dialog', 'aria-label': 'Pause', 'data-testid': 'pause-menu' },
      h('h2', {}, 'Pause'),
      h('p', { class: 'muted' }, `${MODE_NAMES[s.mode]} · ${s.map.name} · Seed ${s.seed.text}`),
      h('p', { class: 'tiny muted' }, `KERNWACHT Version ${__APP_VERSION__}`),
      s.modifiers.length ? h('p', { class: 'small' }, 'Aktive Modifikatoren: ', s.modifiers.map((m) => MODIFIERS[m].name).join(', ')) : null,
      h('div', { class: 'stack' },
        h('button', { class: 'btn primary', 'data-autofocus': '', onclick: () => this.a.togglePause() }, 'Fortsetzen'),
        h('button', { class: 'btn', onclick: () => this.a.help() }, 'Hilfe & Kodex'),
        h('button', { class: 'btn', onclick: () => this.a.settings() }, 'Einstellungen'),
        h('button', { class: 'btn', onclick: () => this.confirm('Partie mit demselben Seed neu starten?', () => this.a.restart()) }, 'Neustart'),
        h('button', { class: 'btn', onclick: () => this.confirm(s.phase === 'build' ? 'Zum Hauptmenü? Die Partie kann später fortgesetzt werden.' : 'Zum Hauptmenü? Fortgesetzt wird ab der letzten Bauphase.', () => this.a.quit()) }, 'Hauptmenü'),
      ));
    this.modal.append(box);
    (box.querySelector('[data-autofocus]') as HTMLElement | null)?.focus();
  }

  private confirm(text: string, yes: () => void): void {
    this.modalKind = 'confirm';
    clear(this.modal);
    this.modal.append(h('div', { class: 'dialog', role: 'alertdialog' },
      h('p', {}, text),
      h('div', { class: 'row' },
        h('button', { class: 'btn primary', onclick: () => yes() }, 'Ja'),
        h('button', { class: 'btn', onclick: () => { this.modalKind = ''; this.modalDirty = true; } }, 'Nein'))));
  }

  // ------------------------------------------------------------ Tutorial

  tutorialActive = false;

  startTutorial(): void {
    this.tutorialActive = true;
    this.tutorialStep = 0;
    this.modalDirty = true;
  }

  private renderTutorial(): void {
    const mazing = this.sim.s.map.variant === 'mazing';
    const steps: [string, string][] = [
      ['Willkommen, Kernwächter!', 'Gegner strömen aus dem violetten Riss und wollen den leuchtenden Kern erreichen. Jeder Gegner, der durchkommt, kostet Kernenergie (♥). Sinkt sie auf 0, ist die Partie verloren.'],
      ['Türme bauen', 'Wähle unten einen Turm (oder Taste 1–0). Am Desktop klickst du ein freies Feld an. Auf Touchgeräten tippst du ein Feld an, prüfst Vorschau und Reichweite und bestätigst mit „Hier bauen“ oder einem zweiten Tippen.'],
      [mazing ? 'Mazing: baue ein Labyrinth' : 'Classic: der Weg ist fest', mazing
        ? 'Hier gibt es keinen festen Weg. Türme und günstige Barrikaden (B) lenken die Gegner um – die Vorschau zeigt die neue Route. Den Weg vollständig zu blockieren ist nicht möglich. Flieger folgen ebenfalls dem Weg, brauchen aber Türme mit Luftabwehr.'
        : 'Gegner folgen dem hellen Weg. Platziere Türme an Kurven, damit sie lange in Reichweite bleiben. Anhöhen geben Reichweite, Leylinien (violetter Ring) Schaden, Goldadern (gelbe Kristalle) mehr Minenertrag.'],
      ['Wellen & Gold', 'Starte jede Welle mit „▶ Welle“ (Leertaste). Die Leiste oben zeigt, welche Gegner kommen – tippe sie für Details und Konter an. Abschüsse und überstandene Wellen bringen Gold.'],
      ['Aufwerten, Fähigkeiten, Fusion', 'Tippe einen Turm an: drei Ausbaustufen, danach eine von zwei Spezialisierungen. Ab Stufe 2 haben viele Türme eine aktive Fähigkeit (C). Zwei passende, voll ausgebaute Türme nebeneinander lassen sich zu einem legendären Turm verschmelzen.'],
      ['Helden', 'Unten links warten deine Helden. Z/X oder Porträt antippen und eine Wegzelle wählen: der Held kämpft dort eine begrenzte Zeit. Fähigkeiten mit A/S/D. Einsätze kosten Rissenergie (⚡), die du durch Abschüsse, Reaktionen und Wellen sammelst. Fällt ein Held, braucht er länger zur Erholung.'],
      ['Elemente, Netzwerke, Bosse', 'Kombiniere Elemente: Feuer + Kälte = Dampf (nass), Blitz auf nasse Ziele = Überladung, Feuer auf Gift = Giftbrand. Leitpylone vernetzen Türme für Boni und Resonanzen. Bosse kündigen Fähigkeiten an – Resonatoren unterbrechen sie.'],
      ['Kamera', 'Ziehen verschiebt die Karte, Mausrad oder zwei Finger zoomen, ⟲ ⟳ (Q/E) drehen. Die Minikarte oben rechts springt per Antippen. „?“ öffnet jederzeit Hilfe und Kodex. Viel Erfolg!'],
    ];
    const [title, text] = steps[this.tutorialStep];
    const last = this.tutorialStep === steps.length - 1;
    const box = h('div', { class: 'dialog tutorial', role: 'dialog', 'aria-label': 'Einführung', 'data-testid': 'tutorial' },
      h('div', { class: 'muted small' }, `Einführung ${this.tutorialStep + 1}/${steps.length}`),
      h('h2', {}, title), h('p', {}, text),
      h('div', { class: 'row' },
        h('button', { class: 'btn', onclick: () => this.endTutorial() }, 'Überspringen'),
        h('button', { class: 'btn primary', 'data-testid': 'tutorial-next', onclick: () => {
          if (last) this.endTutorial();
          else {
            this.tutorialStep++;
            this.modalDirty = true;
          }
        } }, last ? 'Los geht’s!' : 'Weiter')));
    this.modal.append(box);
    (box.querySelector('.primary') as HTMLElement | null)?.focus();
  }

  private endTutorial(): void {
    this.tutorialActive = false;
    this.modalDirty = true;
    this.a.tutorialDone();
  }

  forcePanelRefresh(): void {
    this.panelSig = '';
  }

  hasModal(): boolean {
    return this.modalKind !== '';
  }
}
