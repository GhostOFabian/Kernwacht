// Abgleich TS ↔ C#: spielt eine Helden-Endlos-Partie deterministisch mit dem Bot und einem einfachen
// Helden-Automaten und gibt je Welle einen Kurzbericht aus. Gleiches Programm in Unity: Kernwacht → Test · Simulation abgleichen.
//   node --import tsx scripts/simDigest.ts [champion=lyra] [maxWave=12]
import { CHAMPIONS } from '../src/config/champions.ts';
import { Bot, BOT_PROFILES } from '../src/sim/Bot.ts';
import { championStats } from '../src/sim/ChampionSystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';

const champion = (process.argv[2] ?? 'lyra') as never;
const maxWave = Number(process.argv[3] ?? 12);
const r2 = (x: number) => Math.round(x * 100) / 100;

const sim = GameSimulation.create({
  mode: 'heroEndless', seedText: 'KERNWACHT', variant: 'classic', biome: 'forest', difficulty: 'normal', size: 'normal',
  heroEndless: { champion, startKit: [], meta: { bestWave: 0, unlockedItems: [], fragments: {} }, runId: 'digest' },
} as never);
const bot = new Bot(sim, BOT_PROFILES[0]);

function brain(): void {
  const he = sim.s.he!;
  const c = he.champion;
  if (c.dead > 0) return;
  const st = championStats(he);
  if (c.hp < st.maxHp * 0.4) sim.usePotion('heal');
  const def = CHAMPIONS[c.id];
  for (const n of def.tree) if (c.skillPoints > 0) sim.learnSkill(n.id);
  if (def.skills[0].target === 'self') sim.championSkill(0, c.x, c.y);
  if (c.path.length === 0) {
    let best = null as null | { x: number; y: number };
    let bd = 9;
    for (const p of he.pickups) {
      const d = Math.hypot(p.x - c.x, p.y - c.y);
      if (d < bd) { bd = d; best = p; }
    }
    if (best) sim.moveChampion(Math.floor(best.x), Math.floor(best.y));
  }
  for (const i of c.inventory) if (!c.equipment.includes(i.uid)) sim.equip(i.uid);
}

let lastWave = 0;
const out: string[] = [];
const m = sim.s.map;
out.push(`map ${m.width}x${m.height} spawn ${m.spawn.x},${m.spawn.y} goal ${m.goal.x},${m.goal.y} shop ${sim.s.he!.shop.x},${sim.s.he!.shop.y} gold ${sim.s.gold}`);
let ticks = 0;
while (!sim.isOver() && ticks < 30 * 60 * 40) {
  if (sim.s.phase === 'choice') sim.chooseModifier(sim.s.modOffer?.[0] ?? null);
  if (sim.s.phase === 'build') { bot.tactics(); bot.spend(); }
  sim.step();
  ticks++;
  if (sim.s.phase === 'combat' && sim.s.tick % 90 === 0) bot.spend();
  if (sim.s.phase === 'combat' && sim.s.tick % 15 === 0) bot.tactics();
  if (sim.s.tick % 10 === 0) brain();
  if (sim.lastError) { out.push('FEHLER ' + sim.lastError.split('\n')[0]); break; }
  if (sim.s.stats.wavesCleared !== lastWave) {
    lastWave = sim.s.stats.wavesCleared;
    const he = sim.s.he!;
    const c = he.champion;
    const res = Object.entries(he.resources).map(([k, v]) => `${k}=${v}`).join(',');
    out.push(`W${lastWave} tick=${sim.s.tick} gold=${sim.s.gold} lives=${sim.s.lives} kills=${sim.s.stats.kills} towers=${sim.s.towers.length} lvl=${c.level} xp=${r2(c.xp)} hp=${r2(c.hp)} pos=${r2(c.x)},${r2(c.y)} pickups=${he.pickups.length} inv=${c.inventory.length} res=${res}`);
    if (lastWave >= maxWave) break;
  }
}
console.log(out.join('\n'));
