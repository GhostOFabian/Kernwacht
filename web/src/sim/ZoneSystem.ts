import type { Zone } from '../core/types.ts';
import type { GameSimulation } from './GameSimulation.ts';
import { applyHit, dealDamage } from './DamageSystem.ts';
import { addStatus } from './ElementSystem.ts';
import { towerPayload, updateMine } from './TowerSystem.ts';
import { zoneHitsHeroes } from './HeroSystem.ts';

// Bodenzonen: Giftwolken, Bossportale, angekündigte Feuerzonen, Nullpulse, Rissprung-Ziele.

export function updateZones(sim: GameSimulation, dt: number): void {
  const s = sim.s;
  for (const z of s.zones) {
    if (z.armed > 0) {
      z.armed -= dt;
      if (z.armed <= 0) {
        z.armed = 0;
        onArmed(sim, z);
      }
      continue;
    }
    z.t -= dt;
    if (z.t <= 0) continue;
    if (z.kind === 'poisonCloud') {
      z.tick -= dt;
      if (z.tick <= 0) {
        z.tick = 0.5;
        const t = sim.c.towerByUid.get(z.src);
        const maxStacks = t ? Math.round(sim.stats(t).poisonMaxStacks) : 5;
        const dur = t ? sim.stats(t).poisonDur : 4;
        for (const e of s.enemies) {
          if (!e.alive || e.flying) continue;
          if ((e.x - z.x) ** 2 + (e.y - z.y) ** 2 <= z.r * z.r) addStatus(e, 'poison', dur, z.power, z.src, maxStacks);
        }
      }
    } else if (z.kind === 'mine') {
      updateMine(sim, z);
    } else if (z.kind === 'burnTrap') {
      z.tick -= dt;
      if (z.tick <= 0) {
        z.tick = 0.5;
        for (const e of s.enemies) {
          if (!e.alive || e.flying) continue;
          if ((e.x - z.x) ** 2 + (e.y - z.y) ** 2 <= z.r * z.r) addStatus(e, 'burn', 2.5, z.power, z.src);
        }
      }
    } else if (z.kind === 'portalIn') {
      const out = s.zones.find((o) => o.uid === z.link && o.t > 0);
      if (!out) continue;
      for (const e of s.enemies) {
        if (!e.alive || e.boss || e.flying || e.riftCd > s.time) continue;
        if ((e.x - z.x) ** 2 + (e.y - z.y) ** 2 > z.r * z.r) continue;
        const ox = e.x;
        const oy = e.y;
        e.x = out.x;
        e.y = out.y;
        e.nx = Math.floor(out.x);
        e.ny = Math.floor(out.y);
        e.riftCd = s.time + 6;
        sim.emit({ k: 'teleport', x: ox, y: oy, x2: e.x, y2: e.y });
      }
    }
  }
}

function onArmed(sim: GameSimulation, z: Zone): void {
  const s = sim.s;
  switch (z.kind) {
    case 'riftJump': {
      const boss = sim.c.enemyByUid.get(z.src);
      if (!boss || !boss.alive) return;
      const ox = boss.x;
      const oy = boss.y;
      boss.x = z.x;
      boss.y = z.y;
      boss.nx = Math.floor(z.x);
      boss.ny = Math.floor(z.y);
      boss.traveled += 5;
      sim.emit({ k: 'teleport', x: ox, y: oy, x2: z.x, y2: z.y });
      break;
    }
    case 'fireZone': {
      const damp = s.towers.some((t) => t.def === 'frost' && (t.cx + 0.5 - z.x) ** 2 + (t.cy + 0.5 - z.y) ** 2 <= (z.r + 0.6) ** 2);
      const dur = damp ? z.power * 0.5 : z.power;
      let hit = 0;
      for (const t of s.towers) {
        if ((t.cx + 0.5 - z.x) ** 2 + (t.cy + 0.5 - z.y) ** 2 <= z.r * z.r) {
          t.stunned = Math.max(t.stunned, dur);
          hit++;
        }
      }
      z.t = dur;
      zoneHitsHeroes(sim, z.x, z.y, z.r, 120, false);
      if (damp) sim.emit({ k: 'message', text: 'Frostwächter dämpfen die Feuerzone!', severity: 'info' });
      else if (hit > 0) sim.emit({ k: 'message', text: `${hit} Türme verbrannt (${dur} s inaktiv)`, severity: 'warn' });
      break;
    }
    case 'nullPulse': {
      zoneHitsHeroes(sim, z.x, z.y, z.r, 0, true);
      let hit = 0;
      for (const t of s.towers) {
        if ((t.cx + 0.5 - z.x) ** 2 + (t.cy + 0.5 - z.y) ** 2 <= (z.r + 0.5) ** 2) {
          t.disrupted = Math.max(t.disrupted, z.power);
          hit++;
        }
      }
      if (hit > 0) {
        sim.c.networkDirty = true;
        sim.emit({ k: 'message', text: `Nullpuls: ${hit} Türme vom Netzwerk getrennt`, severity: 'warn' });
      }
      break;
    }
    case 'portalIn':
      sim.emit({ k: 'message', text: 'Risskorridor geöffnet!', severity: 'warn' });
      break;
    case 'meteor': {
      const t = sim.c.towerByUid.get(z.link);
      const st = t ? sim.c.towerStats.get(t.uid) : undefined;
      const big = z.r >= 1.9;
      if (t && st) {
        const p = towerPayload(t, st);
        p.damage = z.power;
        p.splash = z.r;
        const r2 = z.r * z.r;
        for (const e of s.enemies) {
          if (!e.alive || (e.flying && st.hitsAir <= 0)) continue;
          const d2 = (e.x - z.x) ** 2 + (e.y - z.y) ** 2;
          if (d2 <= r2) applyHit(sim, e, p, d2 < r2 * 0.25 ? 1 : 0.7);
        }
      } else {
        for (const e of s.enemies) {
          if (e.alive && !e.flying && (e.x - z.x) ** 2 + (e.y - z.y) ** 2 <= z.r * z.r) dealDamage(sim, e, z.power, 'fire', z.src);
        }
      }
      sim.emit({ k: 'meteor', x: z.x, y: z.y, r: z.r, big });
      sim.emit({ k: 'hit', type: 'fire', x: z.x, y: z.y, amount: z.power, splash: z.r });
      break;
    }
    case 'stomp': {
      let hit = 0;
      for (const t of s.towers) {
        if ((t.cx + 0.5 - z.x) ** 2 + (t.cy + 0.5 - z.y) ** 2 <= z.r * z.r) {
          t.stunned = Math.max(t.stunned, z.power);
          hit++;
        }
      }
      sim.emit({ k: 'burst', x: z.x, y: z.y, r: z.r });
      zoneHitsHeroes(sim, z.x, z.y, z.r, 150, false);
      if (hit > 0) sim.emit({ k: 'message', text: `Weltenstampfer: ${hit} Türme betäubt`, severity: 'warn' });
      break;
    }
    case 'leyNode':
      sim.c.statsDirty = true;
      break;
    default:
      break;
  }
}
