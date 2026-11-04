import type { MapDefinition } from '../core/types.ts';
import { BIOMES } from '../config/biomes.ts';
import { AudioSystem } from '../audio/AudioSystem.ts';
import { randomSeedText } from '../core/rng.ts';
import type { GameState, SaveData } from '../core/types.ts';
import { SKINS } from '../config/achievements.ts';
import { IconFactory } from '../render/IconFactory.ts';
import { Renderer } from '../render/Renderer.ts';
import { Bot, BOT_PROFILES } from '../sim/Bot.ts';
import { WorldView, type ViewUiState } from '../render/WorldView.ts';
import { deserializeRun, SaveSystem, type KeyValueStore } from '../save/SaveSystem.ts';
import { GameSimulation } from '../sim/GameSimulation.ts';
import type { RunSetup } from '../sim/setup.ts';
import { clear, h } from '../ui/dom.ts';
import { renderHelp } from '../ui/screens/help.ts';
import { renderMenu } from '../ui/screens/menu.ts';
import { renderRecords } from '../ui/screens/records.ts';
import { renderResults, type ResultParams } from '../ui/screens/results.ts';
import { renderSettings } from '../ui/screens/settings.ts';
import { renderHeroSetup } from '../ui/screens/heroSetup.ts';
import { renderEditor } from '../editor/MapEditor.ts';
import { commitHeroRun } from './progress.ts';
import { setKeyBindings } from '../config/keys.ts';
import type { ChampionId, ItemId } from '../core/types.ts';
import { GameSession } from './GameSession.ts';

// GameApplication: Start, Szenenwechsel, Lebenszyklus. Besitzt Renderer, Audio und Speicher.
// Menüs zeigen im Hintergrund eine generierte Welt (nur Darstellung, keine Wellen).

export type ScreenId = 'menu' | 'heroSetup' | 'settings' | 'records' | 'help' | 'editor' | 'results' | 'game';

export interface ScreenParams {
  results?: ResultParams;
  helpTab?: string;
  /** 3.10: Karte aus dem Editor, die im Helden-Endlos gespielt werden soll. */
  heroMap?: MapDefinition;
  back?: ScreenId;
}

function safeStorage(): KeyValueStore | null {
  try {
    const ls = window.localStorage;
    const k = '__kw_probe__';
    ls.setItem(k, '1');
    ls.removeItem(k);
    return ls;
  } catch {
    return null;
  }
}

export class GameApplication {
  readonly canvas: HTMLCanvasElement;
  readonly ui: HTMLElement;
  readonly renderer: Renderer;
  readonly view: WorldView;
  readonly audio = new AudioSystem();
  readonly icons = new IconFactory();
  private bgBot: Bot | null = null;
  private bgTimer = 0;
  readonly saves: SaveSystem;
  save: SaveData;
  loadError: string | null;
  screen: ScreenId = 'menu';
  session: GameSession | null = null;
  private bgSim: GameSimulation | null = null;
  private raf = 0;
  private last = 0;
  private bgSpin = 0;
  private toastBox: HTMLElement;

  constructor(canvas: HTMLCanvasElement, ui: HTMLElement) {
    this.canvas = canvas;
    this.ui = ui;
    this.renderer = new Renderer(canvas);
    this.view = new WorldView(this.renderer);
    this.saves = new SaveSystem(safeStorage());
    const res = this.saves.load();
    this.save = res.data;
    this.loadError = res.error;
    this.toastBox = h('div', { class: 'toasts', 'aria-live': 'polite' });
    document.body.appendChild(this.toastBox);
    this.applySettings();
    // Audio erst nach einer Nutzeraktion entsperren (Autoplay-Richtlinien).
    const unlock = (): void => {
      this.audio.unlock();
      this.audio.startMusic();
    };
    window.addEventListener('pointerdown', unlock, { passive: true });
    window.addEventListener('keydown', unlock);
    window.addEventListener('resize', () => this.resize());
    // 2.9.5: Kein automatisches Pausieren mehr bei Tabwechsel/Fokusverlust. Im Hintergrund drosselt der Browser
    // requestAnimationFrame auf 0 – ein Zeitgeber rechnet die Simulation dann ohne Darstellung weiter.
    document.addEventListener('visibilitychange', () => {
      if (document.hidden) {
        this.hiddenLast = performance.now();
        if (!this.hiddenTimer) this.hiddenTimer = window.setInterval(() => this.hiddenTick(), 250);
      } else {
        if (this.hiddenTimer) window.clearInterval(this.hiddenTimer);
        this.hiddenTimer = 0;
        this.last = performance.now();
      }
    });
    this.resize();
  }

  // ------------------------------------------------------------ Speicher & Einstellungen

  persist(): void {
    if (!this.saves.save(this.save)) {
      // Kein stilles Scheitern: einmalig sichtbar machen.
      if (!this.loadError) this.toast('Speichern nicht möglich – lokaler Speicher blockiert.', 'warn');
    }
  }

  applySettings(): void {
    const s = this.save.settings;
    setKeyBindings(s.keys);
    this.audio.setSettings({ master: s.masterVolume, music: s.musicVolume, sfx: s.sfxVolume, muted: s.muted });
    this.applyQuality();
    const skin = SKINS.find((k) => k.id === s.skin) ?? SKINS[0];
    this.view.setSkin({ tint: skin.tint, tintAmount: skin.tintAmount, stone: skin.stone });
    this.icons.stoneColor = skin.stone;
    document.documentElement.style.setProperty('--ui-scale', String(s.uiScale));
    document.documentElement.classList.toggle('reduced-motion', s.reducedMotion);
  }

  pixelRatio(): number {
    const q = this.save.settings.quality;
    const dpr = window.devicePixelRatio || 1;
    if (q === 'low') return Math.min(1, dpr) * 0.75;
    if (q === 'medium') return Math.min(1.5, dpr);
    if (q === 'high') return Math.min(2.5, dpr);
    return Math.min(2, dpr) * (this.session?.adaptiveScale ?? 1);
  }

  /** Automatische Qualitätsstufe (0 = volle Qualität); steigt bei dauerhaft niedriger Bildrate. */
  autoTier = 0;
  static readonly AUTO_TIERS = [
    { msaa: 4, shadowSize: 2048, bloom: true },
    { msaa: 2, shadowSize: 1024, bloom: true },
    { msaa: 0, shadowSize: 1024, bloom: true },
    { msaa: 0, shadowSize: 0, bloom: true },
    { msaa: 0, shadowSize: 0, bloom: false },
  ];

  applyQuality(): void {
    const q = this.save.settings.quality;
    const cine = this.save.settings.cinematicFx !== false;
    const base = q === 'low' ? { msaa: 0, shadowSize: 0, bloom: false }
      : q === 'medium' ? { msaa: 2, shadowSize: 1024, bloom: true }
        : q === 'high' ? { msaa: 4, shadowSize: 2048, bloom: true }
          : GameApplication.AUTO_TIERS[Math.min(this.autoTier, GameApplication.AUTO_TIERS.length - 1)];
    // 3.13: Tiefenunschärfe nur, wo auch Glühen läuft (nicht auf „Niedrig“ bzw. der untersten Automatikstufe)
    this.renderer.setQuality({ ...base, dof: cine && base.bloom });
  }

  /** Senkt die automatische Qualität um eine Stufe. Liefert false, wenn bereits die niedrigste Stufe aktiv ist. */
  lowerAutoQuality(): boolean {
    if (this.autoTier >= GameApplication.AUTO_TIERS.length - 1) return false;
    this.autoTier++;
    this.applyQuality();
    return true;
  }

  /** Detailgrad der Geländeausstattung (Gras, Randsteine). */
  detailLevel(): number {
    const q = this.save.settings.quality;
    return q === 'low' ? 0 : q === 'medium' ? 1 : 2;
  }

  particleCap(): number {
    const q = this.save.settings.quality;
    if (q === 'low') return 150;
    if (q === 'medium') return 450;
    if (q === 'high') return 1100;
    return this.session && this.session.adaptiveScale < 0.9 ? 300 : 700;
  }

  resize(): void {
    const r = this.canvas.getBoundingClientRect();
    const w = Math.max(1, r.width);
    const hh = Math.max(1, r.height);
    this.renderer.resize(w, hh, this.pixelRatio());
    this.view.cam.resize(w, hh);
  }

  toast(text: string, severity: 'info' | 'warn' | 'danger' = 'info', ms = 3200): void {
    const el = h('div', { class: `toast ${severity}`, role: 'status' }, severity === 'danger' ? '⚠ ' : severity === 'warn' ? '! ' : '', text);
    this.toastBox.appendChild(el);
    while (this.toastBox.children.length > 4) this.toastBox.firstElementChild?.remove();
    window.setTimeout(() => el.classList.add('out'), ms);
    window.setTimeout(() => el.remove(), ms + 400);
  }

  // ------------------------------------------------------------ Szenen

  go(screen: ScreenId, params: ScreenParams = {}): void {
    if (screen !== 'game' && this.session) {
      this.session.dispose();
      this.session = null;
    }
    this.screen = screen;
    clear(this.ui);
    document.body.dataset.screen = screen;
    if (screen !== 'game') {
      document.body.appendChild(this.canvas);
      this.canvas.className = 'bg-canvas';
      this.ensureBackground();
      this.audio.intensity = 0;
    }
    let el: HTMLElement;
    switch (screen) {
      case 'menu': el = renderMenu(this); break;
      case 'heroSetup': el = renderHeroSetup(this, params.heroMap ?? null); break;
      case 'editor': el = renderEditor(this); break;
      case 'settings': el = renderSettings(this, params.back ?? 'menu'); break;
      case 'records': el = renderRecords(this); break;
      case 'help': el = renderHelp(this, params.helpTab ?? 'controls', params.back ?? 'menu'); break;
      case 'results': el = renderResults(this, params.results as ResultParams); break;
      default: el = h('div');
    }
    this.ui.appendChild(el);
    requestAnimationFrame(() => {
      this.resize();
      const first = el.querySelector<HTMLElement>('[data-autofocus]') ?? el.querySelector<HTMLElement>('button');
      first?.focus({ preventScroll: true });
    });
    this.loop();
  }

  /** Hintergrundwelt der Menüs (rein kosmetisch). */
  private ensureBackground(): void {
    if (this.bgSim && !this.bgSim.isOver()) {
      this.view.setMap(this.bgSim.s.map, this.detailLevel());
      return;
    }
    // Lebendige Menüwelt: ein Bot spielt im Hintergrund eine echte Partie (ohne Eingaben, ohne Speichern).
    const seed = randomSeedText((Date.now() ^ (Math.random() * 1e9)) & 0xffffff);
    this.bgSim = GameSimulation.create({ mode: 'quick', seedText: seed, variant: 'classic', biome: 'random', difficulty: 'easy', heroes: ['blade', 'bard'] });
    this.bgSim.s.gold += 600;
    this.bgBot = new Bot(this.bgSim, BOT_PROFILES[(Date.now() >> 4) % BOT_PROFILES.length]);
    this.bgTimer = 0;
    this.view.setMap(this.bgSim.s.map, this.detailLevel());
    this.audio.setMusicTheme(BIOMES[this.bgSim.s.map.biome].musicRoot, BIOMES[this.bgSim.s.map.biome].scale);
  }

  private hiddenTimer = 0;
  private hiddenLast = 0;

  /** Hintergrund-Takt (Tab verborgen): nur Simulation, keine Darstellung. */
  private hiddenTick(): void {
    const now = performance.now();
    const dt = (now - this.hiddenLast) / 1000;
    this.hiddenLast = now;
    if (this.screen !== 'game' || !this.session) return;
    try {
      this.session.simOnly(Math.min(dt, 60));
    } catch (err) {
      console.error('[Hintergrund]', err);
    }
  }

  private loop(): void {
    if (this.raf) return;
    this.last = performance.now();
    const tick = (now: number): void => {
      this.raf = 0;
      const dt = Math.min(0.1, Math.max(0, (now - this.last) / 1000));
      this.last = now;
      try {
        if (this.screen === 'game' && this.session) this.session.frame(dt);
        else this.backgroundFrame(dt);
      } catch (err) {
        console.error('[Frame]', err);
        this.toast(`Darstellungsfehler: ${err instanceof Error ? err.message : String(err)}`, 'danger', 6000);
      }
      this.raf = requestAnimationFrame(tick);
    };
    this.raf = requestAnimationFrame(tick);
  }

  private backgroundFrame(dt: number): void {
    const sim = this.bgSim;
    if (!sim) return;
    const reduced = this.save.settings.reducedMotion;
    // Bot spielt mit; nach Sieg/Niederlage startet eine neue Welt
    if (this.bgBot && !reduced) {
      this.bgTimer -= dt;
      if (this.bgTimer <= 0) {
        this.bgTimer = 0.5;
        try {
          this.bgBot.act();
        } catch {
          this.bgBot = null;
        }
      }
      sim.advance(dt);
      const events = sim.drainEvents();
      if (events.length) this.view.handleEvents(events, sim, this.bgUi);
      if (sim.isOver()) {
        this.bgSpin += dt;
        if (this.bgSpin > 4) {
          this.bgSpin = 0;
          this.bgSim = null;
          this.ensureBackground();
          return;
        }
      }
    }
    this.bgUi.reducedMotion = reduced;
    this.bgUi.cinematic = !reduced;
    this.view.frame(sim, this.bgUi, dt);
  }

  private bgUi: ViewUiState = {
    buildDef: null, hoverCell: null, preview: null, selectedTower: 0, showRanges: false, showGrid: false,
    reducedMotion: false, particleCap: 250, cinematic: true,
  };

  // ------------------------------------------------------------ Partien

  /** Baut die Startdaten eines Helden-Endloslaufs aus dem gespeicherten Fortschritt (neue runId). */
  heroEndlessSetup(champion: ChampionId, startKit: ItemId[]): NonNullable<RunSetup['heroEndless']> {
    const m = this.save.progress.heroMeta;
    return {
      champion,
      startKit: [...startKit],
      meta: {
        bestWave: m.bestWave, unlockedItems: [...m.unlockedItems], fragments: { ...m.fragments }, passive: { ...m.passive }, campaignDone: [...this.save.progress.campaignCompleted],
        // Gemeinsame Heldenstufe für alle Helden, Skillbaum je Held
        champ: { level: m.heroLevel.level, xp: m.heroLevel.xp, tree: { ...(m.champions[champion]?.tree ?? {}) } },
        paragon: { level: m.paragon.level, xp: m.paragon.xp, points: { ...m.paragon.points } },
        mastery: { ...m.towerMastery },
      },
      runId: `he-${Date.now().toString(36)}-${Math.floor(Math.random() * 1e9).toString(36)}`,
    };
  }

  /** Ein noch offener (fortsetzbarer) Helden-Endloslauf wird verbucht, bevor er überschrieben wird. */
  private commitPendingHeroRun(): void {
    if (!this.save.resume) return;
    const { state } = deserializeRun(this.save.resume);
    if (state && state.mode === 'heroEndless') commitHeroRun(this.save, state);
  }

  startRun(setup: RunSetup): void {
    let sim: GameSimulation;
    try {
      sim = GameSimulation.create(setup);
    } catch (err) {
      this.toast(`Partie konnte nicht gestartet werden: ${err instanceof Error ? err.message : String(err)}`, 'danger', 6000);
      return;
    }
    this.save.settings.lastVariant = setup.variant;
    this.save.settings.lastDifficulty = setup.difficulty;
    this.save.settings.lastBiome = setup.biome;
    if (setup.heroEndless) {
      this.save.progress.heroMeta.lastChampion = setup.heroEndless.champion;
      this.save.progress.heroMeta.startKit = [...setup.heroEndless.startKit];
    }
    this.commitPendingHeroRun();
    this.save.resume = null;
    this.persist();
    this.enterSession(sim);
  }

  canResume(): { state: GameState | null; error: string | null } {
    if (!this.save.resume) return { state: null, error: null };
    const r = deserializeRun(this.save.resume);
    // 3.9: nur noch Helden-Endlos – alte Spielstände anderer Modi werden nicht mehr angeboten
    if (r.state && r.state.mode !== 'heroEndless') return { state: null, error: null };
    return r;
  }

  resumeRun(): void {
    const { state, error } = this.canResume();
    if (!state) {
      this.toast(`Spielstand kann nicht fortgesetzt werden: ${error ?? 'unbekannt'}. Der Eintrag bleibt erhalten, bis eine neue Partie startet.`, 'warn', 6000);
      return;
    }
    this.enterSession(new GameSimulation(state));
  }

  private enterSession(sim: GameSimulation): void {
    if (this.session) {
      this.session.dispose();
      this.session = null;
    }
    this.go('game');
    this.session = new GameSession(this, sim);
    const b = BIOMES[sim.s.map.biome];
    this.audio.setMusicTheme(b.musicRoot, b.scale);
  }
}
