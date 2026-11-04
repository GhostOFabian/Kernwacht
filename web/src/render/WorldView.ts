import { BIOMES } from '../config/biomes.ts';
import { ABILITIES, BOSSES, ENEMIES, MUTATIONS } from '../config/enemies.ts';
import { HEROES } from '../config/heroes.ts';
import { CHAMPIONS } from '../config/champions.ts';
import { RESOURCES } from '../config/heroEndless.ts';
import { ITEMS, POTIONS, RARITY } from '../config/items.ts';
import { championStats } from '../sim/ChampionSystem.ts';
import { TOWERS, towerCenter, towerSize } from '../config/towers.ts';
import { Cell, type BiomeId, type BossId, type EnemyId, type EnemyInstance, type GameEvent, type GridPos, type HeroId, type HeroInstance, type MapDefinition, type StatusId, type TowerId, type ZoneKind } from '../core/types.ts';
import type { GameSimulation } from '../sim/GameSimulation.ts';
import type { BuildCheck } from '../sim/EconomySystem.ts';
import { canPlaceHero, heroStatsOf } from '../sim/HeroSystem.ts';
import { teslaPairs } from '../sim/TowerSystem.ts';
import { spawnsOf } from '../sim/WorldGenerator.ts';
import { IsoCamera } from './Camera.ts';
import { Fx, jag } from './fx.ts';
import { hexToRgb, lookAt, mat4, mixColor, multiply, ortho, shade } from './math3d.ts';
import { BOSS_HEIGHT, BOSS_MODELS, ENEMY_MODELS, HERO_MODELS, levelParts, specParts, TOWER_MODELS, TOWER_TOP, WEAKPOINT_POS, type Part } from './models.ts';
import { DAMAGE_COLORS, STATUS_GLYPH } from './palette.ts';
import { SHAPE, type Grade, type Lighting, type Pass, type Renderer } from './Renderer.ts';
import { DECOR_SCALE, buildTerrain, cellTop, SEA, type GlowPoint } from './terrain.ts';
import { pickGridCell } from './picking.ts';

/** 2.9: Modellmaßstab relativ zum Feld – Türme, Gegner und Helden etwas kleiner, damit Felder und Lanes luftiger wirken. */
export const UNIT = 0.8;

export { cellTop } from './terrain.ts';

// Übersetzt den Simulationszustand in Render-Instanzen und Effekte. Liest den Zustand ausschließlich.

export interface ViewUiState {
  buildDef: TowerId | null;
  /** 2.9.5: legendärer Turm wird gerade versetzt (Vorschau am Zielfeld). */
  moveDef?: TowerId | null;
  hoverCell: GridPos | null;
  preview: BuildCheck | null;
  selectedTower: number;
  showRanges: boolean;
  showGrid: boolean;
  reducedMotion: boolean;
  particleCap: number;
  selectedHero?: number;
  heroPlacing?: HeroId | null;
  abilityRadius?: number;
  highlight?: number[];
  /** Helden-Endlos: Wirkreichweite um den Helden während der Zielwahl. */
  champCastRange?: number;
  /** Beim Platzieren: Umkreis, in dem Fusionspartner stehen müssen (0 = aus). */
  fusionRing?: number;
  /** Angeklickter Gegner (UID) – Auswahlring und Infopanel. */
  selectedEnemy?: number;
  /** Helden-Endlos: Held ausgewählt (zeigt Aura und Laufziel). */
  champSelected?: boolean;
  cinematic?: boolean;
}

export interface ViewSkin { tint: number; tintAmount: number; stone: number }

interface Smooth { x: number; y: number; h: number; head: number; speed: number; def: EnemyId; boss: BossId | null; elite: boolean; seen: number }

interface Corpse { parts: Part[]; x: number; y: number; z: number; yaw: number; scale: number; color: number; t: number; max: number; boss: boolean }

interface Ambient { x: number; y: number; z: number; vx: number; vy: number; vz: number; life: number; max: number; seed: number }

export interface Floater { text: string; x: number; y: number; z: number; color: string; big?: boolean }


const REACTION_LABEL: Record<string, [string, number]> = {
  steam: ['Dampfstoß', 0xe0f0ff], thermalShock: ['Thermoschock', 0x90d8ff], overload: ['Überladung', 0xffe860],
  toxicBlaze: ['Giftbrand', 0xb0ff40], shatter: ['Resonanzbruch', 0x60e8d4],
};

const ZONE_WINDUP: Partial<Record<ZoneKind, number>> = {
  fireZone: ABILITIES.fireZones.windup, nullPulse: ABILITIES.nullPulse.windup, quake: ABILITIES.quake.windup,
  riftJump: ABILITIES.riftJump.windup, portalIn: ABILITIES.riftPortals.windup, portalOut: ABILITIES.riftPortals.windup,
  stomp: ABILITIES.stomp.windup, siegeSlam: ABILITIES.siegeSlam.windup, devour: ABILITIES.devour.windup,
};

// Tageszeit-Schlüsselbilder (Lichtfarbe, Intensität, Höhenwinkel)
const TOD: [number, number, number][] = [
  [0xffe2b8, 0.9, 0.7], [0xffffff, 1.0, 0.95], [0xffb070, 0.85, 0.55], [0x9aa8ff, 0.62, 0.75],
];

const SPRITE_ADD = true;

export class WorldView {
  readonly cam = new IsoCamera();
  readonly fx = new Fx();
  private smooth = new Map<number, Smooth>();
  private champSmooth: { x: number; y: number; h: number; speed: number } | null = null;
  private heroSmooth = new Map<number, { x: number; y: number; h: number; speed: number; born: number }>();
  private corpses: Corpse[] = [];
  private towerBorn = new Map<number, number>();
  private glows: GlowPoint[] = [];
  private ambient: Ambient[] = [];
  private spawnYaw: { x: number; y: number; yaw: number }[] = [];
  private bossSeen = new Set<number>();
  private time = 0;
  private shake = 0;
  private flash = 0;
  private mapRef: MapDefinition | null = null;
  private lighting: Lighting | null = null;
  private light = { color: [1, 1, 1] as [number, number, number], dir: [-0.45, 0.85, -0.3] as [number, number, number], sky: 0 };
  private firstFrame = true;
  private lightVPm = { view: mat4(), proj: mat4() };
  detail = 2;
  skin: ViewSkin = { tint: 0xffffff, tintAmount: 0, stone: 0x8c8678 };
  onFloater: ((f: Floater) => void) | null = null;
  onBossAppear: ((boss: BossId) => void) | null = null;

  constructor(readonly r: Renderer) {}

  private mapRev = 0;

  /** Gelände neu aufbauen (nach Abbau eines Hindernisses), ohne Kamera und Effekte zurückzusetzen. */
  rebuildTerrain(map: MapDefinition): void {
    this.mapRev = map.rev ?? 0;
    this.glows = buildTerrain(this.r, map, this.detail);
  }

  setMap(map: MapDefinition, detail = this.detail): void {
    this.mapRev = map.rev ?? 0;
    this.mapRef = map;
    this.detail = detail;
    this.cam.setMap(map.width, map.height);
    this.smooth.clear();
    this.heroSmooth.clear();
    this.corpses = [];
    this.towerBorn.clear();
    this.bossSeen.clear();
    this.fx.clear();
    this.ambient = [];
    this.firstFrame = true;
    this.glows = buildTerrain(this.r, map, detail);
    // Portalausrichtung: quer zum ersten Wegstück
    this.spawnYaw = spawnsOf(map).map((sp) => {
      let yaw = 0;
      for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]] as [number, number][]) {
        const nx = sp.x + dx;
        const ny = sp.y + dy;
        if (nx < 0 || ny < 0 || nx >= map.width || ny >= map.height) continue;
        const c = map.cells[ny * map.width + nx];
        if (c === Cell.Path || c === Cell.Goal || c === Cell.Open || c === Cell.NoBuild) {
          yaw = Math.atan2(-dy, dx);
          if (c === Cell.Path) break;
        }
      }
      return { x: sp.x + 0.5, y: sp.y + 0.5, yaw };
    });
    const pal = BIOMES[map.biome].palette;
    const sky = hexToRgb(mixColor(pal.sky, pal.fog, 0.5));
    const size = Math.max(map.width, map.height);
    this.lighting = {
      light: [-0.45, 0.85, -0.3],
      lightColor: hexToRgb(pal.light).map((v) => v * 0.85) as [number, number, number],
      sky: hexToRgb(mixColor(pal.sky, 0xffffff, 0.55)).map((v) => v * 0.55) as [number, number, number],
      ground: hexToRgb(pal.ground).map((v) => v * 0.3) as [number, number, number],
      fog: sky,
      fogCenter: [map.width / 2, map.height / 2],
      fogStart: size * 0.62,
      fogEnd: size * 0.62 + 14,
      clear: sky,
      ...moodLighting(map),
    };
    this.light.color = [...this.lighting.lightColor];
  }

  setSkin(skin: ViewSkin): void {
    this.skin = skin;
  }

  // ------------------------------------------------------------ Ereignisse → Effekte

  handleEvents(events: GameEvent[], sim: GameSimulation, ui: ViewUiState): void {
    const fx = this.fx;
    fx.intensity = ui.reducedMotion ? 0.5 : 1;
    const shake = (v: number): void => {
      if (!ui.reducedMotion) this.shake = Math.max(this.shake, v);
    };
    const gy = (x: number, y: number): number => cellTop(sim.s.map, Math.floor(x), Math.floor(y));
    for (const e of events) {
      switch (e.k) {
        case 'shot': {
          const t = sim.c.towerByUid.get(e.uid);
          const top = t ? gy(e.x, e.y) + TOWER_TOP[t.def] * 0.85 * UNIT : 1;
          const ty = gy(e.tx, e.ty) + 0.45;
          if (e.tower === 'rune' || e.tower === 'riftbinder') {
            const col = e.tower === 'rune' ? 0xc070ff : 0xff60ff;
            fx.beam(e.x, top, e.y, e.tx, ty, e.ty, col, 0.07, 0.14);
            fx.glow(e.tx, ty, e.ty, 0.5, col, 0.18);
          } else if (e.tower === 'minelayer' || e.tower === 'sporemine') {
            fx.smoke(e.x, top, e.y, 0.35, e.tower === 'sporemine' ? 0x8ad040 : 0x6a6460, 0.35, 0.6);
          } else if (e.tower === 'cannon') {
            fx.glow(e.x, top, e.y, 0.7, 0xffc060, 0.12);
            fx.smoke(e.x, top, e.y, 0.5, 0x6a6460, 0.4, 0.8);
          } else if (e.tower === 'ember') {
            fx.fire(e.x, top, e.y, 1.1);
          } else if (e.tower === 'meteor' || e.tower === 'starfall') {
            fx.glow(e.x, top + 0.4, e.y, 0.9, 0xff8030, 0.3);
            for (let i = 0; i < 4; i++) fx.emit({ x: e.x, y: top + 0.3, z: e.y, vy: 4 + Math.random() * 2, life: 0.4, c0: 0xffc060, c1: 0xff4010, s0: 0.25, s1: 0.05, shape: SHAPE.spark, stretch: 0.2 });
          } else if (t && TOWERS[t.def].attack === 'projectile') {
            fx.glow(e.x, top, e.y, 0.3, TOWERS[t.def].color, 0.1, 0.7);
          }
          break;
        }
        case 'hit': {
          const col = DAMAGE_COLORS[e.type];
          const y = gy(e.x, e.y) + 0.4;
          if (e.splash > 0.3) {
            if (e.type === 'fire') fx.explosion(e.x, gy(e.x, e.y), e.y, e.splash * 0.8, col, e.splash > 1.5);
            else {
              fx.ring(e.x, y - 0.35, e.y, e.splash, col, 0.4, 0.18);
              fx.glow(e.x, y, e.y, e.splash, col, 0.2, 0.7);
              fx.sparks(e.x, y, e.y, col, 5);
            }
          } else {
            switch (e.type) {
              case 'fire': fx.fire(e.x, y - 0.1, e.y, 0.8); break;
              case 'frost': fx.sparks(e.x, y, e.y, 0xd8f4ff, 3, 0.7, 0.1); break;
              case 'poison': for (let i = 0; i < 2; i++) fx.emit({ x: e.x, y, z: e.y, vx: (Math.random() - 0.5), vy: 1, vz: (Math.random() - 0.5), grav: 4, life: 0.4, c0: 0xb0f060, s0: 0.12, s1: 0.04 }); break;
              case 'arcane': fx.glow(e.x, y, e.y, 0.45, col, 0.16); break;
              case 'sonic': fx.ring(e.x, y - 0.3, e.y, 0.5, col, 0.25); break;
              default: fx.sparks(e.x, y, e.y, col, 2, 0.8, 0.09);
            }
          }
          break;
        }
        case 'chain': {
          const pts = e.pts;
          for (let i = 0; i + 3 < pts.length; i += 2) {
            const y0 = i === 0 ? gy(pts[0], pts[1]) + 1.25 : gy(pts[i], pts[i + 1]) + 0.45;
            const y1 = gy(pts[i + 2], pts[i + 3]) + 0.45;
            fx.bolt(pts[i], y0, pts[i + 1], pts[i + 2], y1, pts[i + 3], 0xfff090, 0.05, 0.2, 1);
            fx.glow(pts[i + 2], y1, pts[i + 3], 0.45, 0xfff090, 0.15);
          }
          break;
        }
        case 'arc':
          fx.bolt(e.x, 1.2, e.y, e.tx, 0.5, e.ty, 0x9ae0ff, 0.05, 0.16, 1);
          break;
        case 'pulse': {
          const col = e.tower === 'frost' ? 0x9ae0ff : 0x60e8d4;
          const y = gy(e.x, e.y);
          fx.ring(e.x, y, e.y, e.r, col, 0.5, 0.1);
          if (e.tower === 'frost') {
            for (let i = 0; i < 10 * fx.intensity; i++) {
              const a = Math.random() * Math.PI * 2;
              fx.emit({ shape: SHAPE.star, x: e.x + Math.cos(a) * e.r * 0.3, y: y + 0.3, z: e.y + Math.sin(a) * e.r * 0.3, vx: Math.cos(a) * e.r, vz: Math.sin(a) * e.r, vy: 0.3, drag: 2, life: 0.6, c0: 0xffffff, c1: 0x9ae0ff, s0: 0.14, s1: 0.04 });
            }
          } else fx.ring(e.x, y, e.y, e.r * 0.7, col, 0.6, 0.06);
          break;
        }
        case 'gravity': {
          const y = gy(e.x, e.y);
          fx.emit({ shape: SHAPE.ring, flat: true, x: e.x, y: y + 0.05, z: e.y, life: 0.5, c0: 0x8a6aff, s0: e.r, s1: 0.2, seed: 0.15 });
          for (let i = 0; i < 10 * fx.intensity; i++) {
            const a = Math.random() * Math.PI * 2;
            const d = e.r * (0.6 + Math.random() * 0.4);
            fx.emit({ x: e.x + Math.cos(a) * d, y: y + 0.3, z: e.y + Math.sin(a) * d, vx: -Math.cos(a) * d * 1.8, vz: -Math.sin(a) * d * 1.8, life: 0.5, c0: 0xb090ff, c1: 0x301060, s0: 0.2, s1: 0.05 });
          }
          break;
        }
        case 'breath': {
          const t0 = 1.6;
          const dx = e.tx - e.x;
          const dz = e.ty - e.y;
          for (let i = 0; i < 14 * fx.intensity; i++) {
            const s = 1.6 + Math.random() * 1.2;
            fx.emit({
              x: e.x, y: t0, z: e.y, vx: dx * s + (Math.random() - 0.5), vy: -t0 * s * 0.8, vz: dz * s + (Math.random() - 0.5),
              life: 0.5, c0: 0xfff0a0, c1: 0xc02000, s0: 0.18, s1: 0.45, drag: 1,
            });
          }
          break;
        }
        case 'meteor':
          fx.explosion(e.x, gy(e.x, e.y), e.y, e.r, 0xff6a20, e.big);
          if (e.big) {
            shake(0.25);
            this.flash = Math.max(this.flash, 0.12);
          }
          break;
        case 'burst':
          fx.explosion(e.x, gy(e.x, e.y), e.y, e.r, 0xff8030, e.r > 1.2);
          shake(0.1);
          break;
        case 'kill': {
          this.onFloater?.({ text: `+${e.gold}`, x: e.x, y: 0.9, z: e.y, color: '#ffd84a', big: e.boss || e.elite });
          if (e.boss) {
            shake(0.6);
            this.flash = Math.max(this.flash, 0.5);
          }
          break;
        }
        case 'death':
          this.addCorpse(sim, e.x, e.y, e.enemy, e.boss, e.color, e.size);
          break;
        case 'leak':
          if (this.mapRef) {
            const g = this.mapRef.goal;
            fx.glow(g.x + 0.5, 1, g.y + 0.5, 2.2, 0xff3030, 0.4);
            fx.ring(g.x + 0.5, 0, g.y + 0.5, 2.2, 0xff4040, 0.5, 0.2);
            fx.sparks(g.x + 0.5, 1, g.y + 0.5, 0xff6060, 10, 1.4);
            this.onFloater?.({ text: `−${e.lives} ♥`, x: g.x + 0.5, y: 1.6, z: g.y + 0.5, color: '#ff5a5a', big: true });
          }
          shake(0.2);
          break;
        case 'reaction': {
          const [label, col] = REACTION_LABEL[e.id] ?? [e.id, 0xffffff];
          const y = gy(e.x, e.y);
          fx.ring(e.x, y, e.y, 1.4, col, 0.5, 0.14);
          fx.glow(e.x, y + 0.5, e.y, 1.0, col, 0.25);
          if (e.id === 'steam') for (let i = 0; i < 5; i++) fx.smoke(e.x, y + 0.3, e.y, 0.7, 0xe8f0ff, 0.45, 1);
          else fx.sparks(e.x, y + 0.4, e.y, col, 6);
          this.onFloater?.({ text: label, x: e.x, y: 1.2, z: e.y, color: '#' + col.toString(16).padStart(6, '0') });
          break;
        }
        case 'teleport':
          for (const [x, z] of [[e.x, e.y], [e.x2, e.y2]] as const) {
            fx.ring(x, gy(x, z), z, 1.2, 0xff60ff, 0.4, 0.15);
            fx.glow(x, gy(x, z) + 0.5, z, 1.0, 0xff60ff, 0.3);
          }
          this.smooth.forEach((s) => {
            if (Math.abs(s.x - e.x) < 0.3 && Math.abs(s.y - e.y) < 0.3) {
              s.x = e.x2;
              s.y = e.y2;
            }
          });
          break;
        case 'immune':
          fx.ring(e.x, gy(e.x, e.y) + 0.4, e.y, 0.7, 0xffffff, 0.3, 0.1);
          break;
        case 'build':
        case 'upgrade':
        case 'sell': {
          const y = gy(e.x, e.y);
          for (let i = 0; i < 4; i++) fx.smoke(e.x + (Math.random() - 0.5) * 0.8, y + 0.1, e.y + (Math.random() - 0.5) * 0.8, 0.5, 0xb8a888, 0.4, 0.8);
          fx.ring(e.x, y, e.y, 0.9, 0xffffff, 0.35, 0.12);
          if (e.k === 'upgrade') {
            fx.pillar(e.x, y, e.y, 2.4, 0xffd860, 0.5, 0.25);
            fx.sparks(e.x, y + 0.8, e.y, 0xffd860, 10, 1.1);
          }
          if (e.k === 'sell') {
            fx.sparks(e.x, y + 0.4, e.y, 0xffd84a, 12, 1.2);
            fx.debrisBurst(e.x, y, e.y, 0x8a8070, 5);
            this.onFloater?.({ text: `+${e.refund}`, x: e.x, y: 1, z: e.y, color: '#ffd84a' });
          }
          if (e.k === 'build') fx.debrisBurst(e.x, y, e.y, 0x9a9080, 3);
          break;
        }
        case 'fusion': {
          const y = gy(e.x, e.y);
          fx.pillar(e.x, y, e.y, 5, 0xffe080, 1.1, 0.5);
          fx.ring(e.x, y, e.y, 3, 0xffe080, 0.8, 0.12);
          fx.ring(e.x, y, e.y, 2, 0xffffff, 0.6, 0.08);
          for (let i = 0; i < 24 * fx.intensity; i++) fx.emit({ shape: SHAPE.star, x: e.x, y: y + 1, z: e.y, vx: (Math.random() - 0.5) * 5, vy: Math.random() * 4, vz: (Math.random() - 0.5) * 5, drag: 1.5, life: 1, c0: 0xffffff, c1: 0xffc040, s0: 0.25, s1: 0.05 });
          this.flash = Math.max(this.flash, 0.35);
          shake(0.2);
          this.towerBorn.set(sim.c.towerByCell.get(Math.floor(e.y) * sim.s.map.width + Math.floor(e.x))?.uid ?? -1, this.time);
          break;
        }
        case 'towerActive': {
          const y = gy(e.x, e.y);
          const col = TOWERS[e.tower].color;
          fx.pillar(e.x, y, e.y, 3, col, 0.6, 0.35);
          fx.ring(e.x, y, e.y, e.active === 'nova' ? 5 : 2, col, 0.6, 0.12);
          if (e.active === 'deepfreeze') fx.ring(e.x, y, e.y, 3.5, 0xd8f4ff, 0.8, 0.2);
          if (e.active === 'goldrush') fx.sparks(e.x, y + 1, e.y, 0xffd84a, 20, 1.4, 0.16);
          if (e.active === 'nova') {
            this.flash = Math.max(this.flash, 0.25);
            shake(0.15);
          }
          break;
        }
        case 'plateBreak': {
          const y = gy(e.x, e.y) + 1.6;
          fx.glow(e.x, y, e.y, 1.6, 0xff4040, 0.3);
          fx.debrisBurst(e.x, y - 1, e.y, 0xff5050, 8, 1);
          fx.sparks(e.x, y, e.y, 0xff8080, 14, 1.4);
          this.flash = Math.max(this.flash, 0.2);
          shake(0.25);
          this.onFloater?.({ text: e.left > 0 ? 'Schwachpunkt zerstört!' : 'Panzer gebrochen!', x: e.x, y: 3, z: e.y, color: '#ff7070', big: true });
          break;
        }
        case 'devour': {
          const y = gy(e.x, e.y);
          fx.ring(e.x, y, e.y, 1.4, 0x6a1030, 0.6, 0.25);
          fx.debrisBurst(e.x, y, e.y, 0x8a8070, 8);
          this.onFloater?.({ text: 'Verschlungen!', x: e.x, y: 1.5, z: e.y, color: '#ff6080', big: true });
          shake(0.2);
          break;
        }
        case 'heal': {
          const y = gy(e.x, e.y);
          fx.ring(e.x, y, e.y, e.r, 0x60ff90, 0.6, 0.1);
          for (let i = 0; i < 8 * fx.intensity; i++) fx.emit({ shape: SHAPE.star, x: e.x + (Math.random() - 0.5) * e.r, y: y + 0.3, z: e.y + (Math.random() - 0.5) * e.r, vy: 1.2, life: 0.8, c0: 0xa0ffb0, s0: 0.14, s1: 0.06 });
          break;
        }
        case 'burrow': {
          const y = gy(e.x, e.y);
          for (let i = 0; i < 5; i++) fx.smoke(e.x, y + 0.1, e.y, 0.5, 0x6a5a40, 0.55, 0.9);
          fx.debrisBurst(e.x, y, e.y, 0x6a5a40, 5);
          break;
        }
        case 'gold':
          this.onFloater?.({ text: `+${e.amount}`, x: e.x, y: 1.1, z: e.y, color: '#ffd84a' });
          break;
        case 'ability': {
          const y = gy(e.x, e.y);
          if (e.stage === 'interrupt') {
            fx.ring(e.x, y + 0.3, e.y, 1.8, 0x60e8d4, 0.5, 0.15);
            this.onFloater?.({ text: 'Unterbrochen!', x: e.x, y: 1.6, z: e.y, color: '#60e8d4' });
          } else if (e.stage === 'cast') {
            const big: Partial<Record<string, [number, number]>> = {
              quake: [2.2, 0xffa040], nullPulse: [4, 0x40e0ff], summonBrood: [2.2, 0xff6020], stomp: [2.8, 0xffb060],
              siegeSlam: [1, 0xff8040], heal: [2.5, 0x60ff90], devour: [1.5, 0x80204a], riftJump: [1.5, 0xd060ff],
            };
            const b = big[e.id];
            if (b) {
              fx.ring(e.x, y, e.y, b[0], b[1], 0.55, 0.2);
              fx.glow(e.x, y + 0.5, e.y, b[0] * 0.8, b[1], 0.25, 0.6);
              if (e.id === 'quake' || e.id === 'stomp' || e.id === 'siegeSlam') {
                fx.debrisBurst(e.x, y, e.y, 0x6a5a48, 6);
                for (let i = 0; i < 4; i++) fx.smoke(e.x + (Math.random() - 0.5) * b[0], y + 0.1, e.y + (Math.random() - 0.5) * b[0], 0.8, 0x7a6a58, 0.4);
              }
              shake(e.id === 'stomp' ? 0.35 : 0.12);
            }
          }
          break;
        }
        case 'bossPhase': {
          if (e.phase === 0) break; // Auftritt wird separat inszeniert
          const boss = sim.s.enemies.find((x) => x.boss === e.boss && x.alive);
          if (boss) {
            const y = gy(boss.x, boss.y);
            fx.ring(boss.x, y, boss.y, 4, BOSSES[e.boss].color, 0.9, 0.2);
            fx.pillar(boss.x, y, boss.y, 6, mixColor(BOSSES[e.boss].color, 0xffffff, 0.3), 0.9, 0.7);
            fx.sparks(boss.x, y + 1.5, boss.y, BOSSES[e.boss].color, 24, 2);
          }
          this.flash = Math.max(this.flash, 0.4);
          shake(0.5);
          break;
        }
        case 'heroDeploy': {
          const def = HEROES[e.hero];
          const y = gy(e.x, e.y);
          fx.pillar(e.x, y, e.y, 4, def.accent, 0.6, 0.3);
          fx.ring(e.x, y, e.y, 1.6, def.color, 0.6, 0.15);
          fx.sparks(e.x, y + 0.5, e.y, def.accent, 14, 1.3);
          break;
        }
        case 'heroDown': {
          const y = gy(e.x, e.y);
          for (let i = 0; i < 6; i++) fx.smoke(e.x, y + 0.3, e.y, 0.6, 0x5a5a6a, 0.5);
          fx.sparks(e.x, y + 0.5, e.y, 0xa0a0b0, 10, 1);
          fx.ring(e.x, y, e.y, 1.2, 0xff6060, 0.5, 0.12);
          this.onFloater?.({ text: `${HEROES[e.hero].name.split(' ').pop()} fällt!`, x: e.x, y: 1.5, z: e.y, color: '#ff8080', big: true });
          break;
        }
        case 'heroLevel': {
          const h = sim.s.heroes.find((x) => x.id === e.hero);
          if (h) {
            const y = gy(h.x, h.y);
            fx.pillar(h.x, y, h.y, 3, 0xffd860, 0.6, 0.3);
            fx.ring(h.x, y, h.y, 1.2, 0xffd860, 0.5, 0.15);
            for (let i = 0; i < 10; i++) fx.emit({ shape: SHAPE.star, x: h.x, y: y + 0.8, z: h.y, vx: (Math.random() - 0.5) * 2, vy: 1 + Math.random() * 2, vz: (Math.random() - 0.5) * 2, drag: 1.2, life: 0.9, c0: 0xfff0a0, c1: 0xffb020, s0: 0.2, s1: 0.05 });
            this.onFloater?.({ text: `Stufe ${e.level}!`, x: h.x, y: 1.6, z: h.y, color: '#ffd860', big: true });
          }
          break;
        }
        case 'heroAttack': {
          const def = HEROES[e.hero];
          const y = gy(e.tx, e.ty) + 0.45;
          if (e.melee) {
            fx.sparks(e.tx, y, e.ty, def.accent, 4, 0.9, 0.1);
            fx.emit({ shape: SHAPE.ring, x: (e.x + e.tx) / 2, y, z: (e.y + e.ty) / 2, life: 0.15, c0: 0xffffff, s0: 0.2, s1: 0.6, a: 0.7 });
          } else {
            fx.beam(e.x, gy(e.x, e.y) + 0.7, e.y, e.tx, y, e.ty, def.accent, 0.03, 0.12);
            fx.glow(e.tx, y, e.ty, 0.35, def.accent, 0.15);
          }
          break;
        }
        case 'heroAbility': {
          const def = HEROES[e.hero];
          const y = gy(e.x, e.y);
          const r = Math.max(0.6, e.r);
          fx.ring(e.x, y, e.y, r, def.accent, 0.55, 0.14);
          fx.glow(e.x, y + 0.6, e.y, Math.min(2.5, r), def.accent, 0.3, 0.6);
          fx.sparks(e.x, y + 0.6, e.y, def.accent, 8, 1.2);
          if (e.ability === 'whirl') for (let k = 0; k < 3; k++) fx.ring(e.x, y + 0.3 + k * 0.1, e.y, r * (0.6 + k * 0.2), 0xffffff, 0.3 + k * 0.08, 0.05);
          if (r >= 3) shake(0.12);
          break;
        }
        case 'champAttack': {
          const y = gy(e.tx, e.ty) + 0.45;
          if (e.melee) {
            fx.sparks(e.tx, y, e.ty, e.color, 4, 0.9, 0.1);
            fx.emit({ shape: SHAPE.ring, x: (e.x + e.tx) / 2, y, z: (e.y + e.ty) / 2, life: 0.15, c0: 0xffffff, s0: 0.2, s1: 0.6, a: 0.7 });
          } else {
            fx.beam(e.x, gy(e.x, e.y) + 0.7, e.y, e.tx, y, e.ty, e.color, 0.035, 0.12);
            fx.glow(e.tx, y, e.ty, 0.35, e.color, 0.15);
          }
          break;
        }
        case 'champSkill': {
          const y = gy(e.tx, e.ty);
          const r = Math.max(0.6, e.r);
          fx.ring(e.tx, y, e.ty, r, e.color, 0.55, 0.14);
          fx.glow(e.tx, y + 0.6, e.ty, Math.min(2.5, r), e.color, 0.3, 0.6);
          fx.sparks(e.tx, y + 0.6, e.ty, e.color, 10, 1.2);
          if (e.skill === 'charge' || e.skill === 'pierce') fx.beam(e.x, gy(e.x, e.y) + 0.6, e.y, e.tx, y + 0.6, e.ty, e.color, 0.12, 0.3);
          if (e.skill === 'whirl' || e.skill === 'bladestorm') for (let k = 0; k < 3; k++) fx.ring(e.tx, y + 0.3 + k * 0.1, e.ty, r * (0.6 + k * 0.2), 0xffffff, 0.3 + k * 0.08, 0.05);
          if (e.skill === 'citadel' || e.skill === 'inspire' || e.skill === 'field') fx.pillar(e.tx, y, e.ty, 3, e.color, 0.7, 0.4);
          if (r >= 3) shake(0.12);
          break;
        }
        case 'champLevel': {
          const y = gy(e.x, e.y);
          fx.pillar(e.x, y, e.y, 3, 0xffd860, 0.6, 0.3);
          fx.ring(e.x, y, e.y, 1.2, 0xffd860, 0.5, 0.15);
          for (let i = 0; i < 12; i++) fx.emit({ shape: SHAPE.star, x: e.x, y: y + 0.8, z: e.y, vx: (Math.random() - 0.5) * 2, vy: 1 + Math.random() * 2, vz: (Math.random() - 0.5) * 2, drag: 1.2, life: 0.9, c0: 0xfff0a0, c1: 0xffb020, s0: 0.2, s1: 0.05 });
          this.onFloater?.({ text: `Stufe ${e.level}!`, x: e.x, y: 1.6, z: e.y, color: '#ffd860', big: true });
          break;
        }
        case 'champDown':
          fx.ring(e.x, gy(e.x, e.y), e.y, 1.2, 0xff6060, 0.5, 0.12);
          this.onFloater?.({ text: 'Held gefallen!', x: e.x, y: 1.5, z: e.y, color: '#ff8080', big: true });
          break;
        case 'champRespawn':
          fx.pillar(e.x, gy(e.x, e.y), e.y, 3, 0x9ad8ff, 0.7, 0.35);
          break;
        case 'pickup':
          fx.glow(e.x, gy(e.x, e.y) + 0.4, e.y, 0.5, e.color, 0.25);
          if (e.kind !== 'xp') this.onFloater?.({ text: e.label, x: e.x, y: 1.2, z: e.y, color: '#' + e.color.toString(16).padStart(6, '0'), big: e.kind === 'item' });
          break;
        case 'mined':
          fx.debrisBurst(e.x, gy(e.x, e.y) + 0.3, e.y, 0x8a7a60, 14);
          fx.ring(e.x, gy(e.x, e.y) + 0.05, e.y, 1.0, 0xffd860, 0.5, 0.1);
          this.onFloater?.({ text: e.label, x: e.x, y: 1.4, z: e.y, color: '#ffd860', big: true });
          break;
        case 'mineStart':
          fx.ring(e.x, gy(e.x, e.y) + 0.05, e.y, 0.8, 0xffd860, 0.4, 0.08);
          break;
        case 'towerBuff':
          fx.ring(e.x, gy(e.x, e.y) + 0.05, e.y, 0.7, e.color, 0.5, 0.1);
          fx.sparks(e.x, gy(e.x, e.y) + 0.9, e.y, e.color, 5, 0.6);
          break;
        case 'potion': {
          const col = POTIONS[e.potion].color;
          fx.ring(e.x, gy(e.x, e.y) + 0.05, e.y, 0.9, col, 0.5, 0.1);
          for (let i = 0; i < 8; i++) fx.emit({ shape: SHAPE.glow, x: e.x, y: gy(e.x, e.y) + 0.3, z: e.y, vx: (Math.random() - 0.5), vy: 1 + Math.random(), vz: (Math.random() - 0.5), life: 0.7, c0: col, s0: 0.18, s1: 0.04 });
          break;
        }
        case 'victory':
          this.flash = Math.max(this.flash, 0.3);
          break;
        default:
          break;
      }
    }
  }

  private addCorpse(_sim: GameSimulation, x: number, y: number, enemy: EnemyId, boss: BossId | null, color: number, size: number): void {
    // passende Glättung (Blickrichtung, Höhe) über die Position suchen
    let best: Smooth | null = null;
    let bd = 1.5;
    for (const s of this.smooth.values()) {
      if (s.def !== enemy || s.boss !== boss) continue;
      const d = Math.abs(s.x - x) + Math.abs(s.y - y);
      if (d < bd) {
        bd = d;
        best = s;
      }
    }
    const h = best ? best.h : 0;
    const yaw = best ? -best.head : 0;
    const parts = boss ? BOSS_MODELS[boss] : enemy === 'boss' ? null : ENEMY_MODELS[enemy as Exclude<EnemyId, 'boss'>];
    if (parts) {
      const scale = boss ? BOSSES[boss].size : size;
      this.corpses.push({ parts, x, y: h + (ENEMIES[enemy]?.flying ? 1.1 : 0), z: y, yaw, scale, color, t: 0, max: boss ? 1.6 : 0.55, boss: boss !== null });
      if (this.corpses.length > 60) this.corpses.shift();
    }
    const fx = this.fx;
    const n = boss ? 40 : 7;
    for (let i = 0; i < n * fx.intensity; i++) {
      const a = Math.random() * Math.PI * 2;
      fx.emit({
        x: x + Math.cos(a) * 0.2 * size, y: h + 0.3 + Math.random() * 0.5 * size, z: y + Math.sin(a) * 0.2 * size,
        vx: Math.cos(a) * 0.6, vy: 0.8 + Math.random() * 1.5, vz: Math.sin(a) * 0.6, drag: 1.5,
        life: 0.5 + Math.random() * 0.4, c0: mixColor(color, 0xffffff, 0.4), c1: color, s0: 0.14, s1: 0.02,
      });
    }
    fx.decal(x, h, y, 0.35 * size + 0.2, shade(color, 0.35), 0.35, 4);
    if (boss) {
      fx.explosion(x, h, y, 2.5, color, true);
      fx.pillar(x, h, y, 8, mixColor(color, 0xffffff, 0.5), 1.4, 0.9);
    }
  }

  // ------------------------------------------------------------ Teile zeichnen

  private drawParts(
    parts: Part[], x: number, y: number, z: number, yaw: number, aim: number, scale: number,
    main: number, accent: number, pass: Pass, alpha: number,
    tint: number | null, tintAmt: number, emitBoost: number,
    walk: number, moving: number, attack: number, phase: number,
    squash = 1,
  ): void {
    const r = this.r;
    const cy = Math.cos(yaw), sy = Math.sin(yaw);
    const ca = Math.cos(aim), sa = Math.sin(aim);
    const t = this.time;
    for (const p of parts) {
      const turret = p.turret === true;
      const c = turret ? ca : cy;
      const s = turret ? sa : sy;
      const baseYaw = turret ? aim : yaw;
      let px = p.x;
      let pitch = p.pitch ?? 0;
      let roll = p.roll ?? 0;
      let py = p.y;
      if (p.leg !== undefined && moving > 0) {
        const sw = Math.sin(walk + p.leg);
        pitch += sw * 0.5 * moving;
        px += sw * 0.06 * moving;
        py += Math.max(0, -Math.cos(walk + p.leg)) * 0.04 * moving;
      }
      if (p.wing) roll += Math.sin(t * 13 + phase) * 0.7 * p.wing;
      if (p.arm && attack > 0) pitch -= Math.sin(Math.min(1, attack) * Math.PI) * 1.1;
      const spin = p.spin ? t * p.spin : 0;
      const bob = p.bob ? Math.sin(t * 2.5 + phase) * p.bob : 0;
      const ox = (px * c + p.z * s) * scale;
      const oz = (-px * s + p.z * c) * scale;
      let col = p.c === -1 ? main : p.c === -2 ? accent : p.c === -3 ? this.skin.stone : p.c;
      if (tint !== null && tintAmt > 0) col = mixColor(col, tint, tintAmt);
      const wy = y + (py + bob) * scale * squash;
      r.add(pass, p.m, x + ox, wy, z + oz, p.sx * scale, p.sy * scale * squash, p.sz * scale, col, alpha, (p.e ?? 0) + emitBoost, baseYaw + (p.yaw ?? 0) + spin, pitch, roll, p.mat ?? 0);
      if (p.glow && alpha > 0.3) {
        const pulse = 0.85 + Math.sin(t * 3 + phase) * 0.15;
        r.billboard(SPRITE_ADD, SHAPE.glow, x + ox, wy + (p.m === 'cone8' || p.m === 'cyl8' ? p.sy * scale * 0.5 : 0), z + oz, Math.max(p.sx, p.sy) * scale * 1.5 * pulse, p.glow, 0.45 * alpha);
      }
    }
  }

  // ------------------------------------------------------------ Frame

  frame(sim: GameSimulation, ui: ViewUiState, dt: number): void {
    if (!this.lighting || !this.mapRef) return;
    this.time += dt;
    const s = sim.s;
    const r = this.r;
    const fx = this.fx;
    fx.cap = Math.max(200, ui.particleCap * 2);
    fx.intensity = ui.reducedMotion ? 0.5 : 1;

    // Kamera inkl. Wackeln (nur für dieses Bild, ohne Drift)
    this.cam.spin = ui.cinematic ? 0.06 : 0;
    let sx = 0;
    let sz = 0;
    if (this.shake > 0) {
      this.shake = Math.max(0, this.shake - dt);
      const k = this.shake * 0.35;
      sx = (Math.random() - 0.5) * k;
      sz = (Math.random() - 0.5) * k;
    }
    this.cam.tx += sx;
    this.cam.tz += sz;
    this.cam.update(dt);
    this.cam.tx -= sx;
    this.cam.tz -= sz;
    r.camRight = this.cam.right;
    r.camUp = this.cam.up;
    r.toCam = this.cam.toCam;
    r.time = this.time;

    if (this.firstFrame) {
      for (const t of s.towers) this.towerBorn.set(t.uid, -10);
      for (const e of s.enemies) if (e.boss) this.bossSeen.add(e.uid);
      this.firstFrame = false;
    }

    if ((s.map.rev ?? 0) !== this.mapRev) this.rebuildTerrain(s.map);
    this.updateLighting(sim, dt);
    this.drawWorldDynamics(sim, ui);
    this.drawNetwork(sim);
    this.drawTowers(sim, ui);
    this.drawSelection(sim, ui);
    this.drawZones(sim);
    this.drawEnemies(sim, ui, dt);
    this.drawCorpses(dt);
    this.drawHeroes(sim, ui, dt);
    if (s.he) this.drawHeroEndless(sim, ui, dt);
    this.drawProjectiles(sim);
    this.drawWeather(sim, dt, ui);
    fx.update(dt, r);

    this.flash = Math.max(0, this.flash - dt * 1.6);
    r.render(this.cam.vp, this.cam.toCam, this.lighting, this.grade(sim, ui));
  }

  private updateLighting(sim: GameSimulation, dt: number): void {
    const L = this.lighting;
    const map = this.mapRef;
    if (!L || !map) return;
    const s = sim.s;
    const pal = BIOMES[map.biome].palette;
    // Tageszeit wandert mit den Wellen (10 Wellen = ein Tag)
    const tod = ((s.wave % 10) / 10) * TOD.length;
    const i0 = Math.floor(tod) % TOD.length;
    const i1 = (i0 + 1) % TOD.length;
    const f = tod - Math.floor(tod);
    const [c0, k0, e0] = TOD[i0];
    const [c1, k1, e1] = TOD[i1];
    let col = mixColor(mixColor(pal.light, c0, 0.55), mixColor(pal.light, c1, 0.55), f);
    let inten = k0 + (k1 - k0) * f;
    const elev = e0 + (e1 - e0) * f;
    const bossAlive = s.enemies.some((e) => e.alive && e.boss);
    if (bossAlive) {
      col = mixColor(col, 0xff7050, 0.3);
      inten *= 0.92;
    }
    if (s.phase === 'build') inten *= 1.04;
    const target = hexToRgb(col).map((v) => v * 0.88 * inten) as [number, number, number];
    const k = Math.min(1, dt * 1.2);
    for (let j = 0; j < 3; j++) this.light.color[j] += (target[j] - this.light.color[j]) * k;
    L.lightColor = [...this.light.color];
    const az = -2.4 + (s.wave % 10) * 0.12;
    const dir = [Math.cos(az) * 0.7, elev, Math.sin(az) * 0.7];
    const len = Math.hypot(dir[0], dir[1], dir[2]);
    const want: [number, number, number] = [dir[0] / len, dir[1] / len, dir[2] / len];
    for (let j = 0; j < 3; j++) this.light.dir[j] += (want[j] - this.light.dir[j]) * k;
    const dl = Math.hypot(...this.light.dir);
    L.light = [this.light.dir[0] / dl, this.light.dir[1] / dl, this.light.dir[2] / dl];
    const skyCol = mixColor(mixColor(pal.sky, 0xffffff, 0.55), bossAlive ? 0xff8060 : col, 0.25);
    L.sky = hexToRgb(skyCol).map((v) => v * 0.55 * (0.7 + inten * 0.3)) as [number, number, number];
    const ev = s.currentWave?.event ?? null;
    const size = Math.max(map.width, map.height);
    const fogMul = ev === 'fog' ? 0.45 : 1;
    L.fogStart = size * 0.62 * fogMul;
    L.fogEnd = size * 0.62 * fogMul + (ev === 'fog' ? 9 : 14);

    // Schattenkamera aus Lichtrichtung über die ganze Karte
    const cx = map.width / 2;
    const cz = map.height / 2;
    const R = Math.hypot(map.width, map.height) / 2 + 2.5;
    const d = 40;
    lookAt(this.lightVPm.view, cx + L.light[0] * d, L.light[1] * d, cz + L.light[2] * d, cx, 0, cz);
    ortho(this.lightVPm.proj, -R, R, -R, R, 1, d * 2 + 10);
    multiply(this.r.lightVP, this.lightVPm.proj, this.lightVPm.view);
  }

  private grade(sim: GameSimulation, ui: ViewUiState): Grade {
    const map = this.mapRef;
    const g = map ? BIOMES[map.biome].grade : { tint: [1, 1, 1] as [number, number, number], saturation: 1, contrast: 1 };
    const tint: [number, number, number] = [...g.tint];
    let sat = g.saturation;
    let con = g.contrast;
    const ev = sim.s.currentWave?.event ?? null;
    const evTint: Partial<Record<string, [number, number, number, number, number]>> = {
      blizzard: [0.95, 1.0, 1.08, 0.85, 1], heatwave: [1.1, 1.0, 0.9, 1.05, 1.04], riftSurge: [1.05, 0.95, 1.1, 1.1, 1.05],
      sporeBloom: [1.0, 1.05, 0.95, 1.05, 1], crystalResonance: [1.02, 0.98, 1.08, 1.1, 1.02], fog: [0.98, 1.0, 1.02, 0.85, 0.9],
      glowtide: [0.95, 1.0, 1.1, 1.2, 1.05],
    };
    const et = ev ? evTint[ev] : undefined;
    if (et) {
      tint[0] *= et[0]; tint[1] *= et[1]; tint[2] *= et[2];
      sat *= et[3];
      con *= et[4];
    }
    const s = sim.s;
    const low = s.maxLives > 0 ? s.lives / s.maxLives : 1;
    let vig = 0.85;
    if (low < 0.3 && s.phase !== 'victory' && !ui.cinematic) {
      vig += (0.3 - low) * 3 * (0.75 + Math.sin(this.time * 4) * 0.25);
      tint[0] *= 1.05;
      sat *= 0.9;
    }
    if (s.enemies.some((e) => e.alive && e.boss)) {
      tint[0] *= 1.04;
      tint[2] *= 0.97;
      con *= 1.04;
    }
    // 3.13: Kino-Look – Tilt-Shift, Split-Toning, Sonnendunst aus Lichtrichtung
    const mood = map ? MOOD[map.biome] : null;
    const L = this.lighting;
    let sun: [number, number, number] = [0, 0, 0];
    if (L && mood && mood.sun > 0) {
      const p = this.cam.worldToScreen(this.cam.tx + L.light[0] * 40, L.light[1] * 40, this.cam.tz + L.light[2] * 40);
      const ux = Math.max(-0.4, Math.min(1.4, p.x / Math.max(1, this.cam.width)));
      const uy = Math.max(-0.4, Math.min(1.4, 1 - p.y / Math.max(1, this.cam.height)));
      sun = [ux, Number.isFinite(uy) ? uy : 1.1, mood.sun * (bossAlive(sim) ? 0.6 : 1)];
    }
    return {
      tint, saturation: sat, contrast: con, vignette: vig, bloom: 0.38, flash: ui.reducedMotion ? this.flash * 0.3 : this.flash,
      dof: ui.cinematic ? 0.75 : 0.55, split: 1, sun, sunColor: mood ? hexToRgb(mood.glow) : [1, 0.9, 0.7],
    };
  }

  // ------------------------------------------------------------ Welt: Portale, Kern, Leuchtpunkte, Raster, Route

  private drawWorldDynamics(sim: GameSimulation, ui: ViewUiState): void {
    const r = this.r;
    const s = sim.s;
    const map = s.map;
    const t = this.time;
    // Leuchtpunkte (Kristalle, Lava, Pilze, Adern)
    for (const g of this.glows) {
      const p = 0.75 + Math.sin(t * 1.7 + g.phase) * 0.25;
      r.billboard(SPRITE_ADD, SHAPE.glow, g.x, g.y, g.z, g.size * p, g.color, 0.2);
    }
    // Wasser/Lava-Funkeln am Rand
    const sea = SEA[BIOMES[map.biome].sea];
    if (BIOMES[map.biome].sea === 'lava' && Math.random() < 0.3 * this.fx.intensity) {
      const a = Math.random() * Math.PI * 2;
      const d = Math.max(map.width, map.height) * (0.55 + Math.random() * 0.3);
      this.fx.emit({ x: map.width / 2 + Math.cos(a) * d, y: sea.y + 0.1, z: map.height / 2 + Math.sin(a) * d, vy: 1.5, grav: 2, life: 1, c0: 0xffc060, c1: 0xff4010, s0: 0.2, s1: 0.05 });
    }
    // Portale
    for (const sp of this.spawnYaw) {
      const swirl = t * 2;
      r.add('opaque', 'torus', sp.x, 0.75, sp.y, 1.25, 1.25, 1.6, 0xc060ff, 1, 1.0, sp.yaw + Math.PI / 2, 0, Math.PI / 2);
      r.add('transparent', 'disc', sp.x, 0.75, sp.y, 0.58, 1, 0.58, 0x400a60, 0.85, 1, sp.yaw + Math.PI / 2, swirl, Math.PI / 2);
      r.billboard(SPRITE_ADD, SHAPE.glow, sp.x, 0.75, sp.y, 1.0 + Math.sin(t * 3) * 0.1, 0xb050ff, 0.35);
      r.ground(SPRITE_ADD, SHAPE.ring, sp.x, 0.02, sp.y, 0.9 + Math.sin(t * 2) * 0.1, 0xc060ff, 0.6, t, 0.3, 0.1);
      // 3.13: wirbelnder Riss in der Portalebene, Runenkreis am Boden, Lichtsäule
      const px = Math.sin(sp.yaw) * 0.6;
      const pz = Math.cos(sp.yaw) * 0.6;
      r.sprite(SPRITE_ADD, SHAPE.vortex, sp.x, 0.75, sp.y, px, 0, pz, 0, 0.6, 0, 0xd890ff, 0.9, 0.3, 0.15, t * 1.4);
      r.sprite(SPRITE_ADD, SHAPE.vortex, sp.x, 0.75, sp.y, -px * 0.8, 0, -pz * 0.8, 0, 0.48, 0, 0x7040ff, 0.7, 0.3, 0.15, -t * 2.1);
      r.ground(SPRITE_ADD, SHAPE.star, sp.x, 0.03, sp.y, 1.35, 0x9a50ff, 0.18, -t * 0.4);
      r.ground(SPRITE_ADD, SHAPE.ring, sp.x, 0.03, sp.y, 1.45, 0xc080ff, 0.35, t * 0.3, 0.3, 0.03);
      r.line(sp.x, 0, sp.y, sp.x, 4.5, sp.y, 0.35 + Math.sin(t * 2.3) * 0.05, 0xb060ff, 0.14);
      if (Math.random() < 0.35 * this.fx.intensity) {
        const a = Math.random() * Math.PI * 2;
        this.fx.emit({ x: sp.x + Math.cos(a) * 0.9, y: 0.75 + Math.sin(a) * 0.7, z: sp.y + Math.sin(a) * 0.3, vx: -Math.cos(a) * 1.5, vy: -Math.sin(a) * 1.2, vz: -Math.sin(a) * 0.4, life: 0.55, c0: 0xe0a0ff, c1: 0x6010a0, s0: 0.12, s1: 0.02 });
      }
    }
    // Kern
    const gp = map.goal;
    const health = s.lives / Math.max(1, s.maxLives);
    const coreCol = mixColor(0xff3a3a, 0x60f0ff, health);
    const pulse = 1 + Math.sin(t * 3) * 0.05;
    const cx = gp.x + 0.5;
    const cz = gp.y + 0.5;
    const cyy = 1.0 + Math.sin(t * 2) * 0.08;
    r.add('opaque', 'octa', cx, cyy, cz, 0.55 * pulse, 1.0 * pulse, 0.55 * pulse, coreCol, 1, 1.1, t * 0.8);
    r.add('transparent', 'sphere', cx, cyy, cz, 1.25, 1.25, 1.25, coreCol, 0.12, 1);
    r.billboard(SPRITE_ADD, SHAPE.glow, cx, cyy, cz, 1.2 * pulse, coreCol, 0.3);
    for (let k = 0; k < 3; k++) {
      const a = t * 1.3 + (k / 3) * Math.PI * 2;
      r.add('opaque', 'gem', cx + Math.cos(a) * 0.62, cyy + Math.sin(t * 2 + k) * 0.2, cz + Math.sin(a) * 0.62, 0.14, 0.14, 0.14, coreCol, 1, 1.2, a);
    }
    r.ground(SPRITE_ADD, SHAPE.ring, cx, 0.14, cz, 0.95, coreCol, 0.5, -t, 0.3, 0.08);
    // 3.13: Lichtsäule in den Himmel, Runenkreise und schwebende Ringe um den Kern
    r.line(cx, 0.2, cz, cx, 7, cz, 0.22 + Math.sin(t * 3.1) * 0.03, coreCol, 0.22);
    r.line(cx, 0.2, cz, cx, 4, cz, 0.08, 0xffffff, 0.25);
    r.ground(SPRITE_ADD, SHAPE.star, cx, 0.15, cz, 1.6, coreCol, 0.16, t * 0.25);
    r.ground(SPRITE_ADD, SHAPE.ring, cx, 0.15, cz, 1.7, coreCol, 0.35, -t * 0.2, 0.3, 0.025);
    r.add('transparent', 'ringThin', cx, cyy, cz, 1.05, 1, 1.05, coreCol, 0.55, 1, t * 0.6, 0.5 + Math.sin(t * 0.7) * 0.2, 0.3);
    r.add('transparent', 'ringThin', cx, cyy, cz, 0.85, 1, 0.85, coreCol, 0.45, 1, -t * 0.9, -0.6, Math.sin(t * 0.5) * 0.4);
    if (health < 0.35 && Math.random() < 0.3) this.fx.sparks(cx, cyy, cz, 0xff5050, 1, 0.8);

    // Bauraster
    const building = ui.buildDef !== null;
    if (building || ui.showGrid) {
      for (let y = 0; y < map.height; y++) {
        for (let x = 0; x < map.width; x++) {
          const i = y * map.width + x;
          if (map.cells[i] !== Cell.Open || sim.c.towerByCell.has(i)) continue;
          const top = cellTop(map, x, y);
          r.add('transparent', 'box', x + 0.5, top + 0.012, y + 0.5, 0.9, 0.01, 0.9, building ? 0x80ff90 : 0xffffff, building ? 0.16 : 0.07, 1);
        }
      }
    }

    // Route im Mazing andeuten (Lichtpunkte, die zum Kern laufen)
    if (map.variant === 'mazing' && s.phase === 'build' && !(ui.buildDef && ui.hoverCell)) {
      const route = this.currentRoute(sim);
      for (let i = 0; i < route.length - 1; i++) {
        const ph = ((t * 2 - i * 0.25) % 2 + 2) % 2;
        if (ph > 1) continue;
        const a = route[i];
        const b = route[i + 1];
        r.billboard(SPRITE_ADD, SHAPE.glow, (a.x + b.x) / 2 + 0.5, cellTop(map, a.x, a.y) + 0.15, (a.y + b.y) / 2 + 0.5, 0.22, 0xfff0a0, 0.8 * (1 - Math.abs(ph - 0.5) * 2));
      }
    }
  }

  private routeCache: { v: number; route: GridPos[] } | null = null;
  private currentRoute(sim: GameSimulation): GridPos[] {
    const v = sim.s.towers.length * 1000 + (sim.s.towers.at(-1)?.uid ?? 0);
    if (this.routeCache && this.routeCache.v === v) return this.routeCache.route;
    const map = sim.s.map;
    const route: GridPos[] = [];
    let cur: GridPos = map.spawn;
    route.push(cur);
    const f = sim.c.field;
    for (let k = 0; k < 900; k++) {
      if (cur.x === map.goal.x && cur.y === map.goal.y) break;
      let best: GridPos | null = null;
      let bv = f[cur.y * map.width + cur.x];
      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const nx = cur.x + dx;
          const ny = cur.y + dy;
          if (nx < 0 || ny < 0 || nx >= map.width || ny >= map.height) continue;
          if (dx && dy && (f[cur.y * map.width + nx] >= 1e9 || f[ny * map.width + cur.x] >= 1e9)) continue;
          const v2 = f[ny * map.width + nx];
          if (v2 < bv - 1e-9) {
            bv = v2;
            best = { x: nx, y: ny };
          }
        }
      }
      if (!best) break;
      route.push(best);
      cur = best;
    }
    this.routeCache = { v, route };
    return route;
  }

  // ------------------------------------------------------------ Netzwerk

  private drawNetwork(sim: GameSimulation): void {
    const r = this.r;
    const s = sim.s;
    const res = new Set<number>();
    for (const g of s.network.groups) if (g.resonances.length) for (const m of g.members) res.add(m);
    const surge = s.buffs.some((b) => b.id === 'surge' || b.id === 'resoSong');
    for (const l of s.network.links) {
      const a = sim.c.towerByUid.get(l.a);
      const b = sim.c.towerByUid.get(l.b);
      if (!a || !b) continue;
      const ay = cellTop(s.map, a.cx, a.cy) + TOWER_TOP[a.def] * 0.9;
      const by = cellTop(s.map, b.cx, b.cy) + TOWER_TOP[b.def] * 0.75;
      const resonant = res.has(a.uid) && res.has(b.uid);
      const col = resonant ? 0xffd860 : 0x5ab8ff;
      const disrupted = a.disrupted > 0 || b.disrupted > 0;
      const w = (surge ? 0.07 : 0.04) * (disrupted ? 0.6 : 1);
      const alpha = disrupted ? 0.25 + Math.random() * 0.25 : 0.55;
      r.line(a.cx + 0.5, ay, a.cy + 0.5, b.cx + 0.5, by, b.cy + 0.5, w * 2.5, col, alpha * 0.4);
      r.line(a.cx + 0.5, ay, a.cy + 0.5, b.cx + 0.5, by, b.cy + 0.5, w, mixColor(col, 0xffffff, 0.5), alpha);
      const tt = (this.time * 0.8 + (l.a * 0.37 + l.b * 0.11)) % 1;
      r.billboard(SPRITE_ADD, SHAPE.glow, a.cx + 0.5 + (b.cx - a.cx) * tt, ay + (by - ay) * tt, a.cy + 0.5 + (b.cy - a.cy) * tt, 0.18, col, 0.9);
    }
    // Lichtbögen zwischen Tesla-Spulen
    for (const [a, b] of teslaPairs(sim)) {
      const ay = cellTop(s.map, a.cx, a.cy) + 1.2;
      const by = cellTop(s.map, b.cx, b.cy) + 1.2;
      const pts = jag(a.cx + 0.5, ay, a.cy + 0.5, b.cx + 0.5, by, b.cy + 0.5);
      const fl = 0.6 + Math.random() * 0.4;
      for (let i = 0; i + 5 < pts.length; i += 3) {
        r.line(pts[i], pts[i + 1], pts[i + 2], pts[i + 3], pts[i + 4], pts[i + 5], 0.14, 0x60c8ff, 0.3 * fl);
        r.line(pts[i], pts[i + 1], pts[i + 2], pts[i + 3], pts[i + 4], pts[i + 5], 0.035, 0xe0f8ff, 0.9 * fl);
      }
    }
  }

  // ------------------------------------------------------------ Türme

  private drawTowers(sim: GameSimulation, ui: ViewUiState): void {
    const r = this.r;
    const s = sim.s;
    const t = this.time;
    const hymn = s.buffs.some((b) => b.id === 'hymn');
    const hl = new Set(ui.highlight ?? []);
    for (const tw of s.towers) {
      const def = TOWERS[tw.def];
      const by = cellTop(s.map, tw.cx, tw.cy);
      const { x, y: z } = towerCenter(tw);
      let born = this.towerBorn.get(tw.uid);
      if (born === undefined) {
        born = t;
        this.towerBorn.set(tw.uid, t);
      }
      const age = Math.min(1, (t - born) / 0.45);
      const grow = age >= 1 ? 1 : easeOutBack(age);
      const stunned = tw.stunned > 0;
      const scale = (0.9 + (tw.level - 1) * 0.035 + (tw.spec ? 0.04 : 0) + (def.legendary ? 0.08 : 0)) * grow * (towerSize(tw.def) > 1 ? 1.75 : 1) * UNIT;
      const recoil = tw.fireAnim > 0 ? tw.fireAnim : 0;
      const squash = 1 - recoil * 0.08;
      const top = TOWER_TOP[tw.def];
      const main = this.skin.tintAmount > 0 ? mixColor(def.color, this.skin.tint, this.skin.tintAmount) : def.color;
      const accent = tw.spec === 1 ? 0xffa040 : tw.spec === 2 ? 0x60e0ff : 0xffffff;
      const activeOn = tw.activeT > 0;
      const emitBoost = (recoil > 0 ? 0.35 * recoil : 0) + (activeOn ? 0.4 + Math.sin(t * 10) * 0.2 : 0) + (tw.forged > s.time ? 0.25 : 0);
      const parts = TOWER_MODELS[tw.def];
      this.drawParts(parts, x, by, z, 0, -tw.aim, scale, main, accent, 'opaque', 1, stunned ? 0x505058 : null, stunned ? 0.6 : 0, emitBoost, 0, 0, recoil, tw.uid, squash);
      if (TOWERS[tw.def].attack !== 'none') {
        if (tw.level >= 2) this.drawParts(levelParts(tw.level, top), x, by, z, 0, 0, scale, main, accent, 'opaque', 1, null, 0, 0, 0, 0, 0, tw.uid);
        if (tw.spec) this.drawParts(specParts(tw.spec, top), x, by, z, 0, 0, scale, main, accent, 'opaque', 1, null, 0, 0, 0, 0, 0, tw.uid);
      }
      if (age < 1) {
        r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.03, z, 0.3 + age * 0.6, 0xffffff, 1 - age, 0, 0.3, 0.12);
      }
      if (def.legendary) {
        r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.04, z, 0.62, 0xffe080, 0.45 + Math.sin(t * 2) * 0.15, t, 0.3, 0.08);
        if (Math.random() < 0.12 * this.fx.intensity) this.fx.emit({ shape: SHAPE.star, x: x + (Math.random() - 0.5) * 0.8, y: by + 0.3, z: z + (Math.random() - 0.5) * 0.8, vy: 0.8, life: 0.9, c0: 0xfff0b0, c1: 0xffb040, s0: 0.12, s1: 0.03 });
      }
      if (activeOn) {
        r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.04, z, 0.75 + Math.sin(t * 6) * 0.05, def.color, 0.8, -t * 2, 0.3, 0.14);
        r.billboard(SPRITE_ADD, SHAPE.glow, x, by + top * 0.7, z, 1.1, def.color, 0.35);
      }
      if (tw.forged > s.time && Math.random() < 0.25) this.fx.sparks(x, by + 0.4, z, 0xffa040, 1, 0.6, 0.08);
      if (hymn && Math.random() < 0.04) this.fx.emit({ shape: SHAPE.star, x, y: by + top, z, vy: 0.6, life: 0.8, c0: 0xffe080, s0: 0.12, s1: 0.04 });
      if (hl.has(tw.uid)) {
        r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.05, z, 0.62 * towerSize(tw.def), 0xffd860, 0.6 + Math.sin(t * 6) * 0.3, 0, 0.3, 0.14);
      }
      // Turmspezifische Belebung
      switch (tw.def) {
        case 'phoenix':
        case 'dragon': {
          const o = tw.orbit;
          const dx = x + Math.cos(o) * 0.9;
          const dz = z + Math.sin(o) * 0.9;
          const dy = by + top + 0.5 + Math.sin(t * 3 + tw.uid) * 0.1;
          const yaw = Math.atan2(-Math.cos(o), -Math.sin(o));
          this.drawParts(DRAGON, dx, dy, dz, yaw, yaw, 0.55 * scale, main, tw.spec === 2 ? 0x80c8ff : 0xffa040, 'opaque', 1, null, 0, 0, 0, 0, 0, tw.uid);
          break;
        }
        case 'gravity':
          r.ground(false, SHAPE.disc, x, by + 0.03, z, 0.6, 0x100820, 0.35, 0, 0.6);
          if (Math.random() < 0.2 * this.fx.intensity) {
            const a = Math.random() * Math.PI * 2;
            this.fx.emit({ x: x + Math.cos(a) * 0.9, y: by + 0.6, z: z + Math.sin(a) * 0.9, vx: -Math.cos(a) * 1.5, vz: -Math.sin(a) * 1.5, life: 0.55, c0: 0xa080ff, c1: 0x200840, s0: 0.1, s1: 0.03 });
          }
          break;
        case 'totem': {
          const st = sim.c.towerStats.get(tw.uid);
          if (st && st.range > 0) r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.03, z, st.range, 0xffe080, 0.12 + Math.sin(t * 2) * 0.05, 0, 0.3, 0.02);
          break;
        }
        case 'meteor':
        case 'starfall':
          if (Math.random() < 0.15 * this.fx.intensity) this.fx.fire(x, by + top - 0.3, z, 0.6, 0xff6020, false);
          break;
        default:
          break;
      }
      // Laserstrahl
      if (def.attack === 'laser' && tw.target && tw.charge > 0 && !stunned) {
        const e = sim.c.enemyByUid?.get(tw.target) ?? s.enemies.find((q) => q.uid === tw.target);
        if (e && e.alive) {
          const ey = cellTop(s.map, Math.floor(e.x), Math.floor(e.y)) + (e.flying ? 1.4 : 0.45);
          const w = 0.035 + tw.charge * 0.07;
          const ty = by + top * 0.85;
          const ddx = e.x - x;
          const ddz = e.y - z;
          const len = Math.hypot(ddx, ddz) || 1;
          const st = sim.c.towerStats.get(tw.uid);
          const reach = (st?.range ?? len) + 0.3;
          const ex = x + (ddx / len) * reach;
          const ez = z + (ddz / len) * reach;
          r.line(x, ty, z, ex, ey, ez, w * 4, def.color, 0.35);
          r.line(x, ty, z, ex, ey, ez, w, 0xffffff, 0.95);
          r.billboard(SPRITE_ADD, SHAPE.glow, e.x, ey, e.y, 0.5 + tw.charge * 0.4, def.color, 0.8);
          r.billboard(SPRITE_ADD, SHAPE.glow, x, ty, z, 0.4, 0xffffff, 0.7);
          if (Math.random() < 0.3) this.fx.sparks(e.x, ey, e.y, def.color, 1, 0.6, 0.08);
        }
      }
      if (stunned) {
        for (let k = 0; k < 3; k++) {
          const a = t * 4 + k * 2.09;
          r.billboard(SPRITE_ADD, SHAPE.star, x + Math.cos(a) * 0.3, by + top + 0.2, z + Math.sin(a) * 0.3, 0.16, 0xffd040, 0.95);
        }
      }
      if (tw.disrupted > 0) {
        r.add('overlay', 'box', x, by + top + 0.35, z, 0.36, 0.08, 0.08, 0xff4040, 1, 1, Math.PI / 4 + this.cam.yaw);
        r.add('overlay', 'box', x, by + top + 0.35, z, 0.36, 0.08, 0.08, 0xff4040, 1, 1, -Math.PI / 4 + this.cam.yaw);
      }
      if (ui.showRanges && def.attack !== 'economy' && def.attack !== 'none' && tw.uid !== ui.selectedTower) {
        const st = sim.c.towerStats.get(tw.uid);
        if (st && st.range > 0) r.ground(SPRITE_ADD, SHAPE.ring, x, by + 0.03, z, st.range, def.color, 0.3, 0, 0.3, 0.015);
      }
    }
  }

  private drawSelection(sim: GameSimulation, ui: ViewUiState): void {
    const r = this.r;
    const s = sim.s;
    const map = s.map;
    const t = this.time;
    const sel = ui.selectedTower ? sim.c.towerByUid.get(ui.selectedTower) : undefined;
    if (sel) {
      const st = sim.c.towerStats.get(sel.uid);
      const by = cellTop(map, sel.cx, sel.cy);
      const col = TOWERS[sel.def].color;
      const sc = towerCenter(sel);
      const ssz = towerSize(sel.def);
      r.ground(SPRITE_ADD, SHAPE.ring, sc.x, by + 0.05, sc.y, 0.62 * ssz, 0xffffff, 0.9, t, 0.3, 0.12);
      if (st && st.range > 0) {
        r.add('transparent', 'disc', sc.x, by + 0.025, sc.y, st.range, 1, st.range, col, 0.1, 1);
        r.ground(SPRITE_ADD, SHAPE.ring, sc.x, by + 0.035, sc.y, st.range, col, 0.8, 0, 0.3, 0.025);
      }
      if (sel.def === 'pylon' && st) r.ground(SPRITE_ADD, SHAPE.ring, sc.x, by + 0.04, sc.y, st.linkRange, 0x5ab8ff, 0.7, 0, 0.3, 0.02);
    }

    // Bauvorschau
    const ghostDef: TowerId | null = ui.buildDef ?? ui.moveDef ?? null;
    if (ghostDef && ui.hoverCell) {
      const { x: hx, y: hy } = ui.hoverCell;
      const ok = ui.preview?.ok ?? false;
      const top = cellTop(map, hx, hy);
      const gsz = towerSize(ghostDef);
      const gc = gsz / 2;
      r.add('transparent', 'box', hx + gc, top + 0.03, hy + gc, gsz - 0.02, 0.04, gsz - 0.02, ok ? 0x60ff80 : 0xff4040, 0.5, 1);
      const def = TOWERS[ghostDef];
      this.drawParts(TOWER_MODELS[ghostDef], hx + gc, top, hy + gc, 0, 0, 0.9 * UNIT * (gsz > 1 ? 1.75 : 1), def.color, 0xffffff, 'transparent', 0.55, ok ? null : 0xff3030, ok ? 0 : 0.6, 0.3, 0, 0, 0, 0);
      const range = (def.base.range ?? 0) * (map.heights[hy * map.width + hx] >= 2 ? 1.12 : 1) * sim.c.mods.rangeMul;
      if (range > 0) {
        r.add('transparent', 'disc', hx + 0.5, top + 0.02, hy + 0.5, range, 1, range, def.color, 0.08, 1);
        r.ground(SPRITE_ADD, SHAPE.ring, hx + 0.5, top + 0.03, hy + 0.5, range, ok ? 0xffffff : 0xff6060, 0.7, 0, 0.3, 0.025);
      }
      if (ui.fusionRing && ui.fusionRing > 0) {
        r.ground(SPRITE_ADD, SHAPE.ring, hx + 0.5, top + 0.04, hy + 0.5, ui.fusionRing, 0xffd84a, 0.75, t * 0.5, 0.3, 0.03);
      }
      if (ghostDef === 'pylon') {
        const lr = def.base.linkRange ?? 3;
        r.ground(SPRITE_ADD, SHAPE.ring, hx + 0.5, top + 0.035, hy + 0.5, lr, 0x5ab8ff, 0.7, 0, 0.3, 0.02);
      }
      const route = ui.preview?.route;
      if (route && route.length > 1) {
        for (let i = 0; i < route.length - 1; i++) {
          const a = route[i];
          const b = route[i + 1];
          r.line(a.x + 0.5, cellTop(map, a.x, a.y) + 0.1, a.y + 0.5, b.x + 0.5, cellTop(map, b.x, b.y) + 0.1, b.y + 0.5, 0.06, ok ? 0xfff0a0 : 0xff6060, 0.9, true, true);
        }
      }
    }

    // Heldenplatzierung
    if (ui.heroPlacing && ui.hoverCell) {
      const { x: hx, y: hy } = ui.hoverCell;
      const ok = canPlaceHero(sim, hx, hy).ok;
      const top = cellTop(map, hx, hy);
      const def = HEROES[ui.heroPlacing];
      r.ground(SPRITE_ADD, SHAPE.ring, hx + 0.5, top + 0.04, hy + 0.5, 0.55, ok ? def.accent : 0xff4040, 0.9, t, 0.3, 0.15);
      this.drawParts(HERO_MODELS[ui.heroPlacing], hx + 0.5, top, hy + 0.5, -Math.PI / 4, -Math.PI / 4, 0.95 * UNIT, def.color, def.accent, 'transparent', 0.55, ok ? null : 0xff3030, ok ? 0 : 0.5, 0.3, 0, 0, 0, 0);
      const st = heroStatsOfDef(ui.heroPlacing);
      if (st > 0) r.ground(SPRITE_ADD, SHAPE.ring, hx + 0.5, top + 0.04, hy + 0.5, st, def.accent, 0.5, 0, 0.3, 0.02);
    }
    // Zielvorschau für Heldenfähigkeiten
    if (ui.abilityRadius && ui.abilityRadius > 0 && ui.hoverCell) {
      const { x: hx, y: hy } = ui.hoverCell;
      const top = cellTop(map, hx, hy);
      r.ground(SPRITE_ADD, SHAPE.ring, hx + 0.5, top + 0.05, hy + 0.5, ui.abilityRadius, 0xffd860, 0.85, 0, 0.3, 0.04);
      r.ground(false, SHAPE.disc, hx + 0.5, top + 0.04, hy + 0.5, ui.abilityRadius, 0xffd860, 0.12, 0, 0.2);
    }
  }

  // ------------------------------------------------------------ Zonen

  private drawZones(sim: GameSimulation): void {
    const r = this.r;
    const fx = this.fx;
    const t = this.time;
    for (const z of sim.s.zones) {
      const y = cellTop(sim.s.map, Math.floor(z.x), Math.floor(z.y)) + 0.04;
      if (z.kind === 'mine') {
        const tw = sim.c.towerByUid.get(z.link);
        const spore = tw?.def === 'sporemine';
        const body = spore ? 0x6a9a3a : 0x3a3a34;
        const lamp = spore ? 0xb0ff60 : 0xff4030;
        if (z.armed > 0) {
          // Wurfbahn vom Turm zur Landestelle
          if (!tw) continue;
          const c = towerCenter(tw);
          const total = 0.55;
          const p = Math.max(0, Math.min(1, 1 - z.armed / total));
          const sy = cellTop(sim.s.map, tw.cx, tw.cy) + TOWER_TOP[tw.def] * 0.9 * UNIT;
          const mx = c.x + (z.x - c.x) * p;
          const mz = c.y + (z.y - c.y) * p;
          const my = sy + (y - sy) * p + Math.sin(p * Math.PI) * 1.6;
          r.add('opaque', 'sphere', mx, my, mz, 0.2, 0.14, 0.2, body, 1, 0, t * 8, 0);
          r.ground(false, SHAPE.disc, z.x, y, z.y, 0.25 * p, 0x000000, 0.25, 0, 0.6);
          continue;
        }
        if (z.t <= 0) continue;
        r.add('opaque', 'cyl8', z.x, y - 0.02, z.y, 0.3, 0.07, 0.3, body, 1);
        r.add('opaque', 'sphere', z.x, y + 0.06, z.y, 0.18, 0.08, 0.18, spore ? 0x8ad040 : 0x55554c, 1);
        const blink = Math.sin(t * 5 + z.uid) > 0.4;
        r.add('opaque', 'sphere', z.x, y + 0.11, z.y, 0.06, 0.06, 0.06, blink ? lamp : 0x301810, 1, blink ? 0.8 : 0);
        if (blink) r.billboard(SPRITE_ADD, SHAPE.glow, z.x, y + 0.12, z.y, 0.22, lamp, 0.35);
        continue;
      }
      if (z.kind === 'meteor' && z.armed > 0) {
        // fallender Meteor mit Feuerschweif und Zielmarkierung
        const h = Math.min(9, z.armed * 7);
        const mx = z.x - h * 0.35;
        const mz = z.y - h * 0.2;
        const my = y + h;
        const big = z.r >= 1.5;
        const size = big ? 0.55 : 0.32;
        r.add('opaque', 'gem', mx, my, mz, size, size, size, 0x4a2a20, 1, 0.4, t * 5, t * 3);
        r.billboard(SPRITE_ADD, SHAPE.glow, mx, my, mz, size * 3, 0xff7a20, 0.9);
        if (Math.random() < 0.8 * fx.intensity) {
          fx.emit({ x: mx, y: my, z: mz, vx: 0.3, vy: 0.6, vz: 0.2, life: 0.4, c0: 0xffd080, c1: 0xc02000, s0: size * 1.6, s1: 0.1 });
          if (Math.random() < 0.4) fx.smoke(mx, my, mz, size * 1.5, 0x3a3030, 0.35, 0.9);
        }
        const p = 1 - Math.min(1, z.armed / 1.2);
        r.ground(SPRITE_ADD, SHAPE.ring, z.x, y, z.y, z.r, 0xff5020, 0.5 + p * 0.4, t, 0.3, 0.08);
        r.ground(false, SHAPE.disc, z.x, y, z.y, z.r * Math.max(0.1, p), 0xff3010, 0.2, 0, 0.5);
        continue;
      }
      if (z.armed > 0) {
        const blink = 0.35 + 0.25 * Math.sin(t * 12);
        const col = z.kind === 'nullPulse' ? 0x40e0ff : z.kind === 'riftJump' || z.kind.startsWith('portal') ? 0xd060ff : z.kind === 'devour' ? 0xa02050 : 0xff3a20;
        r.ground(false, SHAPE.disc, z.x, y, z.y, z.r, col, blink * 0.5, 0, 0.15);
        r.ground(SPRITE_ADD, SHAPE.ring, z.x, y + 0.01, z.y, z.r, col, 0.95, 0, 0.3, 0.06);
        const total = ZONE_WINDUP[z.kind] ?? 1.5;
        const p = 1 - Math.max(0, z.armed) / total;
        r.ground(SPRITE_ADD, SHAPE.ring, z.x, y + 0.02, z.y, Math.max(0.05, z.r * p), 0xffffff, 0.9, 0, 0.3, 0.08);
        continue;
      }
      if (z.t <= 0) continue;
      switch (z.kind) {
        case 'poisonCloud':
          for (let k = 0; k < 4; k++) {
            const a = k * 1.57 + t * 0.6;
            r.billboard(false, SHAPE.smoke, z.x + Math.cos(a) * z.r * 0.4, y + 0.35 + Math.sin(t * 2 + k) * 0.06, z.y + Math.sin(a) * z.r * 0.4, z.r * 0.75, 0x8ad040, 0.32, a, 1, k * 3.1 + z.uid);
          }
          r.ground(false, SHAPE.disc, z.x, y, z.y, z.r, 0x6ab020, 0.25, 0, 0.4);
          if (Math.random() < 0.3 * fx.intensity) fx.emit({ x: z.x + (Math.random() - 0.5) * z.r, y: y + 0.1, z: z.y + (Math.random() - 0.5) * z.r, vy: 0.5, life: 0.8, c0: 0xc0ff60, s0: 0.08, s1: 0.03 });
          break;
        case 'fireZone':
        case 'burnTrap': {
          r.ground(SPRITE_ADD, SHAPE.glow, z.x, y, z.y, z.r * 1.2, 0xff5010, 0.55, 0, 0.3);
          if (z.kind === 'burnTrap') r.ground(SPRITE_ADD, SHAPE.ring, z.x, y + 0.01, z.y, z.r, 0xffa040, 0.7, t, 0.3, 0.06);
          const n = Math.round(z.r * 3 * fx.intensity);
          for (let k = 0; k < n; k++) {
            if (Math.random() < 0.5) {
              const a = Math.random() * Math.PI * 2;
              const d = Math.sqrt(Math.random()) * z.r * 0.8;
              fx.fire(z.x + Math.cos(a) * d, y, z.y + Math.sin(a) * d, 0.8);
            }
          }
          break;
        }
        case 'portalIn':
        case 'portalOut': {
          const col = z.kind === 'portalIn' ? 0xc060ff : 0xff80ff;
          r.add('opaque', 'torus', z.x, y + 0.65, z.y, 1.3, 1.3, 1.6, col, 1, 1, t * 2, 0, Math.PI / 2);
          r.add('transparent', 'disc', z.x, y + 0.65, z.y, 0.55, 1, 0.55, 0x401060, 0.85, 1, t * 2, 0, Math.PI / 2);
          r.billboard(SPRITE_ADD, SHAPE.glow, z.x, y + 0.65, z.y, 1.3, col, 0.5);
          break;
        }
        case 'leyNode':
          r.ground(SPRITE_ADD, SHAPE.ring, z.x, y, z.y, z.r, 0xc070ff, 0.5, t * 0.5, 0.3, 0.04);
          r.line(z.x, y, z.y, z.x, y + 2.5, z.y, 0.18, 0xc070ff, 0.35 + Math.sin(t * 4) * 0.1);
          r.billboard(SPRITE_ADD, SHAPE.glow, z.x, y + 0.3, z.y, 0.8, 0xc070ff, 0.6);
          break;
        default:
          break;
      }
    }
  }

  // ------------------------------------------------------------ Gegner

  private drawEnemies(sim: GameSimulation, ui: ViewUiState, dt: number): void {
    const seen = new Set<number>();
    const k = 1 - Math.exp(-dt * 22);
    const [rx, rz] = this.cam.rightVector();
    const twins: EnemyInstance[] = [];
    for (const e of sim.s.enemies) {
      if (!e.alive) continue;
      seen.add(e.uid);
      let sm = this.smooth.get(e.uid);
      if (!sm) {
        sm = { x: e.x, y: e.y, h: cellTop(sim.s.map, Math.floor(e.x), Math.floor(e.y)), head: 0, speed: 0, def: e.def, boss: e.boss, elite: e.elite, seen: this.time };
        this.smooth.set(e.uid, sm);
        if (e.boss && !this.bossSeen.has(e.uid)) {
          this.bossSeen.add(e.uid);
          if (!ui.reducedMotion && !ui.cinematic) this.cam.focus(e.x, e.y, 0.5, 3.2);
          this.flash = Math.max(this.flash, 0.25);
          this.fx.pillar(e.x, 0, e.y, 8, BOSSES[e.boss].color, 1.2, 0.8);
          this.fx.ring(e.x, 0, e.y, 3, BOSSES[e.boss].color, 1, 0.2);
          this.onBossAppear?.(e.boss);
        }
      }
      const dx = e.x - sm.x;
      const dz = e.y - sm.y;
      if (dx * dx + dz * dz > 2.5) {
        sm.x = e.x;
        sm.y = e.y;
      } else {
        sm.x += dx * k;
        sm.y += dz * k;
      }
      const sp = Math.hypot(dx, dz) / Math.max(dt, 1e-3);
      sm.speed += (Math.min(1, sp * 2) - sm.speed) * Math.min(1, dt * 8);
      if (Math.abs(dx) + Math.abs(dz) > 0.002) {
        const target = Math.atan2(dz, dx);
        let d = target - sm.head;
        while (d > Math.PI) d -= Math.PI * 2;
        while (d < -Math.PI) d += Math.PI * 2;
        sm.head += d * Math.min(1, dt * 10);
      }
      const ground = cellTop(sim.s.map, Math.floor(sm.x), Math.floor(sm.y));
      sm.h += (ground - sm.h) * Math.min(1, dt * 10);
      this.drawEnemy(sim, e, sm, rx, rz, dt);
      if (ui.selectedEnemy === e.uid) {
        const size = e.boss ? BOSSES[e.boss].size : ENEMIES[e.def].size * (e.elite ? 1.35 : 1);
        const rr = Math.max(0.42, size * 0.75);
        this.r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.06, sm.y, rr, 0xff6060, 0.95, this.time * 2, 0.3, 0.12);
        this.r.billboard(SPRITE_ADD, SHAPE.star, sm.x, sm.h + size * 1.6 + 0.6 + Math.sin(this.time * 5) * 0.06, sm.y, 0.22, 0xff8080, 0.9, this.time);
      }
      if (e.boss === 'twinSol' || e.boss === 'twinLuna') twins.push(e);
    }
    for (const uid of this.smooth.keys()) if (!seen.has(uid)) this.smooth.delete(uid);
    // Zwillingsband
    if (twins.length === 2) {
      const [a, b] = twins;
      if ((a.x - b.x) ** 2 + (a.y - b.y) ** 2 <= 9) {
        const sa = this.smooth.get(a.uid);
        const sb = this.smooth.get(b.uid);
        if (sa && sb) {
          const pts = jag(sa.x, sa.h + 1.6, sa.y, sb.x, sb.h + 1.6, sb.y);
          for (let i = 0; i + 5 < pts.length; i += 3) {
            this.r.line(pts[i], pts[i + 1], pts[i + 2], pts[i + 3], pts[i + 4], pts[i + 5], 0.18, 0xffe0a0, 0.3);
            this.r.line(pts[i], pts[i + 1], pts[i + 2], pts[i + 3], pts[i + 4], pts[i + 5], 0.04, 0xffffff, 0.8);
          }
        }
      }
    }
  }

  private drawEnemy(_sim: GameSimulation, e: EnemyInstance, sm: Smooth, rx: number, rz: number, dt: number): void {
    const r = this.r;
    const fx = this.fx;
    const t = this.time;
    const yaw = -sm.head;
    const has = (id: StatusId): boolean => e.statuses.some((q) => q.id === id);
    const frozen = has('freeze');
    const stunned = has('stun');
    const moving = frozen || stunned || e.blockedBy ? 0 : sm.speed;
    const walk = t * (e.boss ? 5 : 10) + e.uid;
    const hop = moving * Math.abs(Math.sin(walk)) * 0.04;
    let tint: number | null = null;
    let tintAmt = 0;
    if (frozen) { tint = 0xd8f4ff; tintAmt = 0.6; }
    else if (has('chill')) { tint = 0x80c8ff; tintAmt = 0.35; }
    else if (has('poison')) { tint = 0x9ae040; tintAmt = 0.22; }
    if (e.enraged) { tint = 0xff4020; tintAmt = 0.25 + Math.sin(t * 10) * 0.1; }
    let emitBoost = 0;
    let squash = 1;
    if (e.hitFlash > 0) {
      tint = 0xffffff;
      tintAmt = Math.min(0.7, e.hitFlash * 4);
      emitBoost = Math.min(0.8, e.hitFlash * 5);
      squash = 1 - Math.min(0.12, e.hitFlash * 0.8);
    }
    const phased = e.phaseShift > 0;
    const hidden = e.stealthed;
    const pass: Pass = phased || hidden ? 'transparent' : 'opaque';
    const alpha = hidden ? 0.22 + Math.sin(t * 6 + e.uid) * 0.06 : phased ? 0.38 : 1;
    let height: number;
    let barW: number;
    const fly = e.flying ? 1.1 + Math.sin(t * 3 + e.uid) * 0.08 : 0;
    const baseY = sm.h + fly + hop;
    if (e.burrowed) {
      // Erdhügel statt Körper
      r.add('opaque', 'sphere', sm.x, sm.h, sm.y, 0.6, 0.28, 0.6, 0x6a5a40, 1, 0, t);
      if (Math.random() < dt * 6 * fx.intensity) fx.smoke(sm.x, sm.h + 0.1, sm.y, 0.35, 0x7a6a50, 0.45, 0.7);
      height = 0.4;
      barW = 0.6;
    } else if (e.boss) {
      const b = BOSSES[e.boss];
      const sc = b.size * UNIT * (e.challenge ? 1.25 : 1);
      if (e.challenge) {
        r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.05, sm.y, b.size * 1.1, 0xff3030, 0.7 + Math.sin(t * 5) * 0.2, t * 2, 0.3, 0.1);
        if (e.chEnraged && Math.random() < 0.4 * fx.intensity) fx.fire(sm.x + (Math.random() - 0.5) * 0.6, sm.h + 0.3, sm.y + (Math.random() - 0.5) * 0.6, 0.9);
      }
      this.drawParts(BOSS_MODELS[e.boss], sm.x, baseY, sm.y, yaw, yaw, sc, b.color, 0xffffff, pass, alpha, tint, tintAmt, emitBoost, walk, moving, 0, e.uid, squash);
      height = BOSS_HEIGHT[e.boss] * sc + 0.3;
      barW = 1.8;
      if (e.shield > 0) {
        r.add('transparent', 'sphere', sm.x, sm.h + height * 0.45, sm.y, height * 1.05, height * 1.05, height * 1.05, e.boss === 'nullWarden' ? 0x40e0ff : 0x90b0ff, 0.16, 1);
        r.billboard(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + height * 0.45, sm.y, height * 0.55, 0x90d0ff, 0.35, t);
      }
      // Phasenaura
      if (e.phase >= 2) {
        r.ground(SPRITE_ADD, SHAPE.glow, sm.x, sm.h + 0.04, sm.y, 2.2 + Math.sin(t * 3) * 0.2, b.color, 0.45, 0, 0.3);
        if (Math.random() < 0.4 * fx.intensity) fx.emit({ x: sm.x + (Math.random() - 0.5) * 1.6, y: sm.h + 0.2, z: sm.y + (Math.random() - 0.5) * 1.6, vy: 1.5, life: 0.8, c0: mixColor(b.color, 0xffffff, 0.4), c1: b.color, s0: 0.16, s1: 0.03 });
      }
      // Schwachpunkte
      if (e.plates.length > 0) {
        const cy = Math.cos(yaw), sy = Math.sin(yaw);
        for (let i = 0; i < WEAKPOINT_POS.length; i++) {
          const hp = e.plates[i] ?? 0;
          if (hp <= 0) continue;
          const [px, py, pz] = WEAKPOINT_POS[i];
          const ox = (px * cy + pz * sy) * sc;
          const oz = (-px * sy + pz * cy) * sc;
          const wy = baseY + py * sc;
          const pulse = 1 + Math.sin(t * 5 + i) * 0.12;
          r.add('opaque', 'gem', sm.x + ox, wy, sm.y + oz, 0.32 * sc * pulse, 0.32 * sc * pulse, 0.32 * sc * pulse, 0xff3030, 1, 1.5, t * 2);
          r.billboard(SPRITE_ADD, SHAPE.glow, sm.x + ox, wy, sm.y + oz, 0.7 * sc, 0xff4040, 0.6 * (hp / Math.max(1, e.plateMax)) + 0.2);
        }
      }
      if (e.swallowed.length > 0) r.billboard(SPRITE_ADD, SHAPE.glow, sm.x, baseY + 0.8, sm.y, 1.2, 0xffd060, 0.25 + Math.sin(t * 4) * 0.1);
      r.ground(false, SHAPE.disc, sm.x, sm.h + 0.03, sm.y, b.size * 0.9 * UNIT, 0x000000, 0.25, 0, 0.6);
    } else {
      const def = ENEMIES[e.def];
      const sc = def.size * 2 * (e.elite ? 1.35 : 1) * UNIT;
      const parts = ENEMY_MODELS[e.def as Exclude<EnemyId, 'boss'>];
      this.drawParts(parts, sm.x, baseY, sm.y, yaw, yaw, sc, def.color, 0xffffff, pass, alpha, tint, tintAmt, emitBoost, walk, e.flying ? 0 : moving, 0, e.uid, squash);
      height = (e.flying ? fly : 0) + sc * 0.95 + 0.2;
      barW = e.elite ? 0.9 : 0.6;
      if (e.elite) {
        r.add('opaque', 'torus', sm.x, baseY + sc * 0.95 + 0.05, sm.y, 0.36, 0.36, 0.36, 0xffd84a, alpha, 0.9, t * 2);
        r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.03, sm.y, 0.45 * sc, 0xffd84a, 0.45, t, 0.3, 0.12);
      }
      if (e.mutation) {
        const mc = MUTATIONS[e.mutation].color;
        r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.035, sm.y, 0.55 * sc, mc, 0.7, -t * 2, 0.3, 0.1);
        switch (e.mutation) {
          case 'armored':
            r.add(pass, 'rbox', sm.x, baseY + sc * 0.7, sm.y, sc * 0.75, sc * 0.18, sc * 0.75, 0x8a90a0, alpha, 0, yaw);
            break;
          case 'hasty':
            if (moving > 0.3 && Math.random() < 0.5 * fx.intensity) fx.emit({ shape: SHAPE.spark, x: sm.x, y: baseY + sc * 0.4, z: sm.y, vx: -Math.cos(sm.head) * 2, vz: -Math.sin(sm.head) * 2, life: 0.25, c0: 0xfff080, s0: 0.12, s1: 0.04, stretch: 0.25 });
            break;
          case 'explosive':
            r.billboard(SPRITE_ADD, SHAPE.glow, sm.x, baseY + sc * 0.5, sm.y, sc * (0.9 + Math.sin(t * 8 + e.uid) * 0.2), 0xff4020, 0.55);
            break;
          case 'splitting':
            for (let k = 0; k < 2; k++) {
              const a = t * 3 + k * Math.PI;
              r.add(pass, 'sphereLo', sm.x + Math.cos(a) * sc * 0.6, baseY + sc * 0.5, sm.y + Math.sin(a) * sc * 0.6, sc * 0.22, sc * 0.22, sc * 0.22, MUTATIONS.splitting.color, alpha, 0.4);
            }
            break;
          case 'vampiric':
            r.ground(SPRITE_ADD, SHAPE.glow, sm.x, sm.h + 0.04, sm.y, sc * 1.1, 0xc01840, 0.4, 0, 0.3);
            break;
        }
      }
      if (e.shield > 0) r.add('transparent', 'sphere', sm.x, baseY + sc * 0.45, sm.y, sc * 1.25, sc * 1.25, sc * 1.25, 0x90b8ff, 0.2, 1);
      if (e.flying) r.ground(false, SHAPE.disc, sm.x, sm.h + 0.03, sm.y, 0.3 * sc, 0x000000, 0.25, 0, 0.7);
      if (hidden && Math.random() < dt * 4) fx.emit({ x: sm.x, y: baseY + sc * 0.5, z: sm.y, vy: 0.4, life: 0.5, c0: 0x8070a0, s0: 0.15, s1: 0.02, a: 0.4 });
    }

    // Statuseffekte am Körper
    const mid = baseY + Math.min(height, 2.4) * 0.45;
    if (has('burn') && Math.random() < dt * 14 * fx.intensity) fx.fire(sm.x + (Math.random() - 0.5) * 0.3, mid - 0.1, sm.y + (Math.random() - 0.5) * 0.3, 0.7);
    if (has('poison') && Math.random() < dt * 5 * fx.intensity) fx.emit({ x: sm.x + (Math.random() - 0.5) * 0.4, y: mid, z: sm.y + (Math.random() - 0.5) * 0.4, vy: 0.7, life: 0.6, c0: 0xc0ff60, c1: 0x60a020, s0: 0.1, s1: 0.03 });
    if (has('wet') && Math.random() < dt * 4) fx.emit({ x: sm.x + (Math.random() - 0.5) * 0.3, y: mid, z: sm.y + (Math.random() - 0.5) * 0.3, vy: -0.5, grav: 6, life: 0.4, c0: 0x80b0ff, s0: 0.06, s1: 0.03, add: false, shape: SHAPE.disc, a: 0.8 });
    if (frozen) r.add('transparent', 'rbox', sm.x, sm.h + Math.min(height, 2) * 0.42, sm.y, 0.85, Math.min(height, 2) * 0.9, 0.85, 0xd8f4ff, 0.35, 0.5, yaw);
    if (stunned) {
      for (let k = 0; k < 3; k++) {
        const a = t * 5 + k * 2.09;
        r.billboard(SPRITE_ADD, SHAPE.star, sm.x + Math.cos(a) * 0.3, sm.h + height + 0.05, sm.y + Math.sin(a) * 0.3, 0.15, 0xffff80, 0.95);
      }
    }
    if (has('root')) r.add('opaque', 'torus', sm.x, sm.h + 0.08, sm.y, 0.7, 0.9, 0.7, 0x4a8a3a, 1, 0.2, t);
    if (has('mark') || has('riftMark')) r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.04, sm.y, 0.5, has('riftMark') ? 0xff60ff : 0xc070ff, 0.75, t * 2, 0.3, 0.1);
    if (has('voidMark')) r.billboard(SPRITE_ADD, SHAPE.glow, sm.x, mid, sm.y, 0.9, 0x8a40ff, 0.4 + Math.sin(t * 6) * 0.1);
    if (has('pull') && Math.random() < dt * 8) fx.emit({ x: sm.x, y: mid, z: sm.y, vx: (Math.random() - 0.5) * 2, vz: (Math.random() - 0.5) * 2, life: 0.3, c0: 0xa080ff, s0: 0.1, s1: 0.02 });

    // Fähigkeiten-Ankündigung
    for (const a of e.abilities) {
      if (a.windup <= 0) continue;
      const total = ABILITIES[a.id].windup;
      const p = 1 - a.windup / total;
      r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.06, sm.y, 0.8 * (1 - p) + 0.35, 0xffa040, 0.95, 0, 0.3, 0.12);
      r.billboard(SPRITE_ADD, SHAPE.glow, sm.x, mid, sm.y, 0.8 + p * 0.6, 0xffa040, 0.3 + p * 0.3);
      if (a.id === 'netBite' || a.id === 'siegeSlam' || a.id === 'devour') {
        r.line(sm.x, sm.h + 0.5, sm.y, a.tx, sm.h + 0.6, a.ty, 0.05, 0xff6040, 0.8, true, true);
      }
    }

    // Lebensbalken (Overlay)
    const by = sm.h + height + 0.15;
    const f = Math.max(0, e.hp / e.maxHp);
    const bx = sm.x;
    const bz = sm.y;
    r.add('overlay', 'box', bx, by, bz, barW + 0.05, 0.11, 0.02, 0x101010, 0.85, 0, this.cam.yaw);
    const fw = barW * f;
    const off = -(barW - fw) / 2;
    const col = e.boss ? 0xff5050 : f > 0.6 ? 0x60e060 : f > 0.3 ? 0xf0d040 : 0xf04040;
    r.add('overlay', 'box', bx + rx * off, by, bz + rz * off, Math.max(0.001, fw), 0.075, 0.03, col, 1, 0, this.cam.yaw);
    if (e.maxShield > 0 && e.shield > 0) {
      const sf = e.shield / e.maxShield;
      const sw = barW * sf;
      const so = -(barW - sw) / 2;
      r.add('overlay', 'box', bx + rx * so, by + 0.095, bz + rz * so, Math.max(0.001, sw), 0.05, 0.03, 0x70c8ff, 1, 0, this.cam.yaw);
    }
    // Status-Symbole (Form + Farbe)
    let n = 0;
    for (const st of e.statuses) {
      const g = STATUS_GLYPH[st.id];
      if (!g) continue;
      const off2 = -barW / 2 + 0.08 + n * 0.17;
      r.add('overlay', g.mesh, bx + rx * off2, by + 0.2, bz + rz * off2, 0.13, 0.13 * Math.abs(g.sy), 0.13, g.color, 1, 1, t * 2, g.sy < 0 ? Math.PI : 0);
      n++;
      if (n >= 6) break;
    }
    if (e.def === 'shield' && e.shield > 0) {
      let kk = 0;
      for (const [type, lvl] of Object.entries(e.adapt)) {
        for (let i = 0; i < lvl; i++) {
          const o = barW / 2 - 0.06 - kk * 0.1;
          r.add('overlay', 'box', bx + rx * o, by - 0.12, bz + rz * o, 0.08, 0.08, 0.08, DAMAGE_COLORS[type as keyof typeof DAMAGE_COLORS], 1, 1);
          kk++;
        }
      }
    }
  }

  private drawCorpses(dt: number): void {
    let w = 0;
    for (const c of this.corpses) {
      c.t += dt;
      if (c.t >= c.max) continue;
      const p = c.t / c.max;
      const alpha = (1 - p) * 0.8;
      const sink = p * (c.boss ? 1.2 : 0.35);
      this.drawParts(c.parts, c.x, c.y - sink, c.z, c.yaw, c.yaw, c.scale * (1 - p * 0.25), c.color, 0xffffff, 'transparent', alpha, 0xffffff, 0.3 + p * 0.5, 0.6 * (1 - p), 0, 0, 0, 0, 1 - p * 0.4);
      if (Math.random() < 0.6 * this.fx.intensity) {
        this.fx.emit({ x: c.x + (Math.random() - 0.5) * 0.5 * c.scale, y: c.y + Math.random() * c.scale, z: c.z + (Math.random() - 0.5) * 0.5 * c.scale, vy: 1.2, life: 0.5, c0: mixColor(c.color, 0xffffff, 0.5), c1: c.color, s0: 0.1, s1: 0.02 });
      }
      this.corpses[w++] = c;
    }
    this.corpses.length = w;
  }

  // ------------------------------------------------------------ Helden

  private drawHeroes(sim: GameSimulation, ui: ViewUiState, dt: number): void {
    const t = this.time;
    const s = sim.s;
    const [rx, rz] = this.cam.rightVector();
    const seen = new Set<number>();
    for (const h of s.heroes) {
      if (h.state !== 'active') continue;
      seen.add(h.uid);
      let sm = this.heroSmooth.get(h.uid);
      if (!sm) {
        sm = { x: h.x, y: h.y, h: cellTop(s.map, Math.floor(h.x), Math.floor(h.y)), speed: 0, born: t };
        this.heroSmooth.set(h.uid, sm);
      }
      const dx = h.x - sm.x;
      const dz = h.y - sm.y;
      const k = 1 - Math.exp(-dt * 18);
      if (dx * dx + dz * dz > 4) {
        sm.x = h.x;
        sm.y = h.y;
      } else {
        sm.x += dx * k;
        sm.y += dz * k;
      }
      const sp = Math.hypot(dx, dz) / Math.max(dt, 1e-3);
      sm.speed += (Math.min(1, sp * 2) - sm.speed) * Math.min(1, dt * 8);
      sm.h += (cellTop(s.map, Math.floor(sm.x), Math.floor(sm.y)) - sm.h) * Math.min(1, dt * 10);
      this.drawHero(sim, h, sm, rx, rz, ui);
    }
    for (const uid of this.heroSmooth.keys()) if (!seen.has(uid)) this.heroSmooth.delete(uid);
    void t;
  }

  private drawHero(sim: GameSimulation, h: HeroInstance, sm: { x: number; y: number; h: number; speed: number; born: number }, rx: number, rz: number, ui: ViewUiState): void {
    const r = this.r;
    const t = this.time;
    const def = HEROES[h.id];
    const st = heroStatsOf(sim, h);
    const appear = Math.min(1, (t - sm.born) / 0.35);
    const scale = 0.95 * UNIT * (appear < 1 ? easeOutBack(appear) : 1);
    const yaw = -h.facing;
    const invuln = h.buffKind === 'bulwark' && h.buffT > 0;
    this.drawParts(HERO_MODELS[h.id], sm.x, sm.h, sm.y, yaw, yaw, scale, def.color, def.accent, 'opaque', 1, invuln ? 0xffe080 : null, invuln ? 0.35 : 0, invuln ? 0.4 : 0.05, t * 9 + h.uid, sm.speed, h.anim * 3, h.uid);
    // Heldenkennzeichnung: Lichtkegel und Bodenring
    r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.04, sm.y, 0.48, def.accent, 0.8, t, 0.3, 0.14);
    r.ground(false, SHAPE.disc, sm.x, sm.h + 0.03, sm.y, 0.42, 0x000000, 0.25, 0, 0.6);
    const selected = ui.selectedHero === h.uid;
    if (st.auraRadius > 0 && (def.support || selected)) {
      r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.035, sm.y, st.auraRadius, def.accent, selected ? 0.55 : 0.28, 0, 0.3, 0.02);
      r.ground(SPRITE_ADD, SHAPE.disc, sm.x, sm.h + 0.03, sm.y, st.auraRadius, def.accent, 0.05 + Math.sin(t * 2) * 0.02, 0, 0.5);
    }
    if (def.support && Math.random() < 0.12 * this.fx.intensity) {
      const a = Math.random() * Math.PI * 2;
      const d = Math.random() * Math.max(0.5, st.auraRadius);
      this.fx.emit({ shape: SHAPE.star, x: sm.x + Math.cos(a) * d, y: sm.h + 0.2, z: sm.y + Math.sin(a) * d, vy: 0.6, life: 0.9, c0: def.accent, s0: 0.1, s1: 0.03 });
    }
    if (invuln) r.add('transparent', 'sphere', sm.x, sm.h + 0.55, sm.y, 1.3, 1.3, 1.3, 0xffe080, 0.18, 1);
    if (h.blocking > 0) r.billboard(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.55, sm.y, 0.6, 0x9ab8ff, 0.3, t);
    if (selected) {
      r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.05, sm.y, 0.62, 0xffffff, 0.9, -t, 0.3, 0.1);
      // Ankerfahne
      const ay = cellTop(sim.s.map, Math.floor(h.ax), Math.floor(h.ay));
      r.add('opaque', 'cyl8', h.ax, ay, h.ay, 0.04, 0.8, 0.04, 0xd0c8b8);
      r.add('opaque', 'leaf', h.ax, ay + 0.72, h.ay, 0.3, 1, 0.35, def.accent, 1, 0.5, Math.PI / 2, 0, Math.PI / 2 + Math.sin(t * 4) * 0.2);
      r.ground(SPRITE_ADD, SHAPE.ring, h.ax, ay + 0.04, h.ay, 0.35, def.accent, 0.8, t, 0.3, 0.15);
      if ((h.ax - sm.x) ** 2 + (h.ay - sm.y) ** 2 > 0.3) r.line(sm.x, sm.h + 0.1, sm.y, h.ax, ay + 0.1, h.ay, 0.03, def.accent, 0.5);
    }
    if (h.pendingPerk) r.billboard(SPRITE_ADD, SHAPE.star, sm.x, sm.h + 1.55 + Math.sin(t * 5) * 0.08, sm.y, 0.32, 0xffd860, 1, t, 1, 0, true);
    // Balken: LP (Heldenfarbe) + Einsatzzeit
    const by = sm.h + 1.3 * UNIT + 0.1;
    const barW = 0.8;
    r.add('overlay', 'box', sm.x, by, sm.y, barW + 0.05, 0.17, 0.02, 0x101010, 0.85, 0, this.cam.yaw);
    const f = Math.max(0, h.hp / Math.max(1, st.maxHp));
    const fw = barW * f;
    const off = -(barW - fw) / 2;
    r.add('overlay', 'box', sm.x + rx * off, by + 0.03, sm.y + rz * off, Math.max(0.001, fw), 0.07, 0.03, f > 0.35 ? 0x60e0a0 : 0xff6060, 1, 0, this.cam.yaw);
    const tf = Math.max(0, Math.min(1, h.activeLeft / Math.max(1, st.activeTime)));
    const tw = barW * tf;
    const toff = -(barW - tw) / 2;
    r.add('overlay', 'box', sm.x + rx * toff, by - 0.045, sm.y + rz * toff, Math.max(0.001, tw), 0.035, 0.03, 0x80b8ff, 1, 0, this.cam.yaw);
    for (let i = 0; i < h.level; i++) {
      const o = -barW / 2 + 0.06 + i * 0.13;
      r.add('overlay', 'octa', sm.x + rx * o, by + 0.17, sm.y + rz * o, 0.09, 0.12, 0.09, 0xffd84a, 1, 1);
    }
  }

  // ------------------------------------------------------------ Helden-Endlos: Held, Laden, Beute, Zonen

  private drawHeroEndless(sim: GameSimulation, ui: ViewUiState, dt: number): void {
    const he = sim.s.he;
    if (!he) return;
    const r = this.r;
    const t = this.time;
    const map = sim.s.map;
    const c = he.champion;
    const def = CHAMPIONS[c.id];
    // Laden (Gebäude auf der Ladenzelle)
    {
      const x = he.shop.x + 0.5;
      const z = he.shop.y + 0.5;
      const y = cellTop(map, he.shop.x, he.shop.y);
      r.add('opaque', 'rbox', x, y + 0.02, z, 0.96, 0.14, 0.96, 0x5a4a3a);
      r.add('opaque', 'rbox', x, y + 0.16, z, 0.8, 0.58, 0.7, 0x9a7650);
      r.add('opaque', 'box', x, y + 0.5, z + 0.36, 0.86, 0.06, 0.24, 0xc84a3a, 1, 0, 0, -0.5);
      r.add('opaque', 'cone4', x, y + 0.74, z, 0.72, 0.5, 0.72, 0x7a3a2a, 1, 0, Math.PI / 4);
      r.add('opaque', 'box', x, y + 0.24, z + 0.36, 0.26, 0.34, 0.03, 0x302018);
      r.add('opaque', 'rbox', x - 0.28, y + 0.16, z + 0.44, 0.18, 0.18, 0.18, 0x7a5a38);
      r.add('opaque', 'sphereLo', x + 0.3, y + 0.24, z + 0.46, 0.12, 0.16, 0.12, 0x60c0ff, 1, 0.5);
      r.add('opaque', 'cyl8', x + 0.46, y + 0.14, z - 0.3, 0.03, 1.1, 0.03, 0xd0c8b8);
      r.add('opaque', 'leaf', x + 0.46, y + 1.1, z - 0.3, 0.3, 1, 0.34, 0xffd860, 1, 0.3, Math.PI / 2, 0, Math.PI / 2 + Math.sin(t * 3) * 0.25);
      r.add('opaque', 'gem', x, y + 1.38 + Math.sin(t * 2) * 0.06, z, 0.14, 0.22, 0.14, 0xffd860, 1, 0.5, t);
      const inRange = (c.x - x) ** 2 + (c.y - z) ** 2 <= 2.2 * 2.2 && c.dead <= 0;
      r.ground(SPRITE_ADD, SHAPE.ring, x, y + 0.04, z, 2.2, 0xffd860, inRange ? 0.55 : 0.18, t * 0.3, 0.3, 0.015);
      if (inRange) r.ground(SPRITE_ADD, SHAPE.disc, x, y + 0.03, z, 2.2, 0xffd860, 0.06, 0, 0.5);
    }
    // Verstärkte Türme: kreisende Runen über dem Turm
    const buffed = new Set<number>();
    for (const b of he.towerBuffs) buffed.add(b.tower);
    for (const [uid, v] of sim.c.champBuff) if (v.dmg + v.speed + v.range + v.crit > 0.001) buffed.add(uid);
    for (const uid of buffed) {
      const tw = sim.c.towerByUid.get(uid);
      if (!tw) continue;
      const { x, y: z } = towerCenter(tw);
      const y = cellTop(map, tw.cx, tw.cy);
      const strong = he.towerBuffs.some((b) => b.tower === uid);
      r.ground(SPRITE_ADD, SHAPE.ring, x, y + 0.06, z, 0.58, def.accent, strong ? 0.75 : 0.35, -t * 1.5, 0.3, 0.1);
      if (strong) {
        for (let k = 0; k < 3; k++) {
          const a = t * 2 + (k * Math.PI * 2) / 3;
          r.billboard(SPRITE_ADD, SHAPE.star, x + Math.cos(a) * 0.42, y + 1.25 + Math.sin(t * 3 + k) * 0.08, z + Math.sin(a) * 0.42, 0.16, def.accent, 0.9, t);
        }
      }
    }
    // Zonen (Fallen, Pfeilhagel)
    for (const zn of he.zones) {
      const y = cellTop(map, Math.floor(zn.x), Math.floor(zn.y));
      const col = zn.kind === 'snare' ? 0x80ff90 : 0x9ad8ff;
      r.ground(SPRITE_ADD, SHAPE.ring, zn.x, y + 0.05, zn.y, zn.r, col, 0.7, t, 0.3, 0.05);
      r.ground(false, SHAPE.disc, zn.x, y + 0.04, zn.y, zn.r, col, 0.1, 0, 0.3);
      if (zn.kind === 'volley' && Math.random() < 0.4 * this.fx.intensity) {
        const a = Math.random() * Math.PI * 2;
        const d = Math.random() * zn.r;
        const px = zn.x + Math.cos(a) * d;
        const pz = zn.y + Math.sin(a) * d;
        this.fx.beam(px - 0.2, y + 2.2, pz - 0.2, px, y + 0.1, pz, col, 0.03, 0.12);
      }
    }
    // Abbau: Fortschrittsring am Hindernis, Staub und Splitter
    if (he.mining) {
      const mi = he.mining;
      const mx = (mi.cell % map.width) + 0.5;
      const mz = Math.floor(mi.cell / map.width) + 0.5;
      const my = cellTop(map, Math.floor(mx), Math.floor(mz));
      const p = 1 - Math.max(0, mi.t) / mi.total;
      r.ground(SPRITE_ADD, SHAPE.ring, mx, my + 0.05, mz, 0.62, 0x403020, 0.6, 0, 0.3, 0.12);
      r.ground(SPRITE_ADD, SHAPE.ring, mx, my + 0.06, mz, 0.62 * Math.max(0.05, p), 0xffd860, 0.9, t, 0.3, 0.12);
      if (Math.random() < 0.25 * this.fx.intensity) this.fx.smoke(mx + (Math.random() - 0.5) * 0.6, my + 0.3, mz + (Math.random() - 0.5) * 0.6, 0.3, 0x9a8a70, 0.35, 0.8);
      if (Math.random() < 0.18 * this.fx.intensity) this.fx.sparks(mx, my + 0.5, mz, 0xd0b080, 2, 0.6, 0.08);
    } else if (he.mineTarget !== undefined && he.mineTarget >= 0) {
      const mx = (he.mineTarget % map.width) + 0.5;
      const mz = Math.floor(he.mineTarget / map.width) + 0.5;
      r.ground(SPRITE_ADD, SHAPE.ring, mx, cellTop(map, Math.floor(mx), Math.floor(mz)) + 0.05, mz, 0.6, 0xffd860, 0.6 + Math.sin(t * 6) * 0.2, t, 0.3, 0.08);
    }
    // Beute am Boden
    for (const p of he.pickups) {
      const y = cellTop(map, Math.floor(p.x), Math.floor(p.y)) + 0.3 + Math.sin(t * 3 + p.uid) * 0.06;
      const fade = p.t < 5 ? 0.4 + 0.6 * Math.abs(Math.sin(t * 6)) : 1;
      let col = 0x80ff9a;
      let size = 0.18;
      if (p.kind === 'resource') col = RESOURCES[p.ref as keyof typeof RESOURCES]?.color ?? 0xffffff;
      else if (p.kind === 'fragment' || p.kind === 'item') {
        col = parseInt((RARITY[ITEMS[p.ref]?.rarity ?? 'common'].color).slice(1), 16);
        size = p.kind === 'item' ? 0.3 : 0.22;
      } else if (p.kind === 'potion') col = POTIONS[p.ref as keyof typeof POTIONS]?.color ?? 0xff6060;
      if (p.kind === 'xp') {
        r.billboard(SPRITE_ADD, SHAPE.glow, p.x, y, p.y, size * (1 + Math.min(1, p.amount / 40)), col, 0.9 * fade, t);
      } else {
        r.add('opaque', p.kind === 'potion' ? 'sphereLo' : 'gem', p.x, y, p.y, size * 0.6, size, size * 0.6, col, fade, 1.2, t * 2 + p.uid);
        r.billboard(SPRITE_ADD, SHAPE.glow, p.x, y, p.y, size * 2.4, col, 0.45 * fade, t);
        if (p.kind === 'item') r.add('transparent', 'cyl8', p.x, y - 0.3, p.y, 0.12, 2.2, 0.12, col, 0.18 * fade, 1);
      }
    }
    // Held
    if (c.dead > 0) {
      this.champSmooth = null;
      const g = map.goal;
      r.billboard(SPRITE_ADD, SHAPE.ring, g.x + 0.5, cellTop(map, g.x, g.y) + 1.4, g.y + 0.5, 0.5, 0x9ad8ff, 0.4 + Math.sin(t * 4) * 0.2, t);
      return;
    }
    let sm = this.champSmooth;
    if (!sm) {
      sm = { x: c.x, y: c.y, h: cellTop(map, Math.floor(c.x), Math.floor(c.y)), speed: 0 };
      this.champSmooth = sm;
    }
    const dx = c.x - sm.x;
    const dz = c.y - sm.y;
    const k = 1 - Math.exp(-dt * 18);
    if (dx * dx + dz * dz > 4) {
      sm.x = c.x;
      sm.y = c.y;
    } else {
      sm.x += dx * k;
      sm.y += dz * k;
    }
    const sp = Math.hypot(dx, dz) / Math.max(dt, 1e-3);
    sm.speed += (Math.min(1, sp * 2) - sm.speed) * Math.min(1, dt * 8);
    sm.h += (cellTop(map, Math.floor(sm.x), Math.floor(sm.y)) - sm.h) * Math.min(1, dt * 10);
    const st = championStats(he);
    const yaw = -c.facing;
    const storm = c.channel > 0;
    this.drawParts(HERO_MODELS[def.model], sm.x, sm.h, sm.y, storm ? t * 14 : yaw, storm ? t * 14 : yaw, 1.05 * UNIT, def.color, def.accent, 'opaque', 1, null, 0, 0.12, t * 9, sm.speed, c.anim * 3, 7);
    r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.04, sm.y, 0.5, def.accent, 0.9, t, 0.3, 0.16);
    r.ground(false, SHAPE.disc, sm.x, sm.h + 0.03, sm.y, 0.44, 0x000000, 0.28, 0, 0.6);
    r.billboard(SPRITE_ADD, SHAPE.glow, sm.x, sm.h + 0.6, sm.y, 1.1, def.accent, 0.15, t);
    if (storm) for (let q = 0; q < 2; q++) r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.3 + q * 0.15, sm.y, 1.6 + q * 0.3, 0xffffff, 0.35, t * 8, 0.3, 0.05);
    if (def.aura || ui.champSelected) {
      r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.035, sm.y, st.supportRadius, def.accent, ui.champSelected ? 0.5 : 0.22, 0, 0.3, 0.02);
      r.ground(SPRITE_ADD, SHAPE.disc, sm.x, sm.h + 0.03, sm.y, st.supportRadius, def.accent, 0.04 + Math.sin(t * 2) * 0.015, 0, 0.5);
    }
    if (ui.champSelected) {
      r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.05, sm.y, 0.66, 0xffffff, 0.9, -t, 0.3, 0.1);
      r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.03, sm.y, st.pickupRadius, 0x80ff9a, 0.25, 0, 0.3, 0.015);
    }
    if (ui.champCastRange && ui.champCastRange > 0) r.ground(SPRITE_ADD, SHAPE.ring, sm.x, sm.h + 0.05, sm.y, ui.champCastRange, 0xffffff, 0.6, 0, 0.3, 0.02);
    // Laufziel
    if (c.path.length) {
      const last = c.path[c.path.length - 1];
      const lx = (last % map.width) + 0.5;
      const lz = Math.floor(last / map.width) + 0.5;
      const ly = cellTop(map, Math.floor(lx), Math.floor(lz));
      r.ground(SPRITE_ADD, SHAPE.ring, lx, ly + 0.05, lz, 0.32 + Math.sin(t * 6) * 0.05, def.accent, 0.85, t * 2, 0.3, 0.15);
      let px = sm.x;
      let pz = sm.y;
      let ph = sm.h;
      for (const ci of c.path) {
        const nx = (ci % map.width) + 0.5;
        const nz = Math.floor(ci / map.width) + 0.5;
        const nh = cellTop(map, Math.floor(nx), Math.floor(nz));
        r.line(px, ph + 0.08, pz, nx, nh + 0.08, nz, 0.03, def.accent, 0.35);
        px = nx;
        pz = nz;
        ph = nh;
      }
    }
    // Balken über dem Helden: LP, Mana
    const [rx, rz] = this.cam.rightVector();
    const by = sm.h + 1.45 * UNIT + 0.1;
    const barW = 0.95;
    r.add('overlay', 'box', sm.x, by, sm.y, barW + 0.05, 0.19, 0.02, 0x101010, 0.85, 0, this.cam.yaw);
    const f = Math.max(0, c.hp / Math.max(1, st.maxHp));
    const fw = barW * f;
    r.add('overlay', 'box', sm.x + rx * (-(barW - fw) / 2), by + 0.035, sm.y + rz * (-(barW - fw) / 2), Math.max(0.001, fw), 0.08, 0.03, f > 0.35 ? 0x60e0a0 : 0xff6060, 1, 0, this.cam.yaw);
    const mf = st.maxMana > 0 ? Math.max(0, c.mana / st.maxMana) : 0;
    const mw = barW * mf;
    r.add('overlay', 'box', sm.x + rx * (-(barW - mw) / 2), by - 0.05, sm.y + rz * (-(barW - mw) / 2), Math.max(0.001, mw), 0.04, 0.03, 0x5a9aff, 1, 0, this.cam.yaw);
    if (c.skillPoints > 0) r.billboard(SPRITE_ADD, SHAPE.star, sm.x, sm.h + 1.75 * UNIT + 0.15 + Math.sin(t * 5) * 0.08, sm.y, 0.3, 0xffd860, 1, t, 1, 0, true);
  }

  // ------------------------------------------------------------ Geschosse

  private drawProjectiles(sim: GameSimulation): void {
    const r = this.r;
    const fx = this.fx;
    for (const p of sim.s.projectiles) {
      if (!p.alive) continue;
      const remaining = Math.hypot(p.tx - p.x, p.ty - p.y);
      const frac = p.traveled / Math.max(0.001, p.traveled + remaining);
      const base = cellTop(sim.s.map, Math.floor(p.x), Math.floor(p.y));
      const lob = p.kind === 'ember' || p.kind === 'spore' || p.kind === 'cannon';
      const y = base + 0.75 + (lob ? Math.sin(frac * Math.PI) * 1.3 : 0.25 * (1 - frac));
      const yaw = Math.atan2(-(p.ty - p.y), p.tx - p.x);
      const col = DAMAGE_COLORS[p.payload.type];
      switch (p.kind) {
        case 'ranger':
          r.add('opaque', 'rbox', p.x, y, p.y, 0.42, 0.04, 0.04, 0xe8dcc0, 1, 0.3, yaw);
          r.billboard(SPRITE_ADD, SHAPE.spark, p.x, y, p.y, 0.18, 0xfff0d0, 0.4, 0, 2);
          break;
        case 'ballista':
          r.add('opaque', 'rbox', p.x, y, p.y, 0.7, 0.07, 0.07, 0xd0c0a0, 1, 0.3, Math.atan2(-p.dirY, p.dirX));
          r.billboard(SPRITE_ADD, SHAPE.glow, p.x, y, p.y, 0.3, 0xffffff, 0.35);
          break;
        case 'ember':
          r.add('opaque', 'sphere', p.x, y, p.y, 0.26, 0.26, 0.26, 0xffa040, 1, 1.4);
          r.billboard(SPRITE_ADD, SHAPE.glow, p.x, y, p.y, 0.7, 0xff6020, 0.85);
          if (Math.random() < 0.8 * fx.intensity) fx.fire(p.x, y, p.y, 0.6);
          break;
        case 'spore':
          r.add('opaque', 'sphere', p.x, y, p.y, 0.22, 0.22, 0.22, 0x9ae040, 1, 0.7);
          r.billboard(SPRITE_ADD, SHAPE.glow, p.x, y, p.y, 0.45, 0x9ae040, 0.5);
          if (Math.random() < 0.4 * fx.intensity) fx.emit({ x: p.x, y, z: p.y, vy: -0.3, life: 0.5, c0: 0xc0ff70, s0: 0.1, s1: 0.03 });
          break;
        case 'cannon':
          r.add('opaque', 'sphere', p.x, y, p.y, 0.24, 0.24, 0.24, 0x2a2a2e, 1, 0);
          if (Math.random() < 0.5 * fx.intensity) fx.smoke(p.x, y, p.y, 0.25, 0x8a8480, 0.3, 0.6);
          break;
        default:
          r.add('opaque', 'sphereLo', p.x, y, p.y, 0.2, 0.2, 0.2, col, 1, 1.2);
          r.billboard(SPRITE_ADD, SHAPE.glow, p.x, y, p.y, 0.55, col, 0.8);
          if (Math.random() < 0.5 * fx.intensity) fx.emit({ x: p.x, y, z: p.y, life: 0.3, c0: col, s0: 0.2, s1: 0.03 });
      }
    }
  }

  // ------------------------------------------------------------ Wetter

  private drawWeather(sim: GameSimulation, dt: number, ui: ViewUiState): void {
    const map = this.mapRef;
    if (!map) return;
    const biome = BIOMES[map.biome];
    const ev = sim.s.currentWave?.event ?? null;
    this.drawSunRays(map);
    let kind: string = biome.weather;
    if (ev === 'blizzard') kind = 'blizzard';
    else if (ev === 'fog') kind = 'mist';
    else if (ev === 'glowtide' || ev === 'sporeBloom') kind = 'spores';
    else if (ev === 'heatwave') kind = 'embers';
    const base = kind === 'blizzard' ? 240 : kind === 'snow' ? 140 : kind === 'mist' ? 40 : 90;
    const want = Math.round(base * (ui.reducedMotion ? 0.4 : 1) * Math.min(1, ui.particleCap / 500) * (map.width * map.height) / 308);
    const W = map.width;
    const H = map.height;
    const spawn = (a: Ambient, fresh: boolean): void => {
      a.x = -2 + Math.random() * (W + 4);
      a.z = -2 + Math.random() * (H + 4);
      a.seed = Math.random() * 100;
      a.max = 3 + Math.random() * 5;
      a.life = fresh ? Math.random() * a.max : a.max;
      switch (kind) {
        case 'snow': case 'blizzard':
          a.y = fresh ? Math.random() * 7 : 7;
          a.vy = kind === 'blizzard' ? -2.2 : -0.7 - Math.random() * 0.4;
          a.vx = kind === 'blizzard' ? 2.5 : 0.2;
          a.vz = kind === 'blizzard' ? 0.8 : 0;
          break;
        case 'embers':
          a.y = Math.random() * 2;
          a.vy = 0.5 + Math.random() * 0.6;
          a.vx = (Math.random() - 0.5) * 0.3;
          a.vz = (Math.random() - 0.5) * 0.3;
          break;
        case 'wind':
          a.y = 0.5 + Math.random() * 3;
          a.vx = 4 + Math.random() * 3;
          a.vz = 1.5;
          a.vy = 0;
          break;
        case 'mist':
          a.y = 0.2 + Math.random() * 0.6;
          a.vx = 0.15;
          a.vz = 0.05;
          a.vy = 0;
          break;
        default:
          a.y = 0.3 + Math.random() * 2.2;
          a.vx = (Math.random() - 0.5) * 0.3;
          a.vz = (Math.random() - 0.5) * 0.3;
          a.vy = kind === 'spores' ? 0.15 : 0;
      }
    };
    while (this.ambient.length < want) {
      const a: Ambient = { x: 0, y: 0, z: 0, vx: 0, vy: 0, vz: 0, life: 0, max: 1, seed: 0 };
      spawn(a, true);
      this.ambient.push(a);
    }
    if (this.ambient.length > want) this.ambient.length = want;
    const r = this.r;
    const t = this.time;
    for (const a of this.ambient) {
      a.life -= dt;
      a.x += a.vx * dt;
      a.y += a.vy * dt;
      a.z += a.vz * dt;
      if (kind === 'fireflies' || kind === 'sparkles' || kind === 'spores') {
        a.x += Math.sin(t * 0.7 + a.seed) * dt * 0.3;
        a.z += Math.cos(t * 0.6 + a.seed * 1.3) * dt * 0.3;
      }
      if (kind === 'snow') a.x += Math.sin(t + a.seed) * dt * 0.3;
      if (a.life <= 0 || a.y < -0.5 || a.x > W + 3 || a.z > H + 3) {
        spawn(a, false);
        continue;
      }
      const fade = Math.min(1, a.life / 0.8, (a.max - a.life) / 0.8);
      switch (kind) {
        case 'fireflies': {
          const blink = Math.max(0, Math.sin(t * 2.2 + a.seed * 3));
          r.billboard(SPRITE_ADD, SHAPE.glow, a.x, a.y, a.z, 0.16, 0xd8ff80, blink * fade * 0.9);
          break;
        }
        case 'embers':
          r.billboard(SPRITE_ADD, SHAPE.glow, a.x, a.y, a.z, 0.1 + Math.sin(t * 9 + a.seed) * 0.03, 0xff8030, fade * 0.9);
          break;
        case 'snow':
        case 'blizzard':
          r.billboard(false, SHAPE.disc, a.x, a.y, a.z, kind === 'blizzard' ? 0.06 : 0.05, 0xffffff, fade * 0.85, 0, kind === 'blizzard' ? 2.2 : 1);
          break;
        case 'sparkles':
          r.billboard(SPRITE_ADD, SHAPE.star, a.x, a.y, a.z, 0.12, 0xfff0d0, fade * Math.max(0, Math.sin(t * 3 + a.seed * 5)) * 0.9, t);
          break;
        case 'wind':
          r.billboard(SPRITE_ADD, SHAPE.spark, a.x, a.y, a.z, 0.35, 0xffffff, fade * 0.25, 0.3, 4);
          break;
        case 'mist':
          r.billboard(false, SHAPE.smoke, a.x, a.y, a.z, 2.2, 0xc8d8c8, fade * 0.16, a.seed, 1.4, a.seed);
          break;
        case 'spores':
          r.billboard(SPRITE_ADD, SHAPE.glow, a.x, a.y, a.z, 0.12, (a.seed | 0) % 2 ? 0x60f0d0 : 0xc080ff, fade * 0.8);
          break;
        default:
          break;
      }
    }
  }

  /** 3.13: Lichtstrahlen (God Rays) schräg in Lichtrichtung über der Insel, langsam wandernd. */
  private drawSunRays(map: MapDefinition): void {
    const L = this.lighting;
    const m = MOOD[map.biome];
    if (!L || m.sun < 0.06) return;
    const r = this.r;
    const t = this.time;
    const [lx, ly, lz] = L.light;
    const col = mixColor(m.glow, 0xffffff, 0.35);
    for (let k = 0; k < 7; k++) {
      const ph = k * 2.399;
      const drift = (t * 0.05 + k / 7) % 1;
      const gx = map.width * (0.1 + 0.8 * ((Math.sin(ph) * 0.5 + 0.5) * 0.6 + drift * 0.4) % 1);
      const gz = map.height * (0.15 + 0.7 * ((Math.cos(ph * 1.7) * 0.5 + 0.5)));
      const a = Math.sin(drift * Math.PI) * (0.05 + 0.03 * Math.sin(t * 0.6 + ph));
      const len = 9;
      r.line(gx, 0, gz, gx + lx * len, ly * len, gz + lz * len, 0.7 + (k % 3) * 0.35, col, a);
    }
  }

  // ------------------------------------------------------------ Eingabehilfen

  /** Bildschirmposition → Zelle (berücksichtigt Geländehöhe näherungsweise). */
  pickCell(px: number, py: number, map: MapDefinition): GridPos | null {
    return pickGridCell(this.cam, map, px, py);
  }

  /** Weltposition (exakt) unter dem Zeiger – für Heldenanker. */
  pickPoint(px: number, py: number, map: MapDefinition): { x: number; y: number } | null {
    const c = this.pickCell(px, py, map);
    if (!c) return null;
    const hit = this.cam.screenToGround(px, py, cellTop(map, c.x, c.y));
    return hit ? { x: hit.x, y: hit.z } : { x: c.x + 0.5, y: c.y + 0.5 };
  }

  /**
   * Gegner unter dem Zeiger (Bildschirmabstand zum Körper, nicht nur zum Bodenfeld).
   * Bei Überlappung gewinnt der Gegner, der der Kamera am nächsten ist.
   */
  pickEnemy(px: number, py: number, sim: GameSimulation): number {
    let best = 0;
    let bd = Infinity;
    for (const e of sim.s.enemies) {
      if (!e.alive || e.stealthed) continue;
      const sm = this.smooth.get(e.uid);
      const x = sm?.x ?? e.x;
      const z = sm?.y ?? e.y;
      const h = sm?.h ?? cellTop(sim.s.map, Math.floor(e.x), Math.floor(e.y));
      const size = e.boss ? BOSSES[e.boss].size : ENEMIES[e.def].size * (e.elite ? 1.35 : 1);
      const lift = e.flying ? 1.1 : 0;
      const a = this.cam.worldToScreen(x, h + lift, z);
      const b = this.cam.worldToScreen(x, h + lift + Math.max(0.5, size * 1.4), z);
      const rad = Math.max(16, this.pixelsPerUnit() * Math.max(0.35, size * 0.6));
      const d = segDist(px, py, a.x, a.y, b.x, b.y);
      if (d > rad) continue;
      const score = d / rad - a.y * 1e-4;
      if (score < bd) {
        bd = score;
        best = e.uid;
      }
    }
    return best;
  }

  /** Turm unter dem Zeiger: das ganze Modell (Sockel bis Spitze) ist anklickbar. */
  pickTower(px: number, py: number, sim: GameSimulation): number {
    let best = 0;
    let bd = Infinity;
    const ppu = this.pixelsPerUnit();
    for (const t of sim.s.towers) {
      const { x, y: z } = towerCenter(t);
      const sz = towerSize(t.def);
      const base = cellTop(sim.s.map, t.cx, t.cy);
      const top = base + Math.max(0.5, (TOWER_TOP[t.def] ?? 1) * UNIT * (sz > 1 ? 1.75 : 1) + 0.15);
      const a = this.cam.worldToScreen(x, base, z);
      const b = this.cam.worldToScreen(x, top, z);
      const rad = Math.max(14, ppu * 0.42 * sz);
      const d = segDist(px, py, a.x, a.y, b.x, b.y);
      if (d > rad) continue;
      // Näher an der Kamera (weiter unten im Bild) gewinnt bei Überlappung
      const score = d / rad - a.y * 1e-3;
      if (score < bd) {
        bd = score;
        best = t.uid;
      }
    }
    return best;
  }

  /**
   * Hindernis (Baum, Fels, Kristall …) über das ganze Modell anklicken – nicht nur über das Bodenfeld.
   * Liefert den Zellindex oder -1. Lava (3) und Wolken (7) zählen nicht.
   */
  pickObstacle(px: number, py: number, sim: GameSimulation): number {
    const map = sim.s.map;
    const ppu = this.pixelsPerUnit();
    const rad = Math.max(12, ppu * 0.42);
    const d = map.decor;
    let best = -1;
    let bd = Infinity;
    for (let k = 0; k + 2 < d.length; k += 3) {
      const idx = d[k];
      const kind = d[k + 1];
      if (kind === 3 || kind === 7 || map.cells[idx] !== Cell.Blocked) continue;
      const x = (idx % map.width) + 0.5;
      const z = Math.floor(idx / map.width) + 0.5;
      const base = cellTop(map, idx % map.width, Math.floor(idx / map.width));
      const h = (kind === 0 || kind === 5 ? 1.3 : kind === 1 ? 0.7 : 0.95) * DECOR_SCALE;
      const a = this.cam.worldToScreen(x, base + 0.1, z);
      const b = this.cam.worldToScreen(x, base + h, z);
      const dist = segDist(px, py, a.x, a.y, b.x, b.y);
      if (dist > rad) continue;
      const score = dist / rad - a.y * 1e-3;
      if (score < bd) {
        bd = score;
        best = idx;
      }
    }
    return best;
  }

  /** Bildschirmpixel je Welteinheit (horizontal, aktuelle Zoomstufe). */
  pixelsPerUnit(): number {
    const [rx, rz] = this.cam.rightVector();
    const a = this.cam.worldToScreen(this.cam.tx, 0, this.cam.tz);
    const b = this.cam.worldToScreen(this.cam.tx + rx, 0, this.cam.tz + rz);
    return Math.hypot(b.x - a.x, b.y - a.y);
  }

  project(x: number, y: number, z: number): { x: number; y: number } {
    return this.cam.worldToScreen(x, y, z);
  }
}

// Kleiner Drache, der den Drachenhorst umkreist (Blickrichtung +X)
const DRAGON: Part[] = [
  { m: 'sphere', x: 0, y: 0, z: 0, sx: 0.9, sy: 0.5, sz: 0.5, c: -1 },
  { m: 'sphere', x: 0.5, y: 0.15, z: 0, sx: 0.38, sy: 0.32, sz: 0.32, c: -1 },
  { m: 'cone8', x: 0.62, y: 0.2, z: 0, sx: 0.14, sy: 0.3, sz: 0.14, c: 0xf0e0c0, roll: 0, pitch: -Math.PI / 2 },
  { m: 'sphereLo', x: 0.62, y: 0.22, z: 0.1, sx: 0.07, sy: 0.07, sz: 0.07, c: -2, e: 2 },
  { m: 'sphereLo', x: 0.62, y: 0.22, z: -0.1, sx: 0.07, sy: 0.07, sz: 0.07, c: -2, e: 2 },
  { m: 'leaf', x: 0, y: 0.1, z: 0.15, sx: 1.1, sy: 1, sz: 1.1, c: -1, wing: 1 },
  { m: 'leaf', x: 0, y: 0.1, z: -0.15, sx: 1.1, sy: 1, sz: -1.1, c: -1, wing: -1 },
  { m: 'cone8', x: -0.4, y: 0, z: 0, sx: 0.2, sy: 0.7, sz: 0.2, c: -1, pitch: Math.PI / 2 },
];

function easeOutBack(t: number): number {
  const c1 = 1.70158;
  const c3 = c1 + 1;
  return 1 + c3 * (t - 1) ** 3 + c1 * (t - 1) ** 2;
}

function heroStatsOfDef(id: HeroId): number {
  return HEROES[id].base.auraRadius;
}

/** Abstand eines Punkts zu einer Strecke (Bildschirmkoordinaten). */
function segDist(px: number, py: number, ax: number, ay: number, bx: number, by: number): number {
  const dx = bx - ax;
  const dy = by - ay;
  const l2 = dx * dx + dy * dy;
  const t = l2 > 0 ? Math.max(0, Math.min(1, ((px - ax) * dx + (py - ay) * dy) / l2)) : 0;
  return Math.hypot(px - (ax + dx * t), py - (ay + dy * t));
}

const bossAlive = (sim: GameSimulation): boolean => sim.s.enemies.some((e) => e.alive && e.boss);

/** 3.13: Stimmung je Biom – Himmelsverlauf, Horizontglühen, Wolkenschatten, Sterne, Sonnendunst. */
export const MOOD: Record<BiomeId, { top: number; glow: number; clouds: number; stars: number; sun: number }> = {
  forest: { top: 0x3c5a5a, glow: 0xffd08a, clouds: 0.3, stars: 0, sun: 0.11 },
  volcano: { top: 0x5a2418, glow: 0xff7a30, clouds: 0.22, stars: 0.25, sun: 0.12 },
  frozen: { top: 0x4a6a8a, glow: 0xbfe8ff, clouds: 0.26, stars: 0.15, sun: 0.1 },
  crystal: { top: 0x5a3a6a, glow: 0xffb0e0, clouds: 0.22, stars: 0.35, sun: 0.11 },
  sky: { top: 0x6a8ab8, glow: 0xffd8b0, clouds: 0.32, stars: 0, sun: 0.13 },
  swamp: { top: 0x2e4436, glow: 0xc8e890, clouds: 0.3, stars: 0.1, sun: 0.07 },
  cavern: { top: 0x261e40, glow: 0x60f0d0, clouds: 0, stars: 0.7, sun: 0.05 },
};

function moodLighting(map: MapDefinition): Partial<Lighting> {
  const m = MOOD[map.biome];
  const pal = BIOMES[map.biome].palette;
  return {
    fogTop: hexToRgb(mixColor(mixColor(pal.fog, m.top, 0.7), m.glow, 0.12)),
    skyGlow: hexToRgb(m.glow).map((v) => v * 0.5) as [number, number, number],
    stars: m.stars,
    clouds: m.clouds,
    island: [map.width / 2, map.height / 2, map.width / 2 + 0.25, map.height / 2 + 0.25],
  };
}
