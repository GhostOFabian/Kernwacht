import type { GameState, PlayerProgress } from '../core/types.ts';

// Erfolge und kosmetische Turm-Skins. Erfolge geben keine spielerischen Vorteile –
// sie schalten ausschließlich Optik frei (keine verpflichtende Werteeskalation).

export interface AchievementDefinition {
  id: string;
  name: string;
  description: string;
  icon: string;
  /** Prüft am Ende einer Partie (Zustand + bereits aktualisierter Fortschritt). */
  check: (s: GameState, p: PlayerProgress) => boolean;
}

const won = (s: GameState): boolean => s.phase === 'victory';
/** 3.9: Nur noch Helden-Endlos – Erfolge über erreichte Wellen (IDs bleiben für bestehende Spielstände gleich). */
const heWave = (s: GameState, n: number): boolean => s.mode === 'heroEndless' && s.stats.wavesCleared >= n;

export const ACHIEVEMENTS: AchievementDefinition[] = [
  { id: 'firstWin', name: 'Erste Wacht', description: 'Erreiche Welle 25 im Helden-Endlos.', icon: '🏆', check: (s) => won(s) || heWave(s, 25) },
  { id: 'flawless', name: 'Makellos', description: 'Erreiche Welle 20, ohne Kernenergie zu verlieren.', icon: '💎', check: (s) => (won(s) || heWave(s, 20)) && s.stats.livesLost === 0 },
  { id: 'hardWin', name: 'Eiserner Wille', description: 'Erreiche Welle 100 im Helden-Endlos.', icon: '⚔', check: (s) => (won(s) && s.difficulty === 'hard') || heWave(s, 100) },
  { id: 'mazer', name: 'Labyrinthmeister', description: 'Erreiche Welle 30 auf einer Mazing-Karte.', icon: '🧱', check: (s) => (won(s) || heWave(s, 30)) && s.map.variant === 'mazing' },
  { id: 'hugeWin', name: 'Weite Welten', description: 'Erreiche Welle 50 auf einer riesigen Karte.', icon: '🗺', check: (s) => (won(s) || heWave(s, 50)) && s.map.size === 'huge' },
  { id: 'bossSlayer', name: 'Bossjäger', description: 'Besiege insgesamt 10 Bosse.', icon: '👑', check: (_s, p) => p.totalBossKills >= 10 },
  { id: 'allBosses', name: 'Bestiarium', description: 'Besiege jeden der sieben Bossarten mindestens einmal (über alle Partien).', icon: '📖', check: (_s, p) => p.bossesDefeated.length >= 7 },
  { id: 'reactions', name: 'Alchemist', description: 'Löse in einer Partie 100 Elementarreaktionen aus.', icon: '⚗', check: (s) => Object.values(s.stats.reactions).reduce((a, b) => a + b, 0) >= 100 },
  { id: 'network', name: 'Netzarchitekt', description: 'Baue ein Netzwerk aus 8 Türmen.', icon: '📡', check: (s) => s.stats.maxNetworkSize >= 8 },
  { id: 'fusion', name: 'Schmelztiegel', description: 'Erschaffe einen legendären Turm.', icon: '✦', check: (s) => s.stats.fusions >= 1 },
  { id: 'heroic', name: 'Heldenhaft', description: 'Helden erzielen in einer Partie 100 Abschüsse.', icon: '🛡', check: (s) => s.stats.heroKills >= 100 },
  { id: 'endless30', name: 'Unermüdlich', description: 'Erreiche Welle 50 im Helden-Endlos.', icon: '∞', check: (s) => (s.mode === 'endless' && s.stats.wavesCleared >= 30) || heWave(s, 50) },
  { id: 'campaign', name: 'Hüter des Kerns', description: 'Erreiche Welle 200 im Helden-Endlos.', icon: '📜', check: (s, p) => p.campaignCompleted.includes('m10') || heWave(s, 200) },
  { id: 'kills5000', name: 'Rissbrecher', description: 'Besiege insgesamt 5000 Gegner.', icon: '☠', check: (_s, p) => p.totalKills >= 5000 },
  { id: 'rich', name: 'Schatzkammer', description: 'Verdiene in einer Partie 5000 Gold.', icon: '💰', check: (s) => s.stats.goldEarned >= 5000 },
  { id: 'mutants', name: 'Mutantenjäger', description: 'Besiege 25 mutierte Elitegegner in einer Partie.', icon: '🧬', check: (s) => s.stats.mutationsKilled >= 25 },
];

export interface SkinDefinition {
  id: string;
  name: string;
  description: string;
  /** Tönung der Turmfarben und Sockelfarbe. */
  tint: number;
  tintAmount: number;
  stone: number;
  requires: string | null;   // Erfolgs-ID
}

export const SKINS: SkinDefinition[] = [
  { id: 'standard', name: 'Standard', description: 'Die klassische Optik.', tint: 0xffffff, tintAmount: 0, stone: 0x8c8678, requires: null },
  { id: 'obsidian', name: 'Obsidian & Gold', description: 'Schwarzer Stein mit goldenen Akzenten.', tint: 0xffd060, tintAmount: 0.35, stone: 0x2a2630, requires: 'firstWin' },
  { id: 'frost', name: 'Ewiges Eis', description: 'Kühles Blau und Eisweiß.', tint: 0x9ae0ff, tintAmount: 0.45, stone: 0xc8dce8, requires: 'mazer' },
  { id: 'void', name: 'Leere', description: 'Violetter Riss-Stein.', tint: 0xc070ff, tintAmount: 0.45, stone: 0x3a2a50, requires: 'fusion' },
  { id: 'royal', name: 'Königlich', description: 'Weißer Marmor und Purpur.', tint: 0xff70b0, tintAmount: 0.3, stone: 0xe8e4e0, requires: 'hardWin' },
  { id: 'molten', name: 'Glutherz', description: 'Glühender Basalt.', tint: 0xff6a20, tintAmount: 0.4, stone: 0x3a2420, requires: 'bossSlayer' },
];

export function skinUnlocked(skin: SkinDefinition, achievements: readonly string[]): boolean {
  return skin.requires === null || achievements.includes(skin.requires);
}
