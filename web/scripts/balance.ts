// Balance-Auswertung: lässt die Bot-Profile auf vielen Seeds spielen und erfasst
// Erfolgsquoten, Gold pro Welle, Schaden pro Gold, Bossüberleben, Turmnutzung und Minen-Amortisation.
//   npm run balance            (Standard: 6 Seeds je Kombination)
//   npm run balance -- 12      (12 Seeds)
import { TOWERS } from '../src/config/towers.ts';
import type { Difficulty, HeroId, MapSize, MapVariant, TowerId } from '../src/core/types.ts';
import { Bot, BOT_PROFILES } from '../src/sim/Bot.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';

const seeds = Number(process.argv[2] ?? 6);
const diffs: Difficulty[] = (process.argv[3]?.split(',') as Difficulty[]) ?? ['easy', 'normal', 'hard'];
const variants: MapVariant[] = (process.argv[4]?.split(',') as MapVariant[]) ?? ['classic', 'mazing'];
const sizes: MapSize[] = (process.argv[5]?.split(',') as MapSize[]) ?? ['normal'];
const heroes: HeroId[] = process.argv[6] === 'none' ? [] : ((process.argv[6]?.split(',') as HeroId[]) ?? ['blade', 'bard']);

interface Agg { runs: number; wins: number; waves: number; livesLost: number; bossFaced: number; bossKilled: number }
const byKey = new Map<string, Agg>();
const usage = new Map<TowerId, number>();
const dmgPerGold = new Map<TowerId, { dmg: number; gold: number }>();
const goldPerWave: number[][] = [];
let mineGold = 0;
let mineCost = 0;
let mineWaves = 0;
const t0 = Date.now();

for (const size of sizes) for (const variant of variants) {
  for (const diff of diffs) {
    for (const p of BOT_PROFILES) {
      const key = `${size.padEnd(6)} ${variant.padEnd(7)} ${diff.padEnd(6)} ${p.name}`;
      const a: Agg = byKey.get(key) ?? { runs: 0, wins: 0, waves: 0, livesLost: 0, bossFaced: 0, bossKilled: 0 };
      for (let i = 0; i < seeds; i++) {
        const sim = GameSimulation.create({ mode: 'quick', seedText: `BAL-${i}`, variant, biome: 'random', difficulty: diff, size, heroes });
        new Bot(sim, p).play();
        const s = sim.s;
        if (sim.lastError) console.error('FEHLER', key, sim.lastError);
        a.runs++;
        if (s.phase === 'victory') a.wins++;
        a.waves += s.stats.wavesCleared;
        a.livesLost += s.stats.livesLost;
        a.bossFaced += s.stats.bossesFaced.length;
        a.bossKilled += s.stats.bossesKilled.length;
        s.stats.goldPerWave.forEach((g, w) => { (goldPerWave[w] ??= []).push(g); });
        for (const t of s.towers) {
          if (t.def === 'barricade') continue;
          usage.set(t.def, (usage.get(t.def) ?? 0) + 1);
          const d = dmgPerGold.get(t.def) ?? { dmg: 0, gold: 0 };
          d.dmg += t.damageDealt;
          d.gold += t.invested;
          dmgPerGold.set(t.def, d);
          if (t.def === 'goldmine') {
            mineGold += t.goldEarned;
            mineCost += t.invested;
            mineWaves += Math.max(0, s.wave - t.builtWave - (s.phase === 'combat' ? 1 : 0));
          }
        }
      }
      byKey.set(key, a);
    }
  }
}

console.log(`\nBalance-Auswertung (${seeds} Seeds je Kombination, ${((Date.now() - t0) / 1000).toFixed(1)} s)\n`);
console.log('Variante Schwier. Profil            Sieg%  Ø Welle  Ø ♥-Verlust  Boss besiegt');
for (const [k, a] of byKey) {
  console.log(`${k.padEnd(34)} ${((100 * a.wins) / a.runs).toFixed(0).padStart(4)}%  ${(a.waves / a.runs).toFixed(1).padStart(6)}  ${(a.livesLost / a.runs).toFixed(1).padStart(9)}  ${a.bossKilled}/${a.bossFaced}`);
}
const agg = (pred: (k: string) => boolean): string => {
  let r = 0, w = 0;
  for (const [k, a] of byKey) if (pred(k)) { r += a.runs; w += a.wins; }
  return r ? `${((100 * w) / r).toFixed(0)} %` : '–';
};
console.log('\nErfolgsquote nach Schwierigkeit:', diffs.map((d) => `${d}: ${agg((k) => k.includes(` ${d}`))}`).join(' · '));
console.log('Erfolgsquote nach Variante:', variants.map((v) => `${v}: ${agg((k) => k.startsWith(v))}`).join(' · '));
console.log('\nGold pro Welle (Median):', goldPerWave.slice(0, 20).map((xs) => xs.sort((a, b) => a - b)[Math.floor(xs.length / 2)]).join(' '));
console.log('\nSchaden pro investiertem Gold & Nutzung:');
for (const [id, d] of [...dmgPerGold].sort((a, b) => b[1].dmg / b[1].gold - a[1].dmg / a[1].gold)) {
  console.log(`  ${TOWERS[id].name.padEnd(22)} ${(d.dmg / Math.max(1, d.gold)).toFixed(1).padStart(7)}  (gebaut ${usage.get(id)})`);
}
if (mineCost > 0) console.log(`\nGoldgräber: Ertrag/Investition ${(mineGold / mineCost).toFixed(2)}, Ø Ertrag je Minenwelle ${(mineGold / Math.max(1, mineWaves)).toFixed(1)} Gold`);
