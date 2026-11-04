import type { GameApplication } from '../../app/GameApplication.ts';
import type { RunRecord } from '../../app/progress.ts';
import { CAMPAIGN, CAMPAIGN_EPILOGUE } from '../../config/campaign.ts';
import { REACTION_BY_ID } from '../../config/elements.ts';
import { ACHIEVEMENTS } from '../../config/achievements.ts';
import { BOSSES } from '../../config/enemies.ts';
import { HEROES } from '../../config/heroes.ts';
import { MODIFIERS } from '../../config/modifiers.ts';
import { TOWERS } from '../../config/towers.ts';
import type { GameState, TowerId } from '../../core/types.ts';
import { scoreBreakdown } from '../../sim/Score.ts';
import { iconImg } from '../../render/IconFactory.ts';
import { fmt, h } from '../dom.ts';
import { MODE_NAMES } from '../text.ts';
import { svgIcon } from '../icons.ts';
import { copyText, shell } from './common.ts';
import { CHAMPIONS } from '../../config/champions.ts';
import { ITEMS } from '../../config/items.ts';
import { MILESTONES, PERKS } from '../../config/heroEndless.ts';

export interface ResultParams { state: GameState; record: RunRecord }

function duration(sec: number): string {
  const m = Math.floor(sec / 60);
  const s = Math.floor(sec % 60);
  return `${m}:${String(s).padStart(2, '0')} min`;
}

export function renderResults(app: GameApplication, p: ResultParams): HTMLElement {
  const s = p.state;
  const r = p.record;
  const st = s.stats;
  const win = s.phase === 'victory';
  const sb = scoreBreakdown(s);
  const dmg = (Object.entries(st.damageByTower) as [TowerId, number][]).sort((a, b) => b[1] - a[1]);
  const maxDmg = Math.max(1, ...dmg.map(([, v]) => v));
  const maxGold = Math.max(1, ...st.goldPerWave);
  const mission = s.missionId ? CAMPAIGN.find((m) => m.id === s.missionId) : undefined;
  const next = r.unlockedMission ? CAMPAIGN.find((m) => m.id === r.unlockedMission) : undefined;
  const he = s.he;
  const hr = r.hero;
  const again = (): void => app.startRun({
    heroEndless: he ? app.heroEndlessSetup(he.champion.id, app.save.progress.heroMeta.startKit) : undefined,
    mode: s.mode, seedText: s.seed.text, variant: s.map.variant, biome: s.map.biome, difficulty: s.difficulty,
    missionId: s.missionId, customMap: s.mode === 'custom' ? s.map : null, size: s.map.size ?? 'normal', heroes: s.heroes.map((x) => x.id),
  });
  const el = shell(app, win ? 'Sieg!' : he ? 'Lauf beendet' : 'Niederlage', null,
    h('div', { class: `result-banner ${win ? 'win' : 'lose'}`, 'data-testid': 'result' },
      h('div', { class: 'big' }, svgIcon(win ? 'trophy' : 'skull', 'svg-ic res-ic')),
      h('div', {},
        h('h2', {}, win ? (mission ? `Mission „${mission.name}“ geschafft` : 'Der Kern hält stand!') : 'Der Kern ist gefallen'),
        h('p', {}, `${MODE_NAMES[s.mode]} · ${s.map.name} · ${st.wavesCleared} Wellen überstanden`),
        r.stars ? h('p', { class: 'stars big-stars', 'aria-label': `${r.stars} Sterne` }, '★'.repeat(r.stars) + '☆'.repeat(3 - r.stars)) : null)),
    r.campaignFinished ? h('div', { class: 'notice good' }, CAMPAIGN_EPILOGUE) : null,
    next ? h('div', { class: 'notice good' }, `Neue Mission freigeschaltet: ${next.name}`) : null,
    r.newBestScore ? h('div', { class: 'notice good' }, 'Neuer persönlicher Bestwert für diesen Modus!') : null,
    r.newBestEndless ? h('div', { class: 'notice good' }, 'Neue beste Endloswelle!') : null,
    ...(r.unlockedHeroes ?? []).map((id) => h('div', { class: 'notice good' }, `Neuer Held freigeschaltet: ${HEROES[id].name} – ${HEROES[id].title}`)),
    (r.newAchievements ?? []).length ? h('div', { class: 'notice good', 'data-testid': 'new-achievements' }, h('b', {}, 'Erfolge errungen: '),
      (r.newAchievements ?? []).map((id) => { const a = ACHIEVEMENTS.find((x) => x.id === id); return a ? `${a.icon} ${a.name}` : id; }).join(' · ')) : null,
    he ? h('div', { class: 'card he-result', 'data-testid': 'hero-result' },
      h('h3', {}, `${CHAMPIONS[he.champion.id].name} · Stufe ${he.champion.level}`),
      h('p', {}, `Welle ${st.wavesCleared} erreicht${hr?.newBestWave ? ' – neue Bestwelle!' : ''} · ${he.champion.kills} Abschüsse durch den Helden · ${fmt(he.champion.damageDealt)} Schaden`),
      h('p', { class: 'small' }, `EP gesammelt ${fmt(he.stats.xpCollected)} · Items gefunden ${he.stats.itemsFound} · hergestellt ${he.stats.itemsCrafted} · Tränke genutzt ${he.stats.potionsUsed} · Ressourcen ${fmt(he.stats.resourcesEarned)}`),
      hr?.newItems.length ? h('div', { class: 'notice good' }, h('b', {}, 'Dauerhaft freigeschaltet: '), hr.newItems.map((i) => `${ITEMS[i]?.icon ?? ''} ${ITEMS[i]?.name ?? i}`).join(' · ')) : null,
      hr?.newPerks.length ? h('div', { class: 'notice good' }, h('b', {}, 'Neue Perks: '), hr.newPerks.map((p) => PERKS.find((x) => x.id === p)?.name ?? p).join(' · ')) : null,
      hr?.milestones.length ? h('div', { class: 'notice good' }, h('b', {}, 'Meilensteine: '), hr.milestones.map((w) => MILESTONES.find((m) => m.wave === w)?.name ?? `W${w}`).join(' · ')) : null) : null,
    h('div', { class: 'cards-grid' },
      h('div', { class: 'card' }, h('h3', {}, `Punkte: ${fmt(sb.total)}`),
        h('table', { class: 'stats-table' }, h('tbody', {},
          h('tr', {}, h('th', {}, 'Wellen'), h('td', {}, fmt(sb.waves))),
          h('tr', {}, h('th', {}, 'Abschüsse'), h('td', {}, fmt(sb.kills))),
          h('tr', {}, h('th', {}, 'Bosse'), h('td', {}, fmt(sb.bosses))),
          h('tr', {}, h('th', {}, 'Sieg + Kernenergie'), h('td', {}, fmt(sb.victory + sb.lives))),
          h('tr', {}, h('th', {}, 'Multiplikator'), h('td', {}, `×${sb.multiplier.toFixed(2)}`))))),
      h('div', { class: 'card' }, h('h3', {}, 'Seed'),
        h('div', { class: 'row tight' }, h('code', { class: 'seed', 'data-testid': 'result-seed' }, s.seed.text),
          h('button', { class: 'btn sm', onclick: () => void copyText(app, s.seed.text) }, '⧉ Kopieren')),
        h('p', { class: 'small muted' }, 'Gleicher Seed + gleiche Regeln = gleiche Karte und gleiche Wellen.'),
        h('h3', {}, 'Modifikatoren'),
        s.modifiers.length ? h('ul', { class: 'small' }, s.modifiers.map((m) => h('li', {}, `${MODIFIERS[m].icon} ${MODIFIERS[m].name}: ${MODIFIERS[m].description}`))) : h('p', { class: 'muted small' }, 'keine')),
      h('div', { class: 'card' }, h('h3', {}, 'Statistik'),
        h('table', { class: 'stats-table' }, h('tbody', {},
          h('tr', {}, h('th', {}, 'Abschüsse'), h('td', {}, fmt(st.kills))),
          h('tr', {}, h('th', {}, 'Durchgebrochen'), h('td', {}, `${st.leaks} (−${st.livesLost} ♥)`)),
          h('tr', {}, h('th', {}, 'Gold verdient / ausgegeben'), h('td', {}, `${fmt(st.goldEarned)} / ${fmt(st.goldSpent)}`)),
          h('tr', {}, h('th', {}, 'Größtes Netzwerk'), h('td', {}, `${st.maxNetworkSize} Türme`)),
          h('tr', {}, h('th', {}, 'Kampfzeit'), h('td', {}, duration(st.duration))),
          h('tr', {}, h('th', {}, 'Bosse'), h('td', {}, st.bossesFaced.length ? st.bossesFaced.map((b) => `${BOSSES[b].name} ${st.bossesKilled.includes(b) ? '✔' : '✖'}`).join(', ') : '–')),
          h('tr', {}, h('th', {}, 'Reaktionen'), h('td', {}, Object.entries(st.reactions).map(([k, v]) => `${REACTION_BY_ID[k]?.name ?? k} ×${v}`).join(', ') || '–')),
          s.heroes.length ? h('tr', {}, h('th', {}, 'Helden'), h('td', {}, `${st.heroDeploys} Einsätze · ${st.heroKills} Abschüsse · ${st.heroDowns} gefallen`)) : null,
          h('tr', {}, h('th', {}, 'Fusionen / Turmfähigkeiten'), h('td', {}, `${st.fusions} / ${st.activesUsed}`)),
          h('tr', {}, h('th', {}, 'Mutanten besiegt'), h('td', {}, String(st.mutationsKilled)))))),
    ),
    h('div', { class: 'cards-grid' },
      h('div', { class: 'card' }, h('h3', {}, 'Schaden je Turmtyp'),
        dmg.length ? h('div', { class: 'hbars' }, dmg.map(([id, v]) => h('div', { class: 'hbar' },
          h('span', { class: 'hl' }, iconImg(app.icons.tower(id, 48), TOWERS[id].icon, 'codex-ic sm'), TOWERS[id].name),
          h('span', { class: 'ht' }, h('span', { class: 'hf', style: `width:${(100 * v) / maxDmg}%` })),
          h('span', { class: 'hv' }, fmt(v))))) : h('p', { class: 'muted' }, '–')),
      h('div', { class: 'card' }, h('h3', {}, 'Gold pro Welle'),
        st.goldPerWave.length ? h('div', { class: 'vbars', role: 'img', 'aria-label': `Gold pro Welle: ${st.goldPerWave.join(', ')}` },
          st.goldPerWave.map((g, i) => h('div', { class: 'vb', title: `Welle ${i + 1}: ${g} Gold`, style: `height:${(100 * g) / maxGold}%` }))) : h('p', { class: 'muted' }, '–')),
    ),
    h('div', { class: 'row center' },
      h('button', { class: 'btn primary', 'data-testid': 'again', onclick: again }, '↻ Gleicher Seed erneut'),
      h('button', { class: 'btn', 'data-testid': 'new-run', onclick: () => app.go('heroSetup') }, '⚔ Neuer Lauf'),
      h('button', { class: 'btn', 'data-testid': 'to-menu', onclick: () => app.go('menu') }, 'Hauptmenü')),
  );
  el.classList.add('results');
  return el;
}
