import { BUILD_ORDER, TOWERS } from './towers.ts';

// Frei belegbare Tasten. Jede Aktion hat eine Standardtaste; Spieler können sie in den Einstellungen
// ändern. Die Spielsitzung übersetzt gedrückte Tasten auf die Standardtaste der gebundenen Aktion,
// dadurch bleibt die gesamte bestehende Tastenlogik unverändert gültig.

export type KeyContext = 'all' | 'lane' | 'hero';

export interface KeyAction {
  id: string;
  name: string;
  group: string;
  /** Standardtaste (KeyboardEvent.key in Kleinbuchstaben, ' ' = Leertaste). */
  def: string;
  /** Wo die Aktion gilt: immer, nur mit Lane-Helden, nur im Helden-Endlosmodus. */
  ctx: KeyContext;
}

const A = (id: string, name: string, group: string, def: string, ctx: KeyContext = 'all'): KeyAction => ({ id, name, group, def, ctx });

export const KEY_ACTIONS: KeyAction[] = [
  A('startWave', 'Welle starten / Mini-Pause', 'Allgemein', ' '),
  A('pause', 'Pause (Menü)', 'Allgemein', 'p'),
  A('speed', 'Spieltempo', 'Allgemein', 'f'),
  A('rotL', 'Kamera links drehen', 'Allgemein', 'q'),
  A('rotR', 'Kamera rechts drehen', 'Allgemein', 'e'),
  A('orbitL', 'Kamera frei drehen −15°', 'Kamera', ','),
  A('orbitR', 'Kamera frei drehen +15°', 'Kamera', '.'),
  A('tiltUp', 'Kamera steiler', 'Kamera', 'pageup'),
  A('tiltDown', 'Kamera flacher', 'Kamera', 'pagedown'),
  A('resetView', 'Blickwinkel zurücksetzen', 'Kamera', 'home'),
  A('ranges', 'Alle Reichweiten', 'Allgemein', 'r'),
  A('grid', 'Raster', 'Allgemein', 'g'),
  A('help', 'Hilfe', 'Allgemein', 'h'),
  A('towerList', 'Turmübersicht', 'Allgemein', 'o'),
  A('upgrade', 'Turm aufwerten', 'Turm', 'u'),
  A('priority', 'Zielwahl wechseln', 'Turm', 't'),
  A('towerActive', 'Turmfähigkeit auslösen', 'Turm', 'c'),
  A('sell', 'Turm verkaufen', 'Turm', 'delete'),
  // Die Lane-Barrikade (3.8) teilt sich die Taste mit der Mazing-Barrikade – je nach Kartenvariante gibt es nur eine davon
  ...BUILD_ORDER.filter((id) => id !== 'lanebarricade').map((id) => A(`tower:${id}`, id === 'barricade' ? 'Barrikade wählen (Mazing) bzw. Lane-Barrikade' : `${TOWERS[id].name} wählen`, 'Türme bauen', TOWERS[id].hotkey)),
  A('hero1', 'Held 1 einsetzen/auswählen', 'Lane-Helden', 'z', 'lane'),
  A('hero2', 'Held 2 einsetzen/auswählen', 'Lane-Helden', 'x', 'lane'),
  A('heroA', 'Fähigkeit 1', 'Lane-Helden', 'a', 'lane'),
  A('heroS', 'Fähigkeit 2', 'Lane-Helden', 's', 'lane'),
  A('heroD', 'Ultimate', 'Lane-Helden', 'd', 'lane'),
  A('heroMode', 'Verhalten wechseln', 'Lane-Helden', 'v', 'lane'),
  A('skill1', 'Fähigkeit 1', 'Helden-Endlos', 'a', 'hero'),
  A('skill2', 'Fähigkeit 2', 'Helden-Endlos', 's', 'hero'),
  A('skill3', 'Fähigkeit 3', 'Helden-Endlos', 'd', 'hero'),
  A('skill4', 'Ultimate', 'Helden-Endlos', 'w', 'hero'),
  A('sheet', 'Charakterbogen', 'Helden-Endlos', 'tab', 'hero'),
  A('shop', 'Laden & Schmiede', 'Helden-Endlos', 'x', 'hero'),
  A('follow', 'Kamera folgt dem Helden', 'Helden-Endlos', 'y', 'hero'),
  A('topdown', 'Draufsicht', 'Helden-Endlos', 'v', 'hero'),
  A('hold', 'Held festsetzen', 'Helden-Endlos', 'z', 'hero'),
  A('nextWave', 'Nächste Welle rufen', 'Helden-Endlos', 'enter', 'hero'),
];

/** Tasten, die nicht belegt werden dürfen (Escape bricht ab / öffnet die Pause). */
export const RESERVED_KEYS = new Set(['escape']);

let overrides: Record<string, string> = {};

export function setKeyBindings(o: Record<string, string> | undefined): void {
  overrides = { ...(o ?? {}) };
}

export function boundKey(actionId: string): string {
  const a = KEY_ACTIONS.find((x) => x.id === actionId);
  return overrides[actionId] ?? a?.def ?? '';
}

const overlaps = (a: KeyContext, b: KeyContext): boolean => a === 'all' || b === 'all' || a === b;

/**
 * Übersetzt eine gedrückte Taste auf die Standardtaste der Aktion, an die sie gebunden ist.
 * null = Taste ist keiner Aktion (mehr) zugeordnet; '' = Taste wird unverändert weitergereicht.
 */
export function translateKey(k: string, ctx: Exclude<KeyContext, 'all'>): string | null {
  const relevant = KEY_ACTIONS.filter((a) => a.ctx === 'all' || a.ctx === ctx);
  const hit = relevant.find((a) => boundKey(a.id) === k);
  if (hit) return hit.def;
  // Eine Standardtaste, deren Aktion umgelegt wurde, löst nichts mehr aus
  if (relevant.some((a) => a.def === k && overrides[a.id] !== undefined)) return null;
  return '';
}

/**
 * Belegt eine Aktion neu. Ist die Taste in einem überlappenden Kontext schon vergeben, tauschen
 * beide Aktionen ihre Tasten. Rückgabe: neue Überschreibungen (nur Abweichungen vom Standard).
 */
export function rebind(actionId: string, key: string, current: Record<string, string>): { map: Record<string, string>; swapped: string | null } {
  const map = { ...current };
  const a = KEY_ACTIONS.find((x) => x.id === actionId);
  if (!a || RESERVED_KEYS.has(key)) return { map, swapped: null };
  const old = map[actionId] ?? a.def;
  const get = (x: KeyAction) => map[x.id] ?? x.def;
  const clash = KEY_ACTIONS.find((x) => x.id !== actionId && overlaps(x.ctx, a.ctx) && get(x) === key);
  const set = (x: KeyAction, k: string) => {
    if (k === x.def) delete map[x.id];
    else map[x.id] = k;
  };
  set(a, key);
  if (clash) set(clash, old);
  return { map, swapped: clash ? clash.id : null };
}

export function keyName(k: string): string {
  if (k === ' ') return 'Leertaste';
  if (k === 'enter') return 'Enter';
  if (k === 'tab') return 'Tab';
  if (k === 'delete') return 'Entf';
  if (k === 'backspace') return 'Rück';
  if (k === 'pageup') return 'Bild↑';
  if (k === 'pagedown') return 'Bild↓';
  if (k === 'home') return 'Pos1';
  if (k === ',') return ',';
  if (k === '-') return '−';
  if (k.startsWith('arrow')) return { arrowup: '↑', arrowdown: '↓', arrowleft: '←', arrowright: '→' }[k] ?? k;
  return k.length === 1 ? k.toUpperCase() : k.charAt(0).toUpperCase() + k.slice(1);
}

export function sanitizeKeys(raw: unknown): Record<string, string> {
  const out: Record<string, string> = {};
  if (typeof raw !== 'object' || raw === null || Array.isArray(raw)) return out;
  for (const [id, v] of Object.entries(raw as Record<string, unknown>)) {
    if (typeof v !== 'string' || v.length === 0 || v.length > 12 || RESERVED_KEYS.has(v)) continue;
    if (!KEY_ACTIONS.some((a) => a.id === id)) continue;
    out[id] = v.toLowerCase();
  }
  return out;
}
