// Balancing-Lauf für den Helden-Endlosmodus: Bot baut Türme, ein einfacher Helden-Automat
// sammelt Beute, wirkt Fähigkeiten und nutzt Tränke. Vergleich mit dem normalen Endlosmodus.
//   node --import tsx scripts/heroBalance.ts [seeds=3] [maxWave=60]
import { CHAMPIONS } from '../src/config/champions.ts';
import type { ChampionId } from '../src/core/types.ts';
import { Bot, BOT_PROFILES } from '../src/sim/Bot.ts';
import { championStats } from '../src/sim/ChampionSystem.ts';
import { GameSimulation } from '../src/sim/GameSimulation.ts';

const seeds = Number(process.argv[2] ?? 3);
const maxWave = Number(process.argv[3] ?? 60);

function heroBrain(sim: GameSimulation): void {
  const he = sim.s.he;
  if (!he) return;
  const c = he.champion;
  if (c.dead > 0) return;
  const st = championStats(he);
  if (c.hp < st.maxHp * 0.4) sim.usePotion('heal');
  if (c.mana < st.maxMana * 0.2) sim.usePotion('mana');
  const def = CHAMPIONS[c.id];
  const enemies = sim.s.enemies.filter((e) => e.alive);
  def.skills.forEach((sk, slot) => {
    if (sk.target === 'self') {
      if (enemies.some((e) => Math.hypot(e.x - c.x, e.y - c.y) < 4) || def.id === 'lyra') sim.championSkill(slot, c.x, c.y);
    } else if (sk.target === 'tower') {
      const t = sim.s.towers.filter((x) => Math.hypot(x.cx - c.x, x.cy - c.y) < sk.castRange).sort((a, b) => b.level - a.level)[0];
      if (t && enemies.length) sim.championSkill(slot, t.cx + 0.5, t.cy + 0.5);
    } else {
      const e = enemies.filter((x) => Math.hypot(x.x - c.x, x.y - c.y) < sk.castRange).sort((a, b) => b.hp - a.hp)[0];
      if (e) {
        if (def.id === 'lyra') {
          const t = sim.s.towers.filter((x) => Math.hypot(x.cx - c.x, x.cy - c.y) < sk.castRange)[0];
          if (t) sim.championSkill(slot, t.cx + 0.5, t.cy + 0.5);
        } else sim.championSkill(slot, e.x, e.y);
      }
    }
  });
  for (const n of def.tree) if (c.skillPoints > 0) sim.learnSkill(n.id);
  // Beute einsammeln, sonst bei den Türmen bzw. am Gegnerstrom bleiben
  if (c.path.length === 0) {
    const p = he.pickups.filter((x) => Math.hypot(x.x - c.x, x.y - c.y) < 9).sort((a, b) => Math.hypot(a.x - c.x, a.y - c.y) - Math.hypot(b.x - c.x, b.y - c.y))[0];
    if (p) sim.moveChampion(Math.floor(p.x), Math.floor(p.y));
    else if (def.melee || def.id === 'vela') {
      const e = enemies.sort((a, b) => a.remaining - b.remaining)[0];
      if (e && Math.hypot(e.x - c.x, e.y - c.y) > 2.5) sim.moveChampion(Math.floor(e.x), Math.floor(e.y));
    } else if (sim.s.towers.length) {
      const t = sim.s.towers[Math.floor(sim.s.tick / 900) % sim.s.towers.length];
      if (Math.hypot(t.cx - c.x, t.cy - c.y) > 2) sim.moveChampion(t.cx, t.cy);
    }
  }
  // Ausrüstung automatisch anlegen
  for (const i of c.inventory) if (!c.equipment.includes(i.uid)) sim.equip(i.uid);
}

function run(mode: 'endless' | 'heroEndless', seed: string, champion: ChampionId): { wave: number; level: number; items: number; err: string | null } {
  const sim = GameSimulation.create({
    mode, seedText: seed, variant: 'classic', biome: 'random', difficulty: 'normal', size: 'large',
    heroEndless: mode === 'heroEndless' ? { champion, startKit: [], meta: { bestWave: 15, unlockedItems: [], fragments: {} }, runId: seed } : undefined,
  });
  const bot = new Bot(sim, BOT_PROFILES[0]);
  let ticks = 0;
  while (!sim.isOver() && ticks < 30 * 60 * 120 && sim.s.wave <= maxWave) {
    if (sim.s.phase === 'choice') sim.chooseModifier(sim.s.modOffer?.[0] ?? null);
    if (sim.s.phase === 'build') {
      bot.tactics();
      bot.spend();
      if (mode === 'endless') sim.startWave();
    }
    sim.step();
    ticks++;
    if (sim.s.phase === 'combat' && sim.s.tick % 90 === 0) bot.spend();
    if (sim.s.phase === 'combat' && sim.s.tick % 15 === 0) bot.tactics();
    if (mode === 'heroEndless' && sim.s.tick % 10 === 0) heroBrain(sim);
    if (sim.lastError) break;
  }
  return { wave: sim.s.stats.wavesCleared, level: sim.s.he?.champion.level ?? 0, items: sim.s.he?.champion.inventory.length ?? 0, err: sim.lastError };
}

for (let i = 0; i < seeds; i++) {
  const seed = `HB-${i + Number(process.env.OFF ?? 0)}`;
  const base = run('endless', seed, 'lyra');
  const line = [`${seed}: Endlos W${base.wave}`];
  for (const ch of ['lyra', 'brann', 'vela'] as const) {
    const r = run('heroEndless', seed, ch);
    line.push(`${ch} W${r.wave} St${r.level} Items${r.items}${r.err ? ` FEHLER ${r.err.split('\n')[0]}` : ''}`);
  }
  console.log(line.join(' | '));
}
