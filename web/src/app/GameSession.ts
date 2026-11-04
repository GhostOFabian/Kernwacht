import { towerUnlocked, towerUnlockWave } from '../config/passiveTree.ts';
import { SIM } from '../config/balance.ts';
import type { SfxId } from '../audio/AudioSystem.ts';
import { MODIFIERS } from '../config/modifiers.ts';
import { BOSSES } from '../config/enemies.ts';
import { HEROES } from '../config/heroes.ts';
import { BUILD_ORDER, TOWERS } from '../config/towers.ts';
import type { GameEvent, GridPos, HeroId, HeroMode, ModifierId, TargetPriority, TowerId } from '../core/types.ts';
import { fusionOptions, fusionPreview } from '../sim/EconomySystem.ts';
import { FUSION_RANGE } from '../config/towers.ts';
import { canPlaceHero } from '../sim/HeroSystem.ts';
import { InputController } from '../input/InputController.ts';
import type { Floater, ViewUiState } from '../render/WorldView.ts';
import { canSerializeRun } from '../save/SaveSystem.ts';
import type { BuildCheck } from '../sim/EconomySystem.ts';
import type { GameSimulation } from '../sim/GameSimulation.ts';
import { h } from '../ui/dom.ts';
import { Hud, type HudActions, type HudView } from '../ui/Hud.ts';
import { openOverlay } from '../ui/screens/overlay.ts';
import { PRIORITY_ORDER } from '../ui/text.ts';
import type { GameApplication } from './GameApplication.ts';
import { commitHeroProgress, recordRun } from './progress.ts';
import { translateKey } from '../config/keys.ts';
import { CHAMPIONS } from '../config/champions.ts';
import { POTIONS } from '../config/items.ts';
import { Cell, type PotionId } from '../core/types.ts';
import { championWalkable, skillState } from '../sim/ChampionSystem.ts';
import { HeroHud, type HeroHudActions } from '../ui/HeroHud.ts';

// Spielsitzung: verbindet Simulation, Darstellung, Eingabe, HUD und Audio.
// Die Simulation läuft mit festem Takt; die Darstellung liest nur.

const SHOT_SFX: Partial<Record<TowerId, SfxId>> = {
  ranger: 'shot_ranger', ember: 'shot_ember', ballista: 'shot_ballista', cannon: 'shot_cannon', spore: 'shot_spore',
  rune: 'beam', riftbinder: 'rift', pylon: 'zap', meteor: 'shot_meteor', starfall: 'shot_meteor', laser: 'beam', gravity: 'gravity',
  glacier: 'shot_spore', voidlance: 'shot_ballista', dragon: 'shot_ember',
};

export class GameSession implements HudActions, HeroHudActions {
  readonly hud: Hud;
  private readonly input: InputController;
  private readonly ui: ViewUiState;
  private readonly hv: HudView;
  private ended = false;
  private endTimer = 0;
  private fpsAcc = 0;
  private fpsFrames = 0;
  private lowFpsTime = 0;
  adaptiveScale = 1;
  private floaterCount = 0;
  private slowmo = 0;
  private lastPhase: string;
  private keepBuilding = false;
  private resizeObs: ResizeObserver | null = null;
  /** Helden-Endlos: HUD-Erweiterung und Zielwahl einer Fähigkeit (-1 = keine). */
  readonly heroHud: HeroHud | null = null;
  private targeting = -1;
  private champSelected = false;

  constructor(private readonly app: GameApplication, readonly sim: GameSimulation) {
    // Automatisierte Oberflächentests bauen viele Türme – dort ist das Turmlimit abschaltbar.
    if ((globalThis as { __KW_E2E_NOLIMIT?: boolean }).__KW_E2E_NOLIMIT) sim.s.rules.towerLimitOff = true;
    const st = app.save.settings;
    this.ui = {
      buildDef: null, hoverCell: null, preview: null, selectedTower: 0, showRanges: st.showRanges, showGrid: st.showGrid,
      reducedMotion: st.reducedMotion, particleCap: app.particleCap(), selectedHero: 0, heroPlacing: null, highlight: [],
    };
    this.hv = { buildDef: null, selectedTower: 0, pendingCell: null, pendingCheck: null, touchMode: matchMedia('(pointer: coarse)').matches, infoEnemy: null, showRanges: st.showRanges, placingHero: null, selectedHero: 0, selectedEnemy: 0, rotationLocked: st.lockRotation, miniPaused: false, selectedObstacle: -1, groupDef: null };
    this.hud = new Hud(sim, this, this.hv, sim.s.map.variant === 'mazing', app.icons);
    this.hud.onModInfo = (id: ModifierId) => app.toast(`${MODIFIERS[id].icon} ${MODIFIERS[id].name}: ${MODIFIERS[id].description}`, 'info', 4500);
    if (sim.s.he) {
      this.heroHud = new HeroHud(sim, this, app.icons);
      this.hud.statsBox.append(this.heroHud.resBar);
      this.hud.stageLeft.append(this.heroHud.dock);
      this.hud.stage.append(this.heroHud.win);
      this.hud.root.classList.add('hero-endless');
    }
    app.ui.appendChild(this.hud.root);
    this.hud.stage.prepend(app.canvas);
    app.canvas.className = 'game-canvas';
    app.view.setMap(sim.s.map, app.detailLevel());
    app.view.onFloater = (f) => this.floater(f);
    app.view.onBossAppear = (b) => {
      this.hud.bossBanner(b);
      app.audio.play('bossIntro');
    };
    this.input = new InputController(app.canvas, {
      tap: (x, y, type, time) => this.tap(x, y, type, time),
      hover: (x, y) => this.hover(x, y),
      hoverEnd: () => this.setHover(null),
      pan: (dx, dy) => app.view.cam.pan(dx, dy),
      zoom: (f, x, y) => app.view.cam.zoomBy(f, x, y),
      orbit: (dy, dp) => this.orbit(dy, dp),
      key: (e) => this.key(e),
    });
    app.canvas.addEventListener('contextmenu', this.onContext);
    this.resizeObs = new ResizeObserver(() => app.resize());
    this.resizeObs.observe(this.hud.stage);
    this.lastPhase = sim.s.phase;
    app.resize();
    app.view.cam.fit();
    // 2.9: etwas näher starten – Felder und Lanes wirken größer (ganze Karte über den Knopf „Ganze Karte“)
    app.view.cam.zoom = Math.max(app.view.cam.minZoom, app.view.cam.zoom * 0.62);
    app.view.cam.clamp();
    if (!st.tutorialSeen && sim.s.wave === 0 && !sim.s.he) this.hud.startTutorial();
    if (sim.s.he) {
      const c = sim.s.he.champion;
      app.view.cam.tx = c.x;
      app.view.cam.tz = c.y;
      app.view.cam.clamp();
      app.toast(`Vorbereitung: ${Math.ceil(sim.s.he.autoTimer)} s bis zur ersten Welle. Klick/Rechtsklick bewegt den Helden, A/S/D/W wirken Fähigkeiten, Tab öffnet den Charakter, X den Laden.`, 'info', 7000);
    }
    this.app.audio.intensity = 0;
    this.saveResume();
  }

  private onContext = (e: Event): void => {
    e.preventDefault();
    if (this.movingTower) {
      this.stopMove();
      return;
    }
    const he = this.sim.s.he;
    if (he && !this.ui.buildDef && !this.hud.hasModal()) {
      if (this.targeting >= 0) {
        this.setTargeting(-1);
        return;
      }
      const me = e as MouseEvent;
      const rect = this.app.canvas.getBoundingClientRect();
      const cell = this.app.view.pickCell(me.clientX - rect.left, me.clientY - rect.top, this.sim.s.map);
      if (cell) this.moveChampionTo(cell.x, cell.y);
      return;
    }
    this.cancel();
  };

  dispose(): void {
    this.input.dispose();
    this.app.view.cam.topDown = false;
    this.resizeObs?.disconnect();
    this.app.canvas.removeEventListener('contextmenu', this.onContext);
    this.app.view.onFloater = null;
    this.app.view.onBossAppear = null;
    if (!this.ended) this.saveResume();
  }

  // ------------------------------------------------------------ Frame

  frame(dt: number): void {
    const sim = this.sim;
    const app = this.app;
    const blocking = this.hud.tutorialActive;
    // Kurze Zeitlupe nach einem Bosssieg (rein darstellerisch: die Simulation bleibt im festen Takt).
    const slow = this.slowmo > 0 ? 0.3 : 1;
    this.slowmo = Math.max(0, this.slowmo - dt);
    if (!blocking) sim.advance(dt * slow);
    const events = sim.drainEvents();
    if (events.length) {
      app.view.handleEvents(events, sim, this.ui);
      this.handleEvents(events);
    }
    if (sim.s.phase !== this.lastPhase) this.onPhaseChange(this.lastPhase, sim.s.phase);
    // Angeklickter Gegner gestorben oder durchgebrochen?
    if (this.hv.selectedEnemy && !sim.c.enemyByUid.get(this.hv.selectedEnemy)?.alive) this.hv.selectedEnemy = 0;
    this.ui.selectedEnemy = this.hv.selectedEnemy;
    if (this.hv.selectedObstacle >= 0 && sim.s.map.cells[this.hv.selectedObstacle] !== Cell.Blocked && sim.s.he?.mining?.cell !== this.hv.selectedObstacle) this.hv.selectedObstacle = -1;
    // Ausgewählter Turm wurde entfernt?
    if (this.ui.selectedTower && !sim.c.towerByUid.has(this.ui.selectedTower)) this.select(0);
    // Mazing: Vorschau aktualisieren, wenn sich Gegner bewegen (Blockade-Prüfung).
    if (this.ui.buildDef && this.ui.hoverCell && sim.s.map.variant === 'mazing' && sim.s.tick % 6 === 0) this.refreshPreview();
    app.audio.intensity = sim.s.boss ? 2 : sim.s.phase === 'combat' ? 1 : 0;
    this.ui.particleCap = app.particleCap();
    // Helden: Auswahl/Platzierung gültig halten
    if (this.ui.selectedHero && !sim.s.heroes.some((x) => x.uid === this.ui.selectedHero && x.state === 'active')) this.selectHero(0);
    if (this.ui.heroPlacing) {
      const ph = sim.s.heroes.find((x) => x.id === this.ui.heroPlacing);
      if (!ph || ph.state !== 'ready') this.setPlacing(null);
    }
    // Fusionspartner des ausgewählten Turms hervorheben
    const sel = this.ui.selectedTower ? sim.c.towerByUid.get(this.ui.selectedTower) : undefined;
    // Fusionspartner hervorheben: beim ausgewählten Turm und schon beim Platzieren eines neuen Turms
    if (sel && this.hv.groupDef === sel.def) this.ui.highlight = sim.s.towers.filter((o) => o.def === sel.def && o !== sel).map((o) => o.uid);
    else if (sel) this.ui.highlight = fusionOptions(sim, sel).map((f) => f.partner.uid);
    else if (this.ui.buildDef && this.ui.hoverCell) {
      const fp = fusionPreview(sim, this.ui.buildDef, this.ui.hoverCell.x, this.ui.hoverCell.y);
      this.ui.highlight = fp.flatMap((f) => f.partners.map((p) => p.uid));
      this.ui.fusionRing = fp.length ? FUSION_RANGE : 0;
    } else this.ui.highlight = [];
    if (!this.ui.buildDef) this.ui.fusionRing = 0;
    if (this.hud.followBoss && sim.s.boss) {
      const b = sim.c.enemyByUid.get(sim.s.boss.uid);
      const c = app.view.cam;
      if (b && b.alive && !c.focusing) {
        const k = Math.min(1, dt * 2.5);
        c.tx += (b.x - c.tx) * k;
        c.tz += (b.y - c.tz) * k;
      }
    }
    if (this.heroHud && sim.s.he) {
      const c = sim.s.he.champion;
      if (this.heroHud.follow && c.dead <= 0 && !app.view.cam.focusing) {
        const k = Math.min(1, dt * 4);
        app.view.cam.tx += (c.x - app.view.cam.tx) * k;
        app.view.cam.tz += (c.y - app.view.cam.tz) * k;
      }
      if (this.targeting >= 0) {
        const chk = skillState(sim, this.targeting);
        if (!chk.ok) this.setTargeting(-1);
      }
      this.ui.champSelected = this.champSelected;
    }
    app.view.frame(sim, this.ui, dt * slow);
    this.hud.camQuad = this.cameraQuad();
    this.hud.update();
    this.heroHud?.update();
    this.adapt(dt);
    if (sim.isOver() && !this.ended) {
      this.endTimer += dt;
      if (this.endTimer > 1.6) this.finish();
    }
  }

  /** Adaptive Auflösung: bei dauerhaft niedriger Bildrate wird die Pixeldichte reduziert. */
  private adapt(dt: number): void {
    if (this.app.save.settings.quality !== 'auto') return;
    this.fpsAcc += dt;
    this.fpsFrames++;
    if (this.fpsAcc < 1) return;
    const fps = this.fpsFrames / this.fpsAcc;
    this.fpsAcc = 0;
    this.fpsFrames = 0;
    if (fps < 38) this.lowFpsTime++;
    else this.lowFpsTime = 0;
    if (this.lowFpsTime >= 3) {
      this.lowFpsTime = 0;
      // Erst Effekte (Kantenglättung, Schatten, Glühen) reduzieren, dann die Auflösung.
      if (!this.app.lowerAutoQuality() && this.adaptiveScale > 0.55) {
        this.adaptiveScale = Math.max(0.55, this.adaptiveScale - 0.15);
        this.app.resize();
      }
    }
  }

  private cameraQuad(): [number, number][] {
    const c = this.app.view.cam;
    const pts: [number, number][] = [];
    for (const [x, y] of [[0, 0], [c.width, 0], [c.width, c.height], [0, c.height]]) {
      const g = c.screenToGround(x, y, 0);
      if (!g) return [];
      pts.push([g.x, g.z]);
    }
    return pts;
  }

  private onPhaseChange(prev: string, next: string): void {
    this.lastPhase = next;
    const he = this.sim.s.he;
    if (he && next === 'build') {
      // Bestwelle wird sofort dauerhaft gesichert (nicht erst bei Partieende).
      const m = this.app.save.progress.heroMeta;
      m.bestWave = Math.max(m.bestWave, this.sim.s.stats.wavesCleared);
      commitHeroProgress(this.app.save, this.sim.s);
      this.app.persist();
    }
    if (next === 'build') {
      this.saveResume();
      if (prev === 'choice') this.hud.forcePanelRefresh();
    }
  }

  private saveResume(): void {
    const s = this.sim.s;
    if (this.sim.isOver()) return;
    if (!canSerializeRun(s)) return;
    this.app.save.resume = this.sim.serialize();
    this.app.persist();
  }

  private finish(): void {
    this.ended = true;
    const save = this.app.save;
    save.resume = null;
    const record = recordRun(save, this.sim.s);
    this.app.persist();
    if (record.newAchievements.length) this.app.audio.play('achievement');
    this.app.go('results', { results: { state: this.sim.s, record } });
  }

  /** Simulation ohne Darstellung weiterrechnen (Tab im Hintergrund). */
  simOnly(dt: number): void {
    if (this.hud.tutorialActive) return;
    const sim = this.sim;
    let left = Math.max(0, dt);
    while (left > 1e-6) {
      const step = Math.min(0.1, left);
      left -= step;
      sim.advance(step);
      const events = sim.drainEvents();
      if (events.length) this.handleEvents(events);
      if (sim.s.phase !== this.lastPhase) this.onPhaseChange(this.lastPhase, sim.s.phase);
      if (sim.isOver() || sim.s.paused) break;
    }
  }

  autoPause(): void {
    if (!this.sim.isOver() && this.sim.s.phase === 'combat') this.sim.setPaused(true);
  }

  // ------------------------------------------------------------ Ereignisse → Audio/HUD

  private handleEvents(events: GameEvent[]): void {
    const au = this.app.audio;
    for (const e of events) {
      switch (e.k) {
        case 'shot': { const id = SHOT_SFX[e.tower]; if (id) au.play(id, 0.8); break; }
        case 'hit': if (e.splash > 0.9) au.play('explosion', 0.6); else au.play('hit', 0.5); break;
        case 'chain': au.play('zap'); break;
        case 'pulse': au.play(e.tower === 'frost' ? 'pulse_frost' : 'pulse_sonic'); break;
        case 'kill':
          au.play('kill');
          if (e.boss) {
            au.play('victory', 0.5);
            if (!this.app.save.settings.reducedMotion) this.slowmo = 1.4;
          }
          break;
        case 'leak': au.play('leak'); break;
        case 'reaction': au.play(e.id === 'shatter' ? 'freeze' : 'reaction'); break;
        case 'gold': au.play('gold'); break;
        case 'build': au.play('build'); break;
        case 'sell': au.play('sell'); break;
        case 'upgrade': au.play('upgrade'); break;
        case 'waveStart': {
          au.play(e.boss ? 'bossWarn' : 'waveStart');
          const w = this.sim.s.currentWave;
          const total = this.sim.s.rules.endless ? '' : ` von ${this.sim.s.rules.waves}`;
          this.hud.banner(`Welle ${e.wave}`, e.boss ? 'Bosswelle!' : w?.event ? `Geländeereignis aktiv${total}` : total ? `Welle ${e.wave}${total}` : 'Endlos', e.boss ? 'boss' : 'wave', 1900);
          break;
        }
        case 'waveClear': au.play('waveClear'); this.hud.banner(`Welle ${e.wave} überstanden`, `+${e.bonus} Gold`, 'clear', 1800); break;
        case 'bossPhase':
          au.play('bossWarn');
          if (e.phase > 0) this.hud.banner(e.name, BOSSES[e.boss].name, 'info', 2000);
          break;
        case 'heroDeploy': au.play('heroDeploy'); break;
        case 'heroDown': au.play('heroDown'); this.app.toast(`${HEROES[e.hero].name} ist gefallen und braucht länger zur Erholung.`, 'warn', 3200); break;
        case 'heroAbility': au.play('heroAbility', 0.8); break;
        case 'heroAttack': if (Math.random() < 0.5) au.play(e.melee ? 'hit' : 'shot_ranger', 0.4); break;
        case 'heroLevel': au.play('levelUp'); break;
        case 'meteor': au.play('explosion', e.big ? 1 : 0.6); break;
        case 'fusion': au.play('fusion'); this.hud.banner(TOWERS[e.tower].name, 'Legendärer Turm erschaffen!', 'win', 2200); break;
        case 'towerActive': au.play('towerActive'); break;
        case 'plateBreak': au.play('shatter'); break;
        case 'devour': au.play('devour'); break;
        case 'burst': au.play('explosion', 0.7); break;
        case 'heal': au.play('heal', 0.5); break;
        case 'gravity': au.play('gravity', 0.6); break;
        case 'arc': if (Math.random() < 0.3) au.play('zap', 0.4); break;
        case 'ability':
          if (e.stage === 'warn') au.play('ability');
          else if (e.stage === 'interrupt') au.play('interrupt');
          break;
        case 'teleport': au.play('teleport'); break;
        case 'victory': au.play('victory'); this.hud.banner('Sieg!', 'Der Kern ist gerettet', 'win', 3000); break;
        case 'defeat': au.play('defeat'); this.hud.banner('Niederlage', 'Der Kern ist gefallen', 'lose', 3000); break;
        case 'message': this.app.toast(e.text, e.severity, e.severity === 'danger' ? 4200 : 3000); break;
        case 'champAttack': if (Math.random() < 0.5) au.play(e.melee ? 'hit' : 'shot_ranger', 0.4); break;
        case 'champLevel': au.play('levelUp'); this.hud.banner(`Stufe ${e.level}`, 'Neuer Fertigkeitspunkt – Tab öffnet den Skillbaum', 'info', 1800); this.heroHud?.refresh(); break;
        case 'champDown': au.play('heroDown'); break;
        case 'champRespawn': au.play('heroDeploy'); break;
        case 'pickup':
          if (e.kind === 'item') { au.play('achievement'); this.app.toast(`Gefunden: ${e.label}`, 'info', 2400); }
          else if (e.kind !== 'xp') au.play('gold', 0.5);
          this.heroHud?.refresh();
          break;
        case 'craft': this.app.toast(`Hergestellt: ${e.label}`, 'info', 2200); break;
        default: break;
      }
    }
  }

  private floater(f: Floater): void {
    if (this.floaterCount > 30) return;
    const p = this.app.view.project(f.x, f.y, f.z);
    const el = h('div', { class: `floater${f.big ? ' big' : ''}`, style: `left:${p.x}px;top:${p.y}px;color:${f.color}` }, f.text);
    this.floaterCount++;
    const done = (): void => {
      el.remove();
      this.floaterCount--;
    };
    el.addEventListener('animationend', done, { once: true });
    window.setTimeout(() => { if (el.isConnected) done(); }, 1500);
    this.hud.floaters.appendChild(el);
  }

  // ------------------------------------------------------------ Eingabe

  private tap(px: number, py: number, type: string, time = performance.now()): void {
    this.tapTime = time;
    this.hv.touchMode = type !== 'mouse';
    if (this.hud.hasModal()) return;
    const cell = this.app.view.pickCell(px, py, this.sim.s.map);
    if (this.ui.heroPlacing) {
      if (!cell) return;
      const id = this.ui.heroPlacing;
      const r = this.sim.deployHero(id, cell.x, cell.y);
      if (!r.ok) {
        this.app.audio.play('error');
        this.app.toast(r.reason, 'warn', 1800);
        return;
      }
      this.setPlacing(null);
      const hero = this.sim.s.heroes.find((x) => x.id === id);
      if (hero) this.selectHero(hero.uid);
      return;
    }
    // Legendären Turm versetzen: Zielfeld anklicken
    if (this.movingTower) {
      if (!cell) return;
      const uid = this.movingTower;
      const r = this.sim.moveTower(uid, cell.x, cell.y);
      if (!r.ok) {
        this.app.audio.play('error');
        this.app.toast(r.reason, 'warn', 1800);
        return;
      }
      this.stopMove();
      this.app.audio.play('build');
      this.select(uid);
      this.hud.forcePanelRefresh();
      return;
    }
    if (this.ui.buildDef) {
      if (!cell) return;
      if (type === 'mouse') {
        this.setHover(cell);
        this.tryBuild(cell);
        return;
      }
      // Touch: erstes Tippen = Vorschau, zweites Tippen auf dasselbe Feld = Bauen
      const pc = this.hv.pendingCell;
      if (pc && pc.x === cell.x && pc.y === cell.y && this.hv.pendingCheck?.ok) {
        this.tryBuild(cell);
      } else {
        this.setHover(cell);
        this.hv.pendingCell = cell;
      }
      return;
    }
    // Gegner anklicken (Körper, nicht nur Bodenfeld) – nicht während der Ziel-Fähigkeit
    if (this.targeting < 0) {
      const foe = this.app.view.pickEnemy(px, py, this.sim);
      if (foe) {
        this.selectEnemy(foe);
        this.app.audio.play('select');
        return;
      }
    }
    // Türme über das ganze Modell anklickbar
    const picked = this.app.view.pickTower(px, py, this.sim);
    if (picked) {
      const pt = this.sim.c.towerByUid.get(picked);
      if (pt && this.targeting >= 0) {
        this.castAt(this.targeting, pt.cx + 0.5, pt.cy + 0.5);
        return;
      }
      if (pt) {
        this.selectHero(0);
        this.hv.selectedEnemy = 0;
        this.towerTap(picked);
        this.app.audio.play('select');
        return;
      }
    }
    // Helden-Endlos: Bäume/Felsen über das ganze Modell anklickbar (nicht nur das Bodenfeld)
    if (this.sim.s.he && this.targeting < 0) {
      const ob = this.app.view.pickObstacle(px, py, this.sim);
      const he = this.sim.s.he;
      if (ob >= 0 && ob !== he.shop.y * this.sim.s.map.width + he.shop.x && !(cell && this.sim.towerAt(cell.x, cell.y))) {
        this.select(0);
        this.hv.selectedObstacle = ob;
        this.hud.forcePanelRefresh();
        this.app.audio.play('select');
        return;
      }
    }
    if (!cell) {
      this.select(0);
      return;
    }
    if (this.sim.s.he && this.heroTap(cell.x, cell.y)) return;
    const t = this.sim.towerAt(cell.x, cell.y);
    // Ausgewählter Held: Wegzelle antippen versetzt den Ankerpunkt
    const selHero = this.ui.selectedHero ? this.sim.s.heroes.find((x) => x.uid === this.ui.selectedHero) : undefined;
    if (!t && selHero && selHero.state === 'active' && canPlaceHero(this.sim, cell.x, cell.y).ok) {
      const r = this.sim.heroAnchor(selHero.id, cell.x, cell.y);
      if (r.ok && selHero.mode === 'retreat') this.sim.heroMode(selHero.id, 'hold');
      this.app.audio.play(r.ok ? 'select' : 'error');
      return;
    }
    // Held angetippt?
    const tappedHero = this.sim.s.heroes.find((x) => x.state === 'active' && Math.floor(x.x) === cell.x && Math.floor(x.y) === cell.y);
    if (!t && tappedHero) {
      this.selectHero(tappedHero.uid === this.ui.selectedHero ? 0 : tappedHero.uid);
      return;
    }
    if (t) {
      this.selectHero(0);
      this.towerTap(t.uid);
      this.app.audio.play('select');
    } else {
      this.selectHero(0);
      this.select(0);
      this.hv.infoEnemy = null;
    }
  }

  private hover(px: number, py: number): void {
    this.hv.touchMode = false;
    if (this.movingTower) {
      const c = this.app.view.pickCell(px, py, this.sim.s.map);
      this.ui.hoverCell = c;
      this.ui.preview = c ? this.sim.canMoveTower(this.movingTower, c.x, c.y) : null;
      return;
    }
    if (this.targeting >= 0) {
      this.ui.hoverCell = this.app.view.pickCell(px, py, this.sim.s.map);
      return;
    }
    if (this.ui.heroPlacing) {
      const c = this.app.view.pickCell(px, py, this.sim.s.map);
      this.ui.hoverCell = c;
      return;
    }
    if (!this.ui.buildDef) return;
    this.setHover(this.app.view.pickCell(px, py, this.sim.s.map));
  }

  private setHover(cell: GridPos | null): void {
    const prev = this.ui.hoverCell;
    if (prev && cell && prev.x === cell.x && prev.y === cell.y) return;
    this.ui.hoverCell = cell;
    if (!cell) {
      this.ui.preview = null;
      if (!this.hv.touchMode) this.hv.pendingCell = null;
      this.hv.pendingCheck = null;
      return;
    }
    if (!this.hv.touchMode) this.hv.pendingCell = cell;
    this.refreshPreview();
  }

  private refreshPreview(): void {
    const c = this.ui.hoverCell;
    if (!c || !this.ui.buildDef) return;
    const chk: BuildCheck = this.sim.canBuild(this.ui.buildDef, c.x, c.y, this.hv.buildSpec ?? 0);
    this.ui.preview = chk;
    this.hv.pendingCheck = chk;
  }

  private tryBuild(cell: GridPos): void {
    const def = this.ui.buildDef;
    if (!def) return;
    const spec = this.hv.buildSpec ?? 0;
    const r = this.sim.build(def, cell.x, cell.y, spec);
    if (!r.ok) {
      this.app.audio.play('error');
      this.app.toast(r.reason, 'warn', 1800);
      this.refreshPreview();
      return;
    }
    const cost = this.sim.canBuild(def, cell.x, cell.y, spec).cost;
    const canAgain = this.sim.s.gold >= cost;
    if ((this.keepBuilding || def === 'barricade') && canAgain) {
      this.ui.hoverCell = null;
      this.hv.pendingCell = null;
      this.hv.pendingCheck = null;
      this.ui.preview = null;
      this.setHover(cell);
    } else {
      this.selectBuild(null);
    }
    this.saveResumeIfBuild();
  }

  private saveResumeIfBuild(): void {
    // Bauaktionen in der Bauphase werden sofort gesichert (für Fortsetzen).
    if (this.sim.s.phase === 'build') this.saveResume();
  }

  private key(e: KeyboardEvent): void {
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    this.keepBuilding = e.shiftKey;
    const raw = e.key.toLowerCase();
    // Eigene Tastenbelegung → Standardtaste der Aktion (Umschalt+Ziffer bleibt für Tränke)
    const tr = e.shiftKey && /^Digit[1-5]$/.test(e.code) ? '' : translateKey(raw, this.sim.s.he ? 'hero' : 'lane');
    if (tr === null) return;
    const k = tr || raw;
    if (this.hud.hasModal()) {
      if ((k === 'escape' || k === 'p') && this.sim.s.paused) {
        e.preventDefault();
        this.togglePause();
      }
      return;
    }
    if (this.sim.s.he && this.heroKey(e, k)) return;
    const mazing = this.sim.s.map.variant === 'mazing';
    const tower = BUILD_ORDER.find((id) => (TOWERS[id].hotkey === k || (k === 'ß' && TOWERS[id].hotkey === '-')) && !(TOWERS[id].mazingOnly && !mazing) && !(TOWERS[id].laneOnly && mazing));
    if (tower) {
      e.preventDefault();
      this.selectBuild(this.ui.buildDef === tower ? null : tower);
      return;
    }
    const heroes = this.sim.s.heroes;
    if ((k === 'z' || k === 'y' || k === 'x') && heroes.length) {
      const idx = k === 'x' ? 1 : 0;
      const hero = heroes[idx];
      if (hero) {
        e.preventDefault();
        this.heroCard(hero.id);
      }
      return;
    }
    if ((k === 'a' || k === 's' || k === 'd') && heroes.length) {
      const hero = this.focusHero();
      if (hero) {
        e.preventDefault();
        this.heroAbility(hero.id, k === 'a' ? 0 : k === 's' ? 1 : 2);
      }
      return;
    }
    switch (k) {
      case 'c':
        if (this.ui.selectedTower) this.towerActive(this.ui.selectedTower);
        break;
      case 'v': {
        const hero = this.focusHero();
        if (hero) {
          const order: HeroMode[] = ['hold', 'advance', 'retreat'];
          this.heroMode(hero.id, order[(order.indexOf(hero.mode) + 1) % order.length]);
        }
        break;
      }
      case ' ':
        e.preventDefault();
        if (this.sim.s.phase === 'build' && !this.sim.s.paused) this.startWave();
        else this.miniPause();
        break;
      case 'escape':
        if (this.movingTower) {
          this.stopMove();
          break;
        }
        if (this.targeting >= 0) {
          this.setTargeting(-1);
          break;
        }
        if (this.heroHud?.close()) break;
        if (this.hud.towerListOpen) {
          this.hud.toggleTowerList(false);
          break;
        }
        if (this.hv.selectedEnemy) {
          this.selectEnemy(0);
          break;
        }
        if (this.ui.buildDef || this.ui.selectedTower || this.hv.infoEnemy || this.ui.heroPlacing || this.ui.selectedHero) this.cancel();
        else this.togglePause();
        break;
      case 'p': this.togglePause(); break;
      case 'o': this.hud.toggleTowerList(); break;
      case 'enter':
        if (this.sim.s.he) {
          e.preventDefault();
          this.startWave();
        }
        break;
      case 'f': this.cycleSpeed(); break;
      case 'q': this.rotate(-1); break;
      case 'e': this.rotate(1); break;
      case ',': this.orbit(-Math.PI / 12, 0); break;
      case '.': this.orbit(Math.PI / 12, 0); break;
      case 'pageup': e.preventDefault(); this.orbit(0, (5 * Math.PI) / 180); break;
      case 'pagedown': e.preventDefault(); this.orbit(0, (-5 * Math.PI) / 180); break;
      case 'home': e.preventDefault(); this.resetView(); break;
      case 'r': this.toggleRanges(); break;
      case 'g':
        this.ui.showGrid = !this.ui.showGrid;
        this.app.save.settings.showGrid = this.ui.showGrid;
        this.app.persist();
        break;
      case 'h': case '?': this.help(); break;
      case 'u': if (this.ui.selectedTower) this.upgrade(this.ui.selectedTower); break;
      case 't': {
        const t = this.sim.c.towerByUid.get(this.ui.selectedTower);
        if (t) {
          const opts = PRIORITY_ORDER.filter((p) => p !== 'random' || TOWERS[t.def].attack === 'rift');
          this.setPriority(t.uid, opts[(opts.indexOf(t.priority) + 1) % opts.length]);
        }
        break;
      }
      case 'delete': case 'backspace':
        if (this.ui.selectedTower) (this.hud.root.querySelector('[data-testid="sell"]') as HTMLButtonElement | null)?.click();
        break;
      default: break;
    }
  }

  // ------------------------------------------------------------ HudActions

  selectBuild(def: TowerId | null, spec: 0 | 1 | 2 = 0): void {
    this.hv.buildSpec = def ? spec : 0;
    if (def && this.sim.s.rules.bannedTowers.includes(def)) return;
    if (def && this.sim.s.he && !towerUnlocked(def, this.sim.s.he, this.sim.s.wave)) {
      this.app.audio.play('error');
      this.app.toast(`${TOWERS[def].name} wird ab Welle ${towerUnlockWave(def, this.sim.s.he)} freigeschaltet.`, 'info', 1800);
      return;
    }
    if (def) {
      this.setPlacing(null);
      this.selectHero(0);
    }
    this.ui.buildDef = def;
    this.hv.buildDef = def;
    this.hv.pendingCell = null;
    this.hv.pendingCheck = null;
    this.ui.preview = null;
    this.ui.hoverCell = null;
    if (def) {
      this.select(0);
      this.hv.infoEnemy = null;
      this.app.audio.play('ui');
    }
    this.hud.forcePanelRefresh();
  }

  confirmBuild(): void {
    const c = this.hv.pendingCell;
    if (c) this.tryBuild(c);
  }

  /** Versetzen eines legendären Turms beginnen: danach das Zielfeld anklicken (Esc/Rechtsklick bricht ab). */
  startMove(uid: number): void {
    const t = this.sim.c.towerByUid.get(uid);
    if (!t) return;
    this.selectBuild(null);
    this.movingTower = uid;
    this.ui.moveDef = t.def;
    this.app.toast('Zielfeld anklicken – Esc bricht ab. Danach kurz außer Gefecht.', 'info', 2500);
  }
  stopMove(): void {
    this.movingTower = 0;
    this.ui.moveDef = null;
    this.ui.preview = null;
  }
  private movingTower = 0;

  cancel(): void {
    this.stopMove();
    this.selectBuild(null);
    this.select(0);
    this.setPlacing(null);
    this.selectHero(0);
    this.hv.infoEnemy = null;
    this.hv.selectedEnemy = 0;
    this.hv.selectedObstacle = -1;
    this.hud.forcePanelRefresh();
  }

  selectEnemy(uid: number): void {
    this.hv.selectedEnemy = uid;
    if (uid) {
      this.select(0);
      this.hv.infoEnemy = null;
      this.ui.selectedHero = 0;
      this.hv.selectedHero = 0;
    }
    this.hud.forcePanelRefresh();
  }

  toggleRotationLock(): void {
    const st = this.app.save.settings;
    st.lockRotation = !st.lockRotation;
    this.hv.rotationLocked = st.lockRotation;
    this.app.persist();
    this.app.toast(st.lockRotation ? 'Kameradrehung gesperrt – Q/E drehen nicht mehr' : 'Kameradrehung freigegeben', 'info', 1400);
  }

  selectTower(uid: number): void {
    this.select(uid);
  }

  private select(uid: number): void {
    if (this.ui.selectedTower === uid) return;
    const nt = uid ? this.sim.c.towerByUid.get(uid) : undefined;
    if (!nt || nt.def !== this.hv.groupDef) this.hv.groupDef = null;
    this.ui.selectedTower = uid;
    this.hv.selectedTower = uid;
    this.hud.disarmSell();
    if (uid) {
      this.hv.infoEnemy = null;
      this.hv.selectedEnemy = 0;
      this.hv.selectedObstacle = -1;
      this.ui.selectedHero = 0;
      this.hv.selectedHero = 0;
    }
  }

  private result(r: { ok: boolean; reason?: string }, sfxOk?: SfxId): void {
    if (!r.ok) {
      this.app.audio.play('error');
      this.app.toast(r.reason ?? 'Nicht möglich', 'warn', 1800);
    } else if (sfxOk) this.app.audio.play(sfxOk);
    this.hud.forcePanelRefresh();
    this.saveResumeIfBuild();
  }

  upgrade(uid: number): void { this.result(this.sim.upgrade(uid)); }
  upgradeAll(uids?: number[]): void {
    const r = this.sim.upgradeAll(uids);
    this.result(r, 'upgrade');
    if (r.ok && r.message) this.app.toast(r.message, 'info', 1600);
  }
  clearGroup(): void { this.hv.groupDef = null; this.hud.forcePanelRefresh(); }

  /** Doppelklick/-tipp auf einen Turm wählt alle Türme derselben Sorte. */
  private towerTap(uid: number): void {
    // Zeitstempel des Klick-Ereignisses (nicht der Verarbeitung): Doppelklicks klappen auch bei niedriger Bildrate
    const now = this.tapTime;
    const last = this.lastTowerTap;
    this.lastTowerTap = { uid, t: now };
    const t = this.sim.c.towerByUid.get(uid);
    if (t && last && last.uid === uid && now - last.t < 420) {
      this.lastTowerTap = null;
      if (this.ui.selectedTower !== uid) this.select(uid);
      this.hv.groupDef = t.def;
      const n = this.sim.s.towers.filter((o) => o.def === t.def).length;
      this.app.toast(`${n}× ${TOWERS[t.def].name} ausgewählt – gemeinsam aufwerten`, 'info', 1400);
      this.hud.forcePanelRefresh();
      return;
    }
    this.select(uid === this.ui.selectedTower && !this.hv.groupDef ? 0 : uid);
  }
  private lastTowerTap: { uid: number; t: number } | null = null;
  private tapTime = 0;
  specialize(uid: number, spec: 1 | 2): void { this.result(this.sim.specialize(uid, spec)); }
  star(uid: number): void { this.result(this.sim.star(uid), 'upgrade'); }
  starAll(uids?: number[]): void {
    const r = this.sim.starAll(uids);
    this.result(r, 'upgrade');
    if (r.ok && r.message) this.app.toast(r.message, 'info', 1600);
  }

  respec(uid: number, spec: 1 | 2): void { this.result(this.sim.respec(uid, spec), 'upgrade'); }
  groupSpec(uids: number[], spec: 1 | 2): void {
    const r = this.sim.groupSpec(uids, spec);
    this.result(r, 'upgrade');
    if (r.ok && r.message) this.app.toast(r.message, 'info', 2200);
  }
  /** Alle (bzw. die angegebenen) Türme verkaufen – nicht in der Mini-Pause. */
  sellAll(uids?: number[]): void {
    if (this.sim.s.paused && this.hv.miniPaused) {
      this.app.audio.play('error');
      this.app.toast('In der Mini-Pause können keine Türme verkauft werden.', 'warn', 1800);
      return;
    }
    const r = this.sim.sellAll(uids);
    this.result(r, 'sell');
    if (r.ok && r.message) this.app.toast(r.message, 'info', 1800);
    this.select(0);
  }
  sell(uid: number): void {
    if (this.sim.s.paused && this.hv.miniPaused) {
      this.app.audio.play('error');
      this.app.toast('In der Mini-Pause können keine Türme verkauft werden.', 'warn', 1800);
      return;
    }
    this.result(this.sim.sell(uid));
    this.select(0);
  }
  setPriority(uid: number, p: TargetPriority): void { this.result(this.sim.setPriority(uid, p), 'ui'); }

  startWave(): void {
    if (this.sim.s.paused) this.sim.setPaused(false);
    this.hv.miniPaused = false;
    const r = this.sim.s.he ? this.sim.nextWave() : this.sim.startWave();
    if (!r.ok) this.app.toast(r.reason, 'warn', 1500);
    this.selectBuild(null);
  }

  togglePause(): void {
    if (this.sim.isOver()) return;
    if (this.sim.s.paused && this.hv.miniPaused) {
      // Aus der Mini-Pause ins Pausenmenü wechseln
      this.hv.miniPaused = false;
      this.app.audio.play('ui');
      return;
    }
    this.hv.miniPaused = false;
    this.sim.setPaused(!this.sim.s.paused);
    this.app.audio.play('ui');
  }

  /** Mini-Pause: anhalten ohne Menü (Bauen, Aufwerten, Fähigkeiten bleiben bedienbar). */
  miniPause(): void {
    if (this.sim.isOver()) return;
    if (this.sim.s.paused) {
      this.hv.miniPaused = false;
      this.sim.setPaused(false);
    } else {
      this.hv.miniPaused = true;
      this.sim.setPaused(true);
    }
    this.app.audio.play('ui');
  }

  cycleSpeed(): void {
    const s = this.sim.s.speed;
    const list = SIM.speeds as readonly number[];
    const i = list.indexOf(s);
    this.sim.setSpeed(list[(i + 1) % list.length]);
    this.app.audio.play('ui');
  }

  chooseModifier(id: ModifierId | null): void {
    this.result(this.sim.chooseModifier(id), 'upgrade');
  }

  // ------------------------------------------------------------ Helden & Turmfähigkeiten

  /** Held, auf den sich A/S/D/V beziehen: ausgewählter oder einziger aktiver Held. */
  private focusHero() {
    const hs = this.sim.s.heroes;
    const sel = hs.find((x) => x.uid === this.ui.selectedHero && x.state === 'active');
    if (sel) return sel;
    const active = hs.filter((x) => x.state === 'active');
    return active.length >= 1 ? active[0] : undefined;
  }

  private setPlacing(id: HeroId | null): void {
    this.ui.heroPlacing = id;
    this.hv.placingHero = id;
    this.ui.hoverCell = null;
    this.hud.forcePanelRefresh();
  }

  private selectHero(uid: number): void {
    if (this.ui.selectedHero === uid) return;
    this.ui.selectedHero = uid;
    this.hv.selectedHero = uid;
    if (uid) {
      this.select(0);
      this.hv.infoEnemy = null;
    }
    this.hud.forcePanelRefresh();
  }

  heroCard(id: HeroId): void {
    const hero = this.sim.s.heroes.find((x) => x.id === id);
    if (!hero) return;
    if (hero.state === 'ready') {
      if (this.ui.heroPlacing === id) {
        this.setPlacing(null);
        return;
      }
      if (this.sim.s.riftEnergy < HEROES[id].cost) {
        this.app.audio.play('error');
        this.app.toast(`Nicht genug Rissenergie (${Math.floor(this.sim.s.riftEnergy)}/${HEROES[id].cost}).`, 'warn', 1800);
        return;
      }
      this.selectBuild(null);
      this.select(0);
      this.selectHero(0);
      this.setPlacing(id);
      this.app.audio.play('ui');
      return;
    }
    if (hero.state === 'active') {
      this.setPlacing(null);
      const willSelect = this.ui.selectedHero !== hero.uid;
      this.selectHero(willSelect ? hero.uid : 0);
      if (willSelect) {
        const c = this.app.view.cam;
        c.tx = hero.x;
        c.tz = hero.y;
        c.clamp();
      }
      this.app.audio.play('select');
      return;
    }
    this.app.toast(`${HEROES[id].name} erholt sich noch ${Math.ceil(hero.cooldown)} s.`, 'info', 1500);
  }

  heroMode(id: HeroId, mode: HeroMode): void { this.result(this.sim.heroMode(id, mode), 'ui'); }
  heroAbility(id: HeroId, slot: 0 | 1 | 2): void { this.result(this.sim.heroAbility(id, slot)); }
  heroPerk(id: HeroId, choice: 0 | 1): void { this.result(this.sim.heroPerk(id, choice), 'upgrade'); }
  towerActive(uid: number): void { this.result(this.sim.towerActive(uid)); }
  fuse(uid: number, partner: number): void {
    const r = this.sim.fuse(uid, partner);
    this.result(r);
    if (r.ok) this.select(uid);
  }
  lookAt(x: number, z: number): void {
    const c = this.app.view.cam;
    c.tx = x;
    c.tz = z;
    c.clamp();
  }

  rotate(dir: number): void {
    if (this.app.save.settings.lockRotation) {
      this.app.toast('Kameradrehung ist gesperrt (Schloss-Knopf rechts unten)', 'info', 1200);
      return;
    }
    this.app.view.cam.rotate(dir);
  }
  /** Freie Kameradrehung (mittlere Maustaste, Strg/Alt + Ziehen, Zwei-Finger-Drehung, Tasten , . Bild↑ Bild↓). */
  orbit(dYaw: number, dPitch: number): void {
    if (this.app.save.settings.lockRotation) {
      const now = performance.now();
      if (now - this.lockToastAt > 2500) {
        this.lockToastAt = now;
        this.app.toast('Kameradrehung ist gesperrt (Schloss-Knopf rechts unten)', 'info', 1200);
      }
      return;
    }
    this.app.view.cam.orbit(dYaw, dPitch);
  }
  private lockToastAt = 0;
  resetView(): void {
    this.app.view.cam.resetView();
    this.app.toast('Blickwinkel zurückgesetzt', 'info', 900);
  }
  zoom(f: number): void {
    const c = this.app.view.cam;
    c.zoomBy(f, c.width / 2, c.height / 2);
  }
  fit(): void { this.app.view.cam.fit(); this.app.view.cam.tx = this.sim.s.map.width / 2; this.app.view.cam.tz = this.sim.s.map.height / 2; }

  toggleRanges(): void {
    this.ui.showRanges = !this.ui.showRanges;
    this.hv.showRanges = this.ui.showRanges;
    this.app.save.settings.showRanges = this.ui.showRanges;
    this.app.persist();
    this.app.toast(this.ui.showRanges ? 'Alle Reichweiten sichtbar' : 'Reichweiten ausgeblendet', 'info', 1200);
  }

  restart(): void {
    const s = this.sim.s;
    this.ended = true;
    // Helden-Endlos: der laufende Lauf zählt als beendet (Freischaltungen bleiben erhalten).
    if (s.he) {
      recordRun(this.app.save, s);
      this.app.persist();
    }
    this.app.startRun({
      heroEndless: s.he ? this.app.heroEndlessSetup(s.he.champion.id, this.app.save.progress.heroMeta.startKit) : undefined,
      mode: s.mode, seedText: s.seed.text, variant: s.map.variant, biome: s.map.biome, difficulty: s.difficulty,
      missionId: s.missionId, customMap: s.mode === 'custom' ? s.map : null, size: s.map.size ?? 'normal', heroes: s.heroes.map((x) => x.id),
    });
  }

  quit(): void {
    this.saveResume();
    this.app.go('menu');
  }

  help(): void {
    if (!this.sim.s.paused && !this.sim.isOver()) this.sim.setPaused(true);
    this.overlayScreen('help');
  }

  settings(): void {
    this.overlayScreen('settings');
  }

  /** Hilfe/Einstellungen als Overlay über der laufenden (pausierten) Partie. */
  private overlayScreen(kind: 'help' | 'settings'): void {
    openOverlay(this.app, kind, () => {
      const st = this.app.save.settings;
      this.ui.reducedMotion = st.reducedMotion;
      this.app.applySettings();
      this.app.resize();
    });
  }

  // ------------------------------------------------------------ Helden-Endlos

  /** Linksklick in der Welt: Zielwahl, Held auswählen oder bewegen. true = verarbeitet. */
  private heroTap(cx: number, cy: number): boolean {
    const he = this.sim.s.he;
    if (!he) return false;
    const c = he.champion;
    if (this.targeting >= 0) {
      this.castAt(this.targeting, cx + 0.5, cy + 0.5);
      return true;
    }
    if (Math.floor(c.x) === cx && Math.floor(c.y) === cy && c.dead <= 0) {
      this.champSelected = !this.champSelected;
      this.select(0);
      this.app.audio.play('select');
      return true;
    }
    if (this.sim.towerAt(cx, cy)) return false; // Turm auswählen (Standardverhalten)
    // Hindernis anklicken: Abbau-Panel
    if (this.sim.s.map.cells[cy * this.sim.s.map.width + cx] === Cell.Blocked && !(he.shop.x === cx && he.shop.y === cy)) {
      this.select(0);
      this.hv.selectedObstacle = cy * this.sim.s.map.width + cx;
      this.hud.forcePanelRefresh();
      this.app.audio.play('select');
      return true;
    }
    if (he.shop.x === cx && he.shop.y === cy) {
      this.moveToShop();
      this.heroHud?.toggle('shop', 'potions');
      return true;
    }
    if (championWalkable(this.sim, cx, cy)) {
      this.select(0);
      this.moveChampionTo(cx, cy);
      return true;
    }
    return false;
  }

  private holdHintAt = 0;

  mine(cell: number): void {
    const w = this.sim.s.map.width;
    const cx = cell % w;
    const cy = Math.floor(cell / w);
    const r = this.sim.mine(cx, cy);
    if (!r.ok) {
      this.app.audio.play('error');
      this.app.toast(r.reason, 'warn', 1800);
      return;
    }
    if (r.walk) {
      // Zum nächsten begehbaren Nachbarfeld laufen; der Abbau beginnt bei Ankunft
      const he = this.sim.s.he;
      const c = he?.champion;
      const opts = [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, 1], [1, -1], [-1, -1]]
        .map(([dx, dy]) => ({ x: cx + dx, y: cy + dy }))
        .filter((p) => championWalkable(this.sim, p.x, p.y))
        .sort((a, b) => (c ? (a.x + 0.5 - c.x) ** 2 + (a.y + 0.5 - c.y) ** 2 - ((b.x + 0.5 - c.x) ** 2 + (b.y + 0.5 - c.y) ** 2) : 0));
      const target = he?.mineTarget ?? -1;
      let moved = false;
      for (const p of opts) {
        if (this.sim.moveChampion(p.x, p.y).ok) {
          moved = true;
          break;
        }
      }
      if (he) he.mineTarget = moved ? target : -1;
      if (!moved) {
        this.app.toast('Das Hindernis ist für den Helden nicht erreichbar.', 'warn', 1800);
        return;
      }
      this.app.toast('Held läuft zum Hindernis – der Abbau beginnt bei Ankunft.', 'info', 1600);
    } else this.app.toast('⛏ Abbau begonnen – nicht unterbrechbar.', 'info', 1600);
    this.app.audio.play('build');
    this.hud.forcePanelRefresh();
  }

  toggleHold(): void {
    const c = this.sim.s.he?.champion;
    if (!c) return;
    const on = !c.holdPos;
    this.sim.holdPosition(on);
    this.app.audio.play('ui');
    this.app.toast(on ? '⚓ Held festgesetzt – Klicks bewegen ihn nicht (Z löst)' : 'Held folgt wieder Laufbefehlen', 'info', 1500);
  }

  private moveChampionTo(cx: number, cy: number): void {
    if (this.sim.s.he?.champion.holdPos) {
      const now = performance.now();
      if (now - this.holdHintAt > 2500) {
        this.holdHintAt = now;
        this.app.toast('Held ist festgesetzt (Z oder ⚓ löst)', 'info', 1200);
      }
      return;
    }
    const r = this.sim.moveChampion(cx, cy);
    if (!r.ok) {
      this.app.audio.play('error');
      this.app.toast(r.reason, 'warn', 1500);
    }
  }

  private heroKey(e: KeyboardEvent, k: string): boolean {
    const hh = this.heroHud;
    if (!hh) return false;
    if (e.shiftKey && /^Digit[1-5]$/.test(e.code)) {
      e.preventDefault();
      const he = this.sim.s.he;
      const n = Number(e.code.slice(5));
      const p = n === 5 ? he?.champion.specialPotion?.potion : he?.champion.potions[n - 1]?.potion;
      if (p) this.usePotion(p);
      else this.app.toast(n === 5 ? 'Kein Trank im Spezialslot.' : `Kein Trank auf Platz ${n}.`, 'info', 1200);
      return true;
    }
    const slot = ['a', 's', 'd', 'w'].indexOf(k);
    if (slot >= 0) {
      e.preventDefault();
      this.championSkillKey(slot);
      return true;
    }
    switch (k) {
      case 'tab': e.preventDefault(); hh.toggle('sheet'); return true;
      case 'x': e.preventDefault(); hh.toggle('shop'); return true;
      case 'y': e.preventDefault(); this.toggleFollow(); return true;
      case 'v': e.preventDefault(); this.toggleTopDown(); return true;
      case 'z': e.preventDefault(); this.toggleHold(); return true;
      default: return false;
    }
  }

  private setTargeting(slot: number): void {
    this.targeting = slot;
    if (this.heroHud) this.heroHud.targeting = slot;
    const he = this.sim.s.he;
    if (slot < 0 || !he) {
      this.ui.abilityRadius = 0;
      this.ui.champCastRange = 0;
      return;
    }
    const sk = CHAMPIONS[he.champion.id].skills[slot];
    this.ui.abilityRadius = sk.target === 'point' ? sk.radius : 0.6;
    this.ui.champCastRange = sk.castRange;
  }

  private castAt(slot: number, x: number, y: number): void {
    const r = this.sim.championSkill(slot, x, y);
    if (!r.ok) {
      this.app.audio.play('error');
      this.app.toast(r.reason, 'warn', 1500);
      return;
    }
    this.app.audio.play('heroAbility', 0.8);
    this.setTargeting(-1);
  }

  championSkillKey(slot: number): void {
    const he = this.sim.s.he;
    if (!he) return;
    const sk = CHAMPIONS[he.champion.id].skills[slot];
    if (!sk) return;
    const chk = skillState(this.sim, slot);
    if (!chk.ok) {
      this.app.audio.play('error');
      this.app.toast(`${sk.name}: ${chk.reason ?? 'nicht bereit'}`, 'warn', 1400);
      return;
    }
    if (sk.target === 'self') {
      this.castAt(slot, he.champion.x, he.champion.y);
      return;
    }
    // Zweiter Tastendruck während der Zielwahl: auf das Feld unter dem Mauszeiger wirken
    if (this.targeting === slot && this.ui.hoverCell) {
      this.castAt(slot, this.ui.hoverCell.x + 0.5, this.ui.hoverCell.y + 0.5);
      return;
    }
    this.selectBuild(null);
    this.setTargeting(slot);
    this.app.toast(`${sk.name}: ${sk.target === 'tower' ? 'Turm' : 'Zielbereich'} anklicken (Esc/Rechtsklick bricht ab)`, 'info', 1800);
  }

  private heResult(r: { ok: boolean; reason?: string }, sfx: SfxId = 'ui'): void {
    if (!r.ok) {
      this.app.audio.play('error');
      this.app.toast(r.reason ?? 'Nicht möglich', 'warn', 1800);
    } else this.app.audio.play(sfx);
    this.heroHud?.refresh();
    this.saveResumeIfBuild();
  }

  usePotion(potion: PotionId): void {
    const r = this.sim.usePotion(potion);
    this.heResult(r, 'heal');
    if (r.ok) this.app.toast(`${POTIONS[potion].icon} ${POTIONS[potion].name}`, 'info', 900);
  }
  slotPotion(potion: PotionId): void { this.heResult(this.sim.slotPotion(potion)); }
  learnSkill(nodeId: string): void { this.heResult(this.sim.learnSkill(nodeId), 'upgrade'); }
  paragonPoint(nodeId: string): void {
    this.heResult(this.sim.paragonPoint(nodeId), 'upgrade');
    commitHeroProgress(this.app.save, this.sim.s);
    this.app.persist();
  }
  socketRune(towerUid: number, runeUid: number): void {
    this.result(this.sim.socketRune(towerUid, runeUid), 'upgrade');
  }
  unsocketRune(runeUid: number): void {
    this.result(this.sim.unsocketRune(runeUid), 'ui');
  }
  treeReset(): void {
    this.heResult(this.sim.treeReset(), 'sell');
  }

  paragonReset(): void {
    this.heResult(this.sim.paragonReset(), 'ui');
    commitHeroProgress(this.app.save, this.sim.s);
    this.app.persist();
  }
  equip(uid: number): void { this.heResult(this.sim.equip(uid), 'build'); }
  unequip(slot: number): void { this.heResult(this.sim.unequip(slot)); }
  shopBuy(offerId: string): void { this.heResult(this.sim.shopBuy(offerId), 'gold'); }
  shopBuyItem(item: string): void { this.heResult(this.sim.shopBuyItem(item), 'gold'); }
  shopSell(uid: number): void { this.heResult(this.sim.shopSell(uid), 'sell'); }
  craft(recipeId: string): void { this.heResult(this.sim.craft(recipeId), 'fusion'); }
  upgradeItem(uid: number): void { this.heResult(this.sim.upgradeItem(uid), 'upgrade'); }

  focusChampion(): void {
    const c = this.sim.s.he?.champion;
    if (!c) return;
    this.champSelected = true;
    this.lookAt(c.dead > 0 ? this.sim.s.map.goal.x + 0.5 : c.x, c.dead > 0 ? this.sim.s.map.goal.y + 0.5 : c.y);
    this.app.audio.play('select');
  }

  toggleFollow(): void {
    if (!this.heroHud) return;
    this.heroHud.follow = !this.heroHud.follow;
    this.app.toast(this.heroHud.follow ? 'Kamera folgt dem Helden (Y)' : 'Kamera frei', 'info', 1100);
  }

  toggleTopDown(): void {
    const on = this.app.view.cam.toggleTopDown();
    this.app.toast(on ? 'Draufsicht (V)' : 'Isometrische Ansicht (V)', 'info', 1000);
  }

  moveToShop(): void {
    const he = this.sim.s.he;
    if (!he) return;
    const opts = [[1, 0], [-1, 0], [0, 1], [0, -1], [1, 1], [-1, 1], [1, -1], [-1, -1]]
      .map(([dx, dy]) => ({ x: he.shop.x + dx, y: he.shop.y + dy }))
      .filter((p) => championWalkable(this.sim, p.x, p.y))
      .sort((a, b) => (a.x + 0.5 - he.champion.x) ** 2 + (a.y + 0.5 - he.champion.y) ** 2 - ((b.x + 0.5 - he.champion.x) ** 2 + (b.y + 0.5 - he.champion.y) ** 2));
    for (const p of opts) if (this.sim.moveChampion(p.x, p.y).ok) return;
    this.app.toast('Der Laden ist gerade nicht erreichbar.', 'warn', 1500);
  }

  tutorialDone(): void {
    this.app.save.settings.tutorialSeen = true;
    this.app.persist();
  }
}
