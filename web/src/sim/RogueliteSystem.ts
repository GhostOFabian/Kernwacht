import { MODIFIER_IDS, MODIFIERS } from '../config/modifiers.ts';
import { Rng } from '../core/rng.ts';
import type { GameState, ModifierId } from '../core/types.ts';
import type { GameSimulation } from './GameSimulation.ts';

// Roguelite-Auswahl: drei Modifikatoren aus eigenem Zufallsstrom (state.rng.mods).
// Regeln: keine Duplikate, keine unvereinbaren Kombinationen (exclusive), und mindestens ein
// Angebot ist sofort nutzbar (generisch oder passend zu gebauten Türmen).

export const SKIP_GOLD = 30;

function usable(s: GameState, id: ModifierId): boolean {
  const req = MODIFIERS[id].requiresTower;
  if (!req) return true;
  return s.towers.some((t) => req.includes(t.def));
}

export function eligibleModifiers(s: GameState): ModifierId[] {
  return MODIFIER_IDS.filter((id) => {
    if (s.modifiers.includes(id)) return false;
    if (MODIFIERS[id].requiresHeroes && s.heroes.length === 0) return false;
    const ex = MODIFIERS[id].exclusive ?? [];
    if (ex.some((x) => s.modifiers.includes(x))) return false;
    if (s.modifiers.some((m) => (MODIFIERS[m].exclusive ?? []).includes(id))) return false;
    return true;
  });
}

export function rollOffer(sim: GameSimulation): ModifierId[] {
  const s = sim.s;
  const rng = new Rng(s.rng.mods);
  const pool = eligibleModifiers(s);
  const offer: ModifierId[] = [];
  const avail = [...pool];
  while (offer.length < 3 && avail.length > 0) {
    const pick = rng.weighted(avail, (id) => (MODIFIERS[id].requiresTower ? (usable(s, id) ? 3 : 0.5) : 2));
    // Gegenseitig ausschließende Angebote vermeiden
    avail.splice(avail.indexOf(pick), 1);
    const ex = MODIFIERS[pick].exclusive ?? [];
    if (offer.some((o) => ex.includes(o) || (MODIFIERS[o].exclusive ?? []).includes(pick))) continue;
    offer.push(pick);
  }
  if (offer.length > 0 && !offer.some((id) => usable(s, id))) {
    const generic = avail.find((id) => !MODIFIERS[id].requiresTower);
    if (generic) offer[offer.length - 1] = generic;
  }
  s.rng.mods = rng.state;
  return offer;
}

export function applyModifierChoice(sim: GameSimulation, id: ModifierId | null): void {
  const s = sim.s;
  if (id === null) {
    s.gold += SKIP_GOLD;
    s.stats.goldEarned += SKIP_GOLD;
    sim.emit({ k: 'message', text: `Kein Modifikator gewählt: +${SKIP_GOLD} Gold`, severity: 'info' });
  } else {
    s.modifiers.push(id);
    if (id === 'glassCannon') s.lives = Math.max(1, s.lives - 5);
    if (id === 'bulwark') {
      s.lives += 8;
      s.maxLives += 8;
    }
    sim.emit({ k: 'message', text: `Modifikator aktiv: ${MODIFIERS[id].name}`, severity: 'info' });
  }
  s.modOffer = null;
  s.phase = 'build';
  sim.c.statsDirty = true;
  sim.c.networkDirty = true;
}
