import type { GameApplication } from '../../app/GameApplication.ts';
import { ACHIEVEMENTS, SKINS } from '../../config/achievements.ts';
import { BOSSES } from '../../config/enemies.ts';
import { fmt, h } from '../dom.ts';
import { MODE_NAMES } from '../text.ts';
import { copyText, shell } from './common.ts';

export function renderRecords(app: GameApplication): HTMLElement {
  const p = app.save.progress;
  const got = p.achievements.length;
  return shell(app, 'Bestwerte & Erfolge', 'menu',
    h('p', { class: 'muted' }, 'Alle Werte werden nur auf diesem Gerät gespeichert. Es gibt bewusst keine Online-Rangliste.'),
    h('h2', {}, `Erfolge (${got}/${ACHIEVEMENTS.length})`),
    h('p', { class: 'small muted' }, 'Erfolge schalten ausschließlich kosmetische Turm-Skins frei (Einstellungen → Turm-Skin).'),
    h('div', { class: 'achievements', 'data-testid': 'achievements' }, ACHIEVEMENTS.map((a) => {
      const done = p.achievements.includes(a.id);
      const skin = SKINS.find((k) => k.requires === a.id);
      return h('div', { class: `ach${done ? ' done' : ''}` }, h('span', { class: 'ach-ic', 'aria-hidden': 'true' }, a.icon),
        h('div', {}, h('b', {}, a.name, done ? ' ✔' : ''), h('div', { class: 'small muted' }, a.description), skin ? h('div', { class: 'tiny good' }, `Belohnung: Skin „${skin.name}“`) : null));
    })),
    h('div', { class: 'cards-grid' },
      h('div', { class: 'card' }, h('h3', {}, 'Allgemein'),
        h('p', {}, `Partien: ${p.totalRuns} · Siege: ${p.totalVictories}`),
        h('p', {}, `Besiegte Gegner: ${fmt(p.totalKills)} · Bosse: ${p.totalBossKills}`),
        h('p', { class: 'small' }, `Bestiarium: ${p.bossesDefeated.length}/${Object.keys(BOSSES).length} Bossarten – ${p.bossesDefeated.map((b) => BOSSES[b as keyof typeof BOSSES]?.name ?? b).join(', ') || 'noch keine'}`),
      ),
      h('div', { class: 'card' }, h('h3', {}, 'Helden-Endlos'),
        h('p', {}, `Bestwelle: ${p.heroMeta.bestWave} · Läufe: ${p.heroMeta.runs}`),
        h('p', {}, `Heldenstufe: ${p.heroMeta.heroLevel.level} · Paragon: ${p.heroMeta.paragon.level}`)),
    ),
    h('h2', {}, 'Beste Partien'),
    p.bestScores.length === 0
      ? h('p', { class: 'muted' }, 'Noch keine Partie abgeschlossen.')
      : h('table', { class: 'records' },
        h('thead', {}, h('tr', {}, h('th', {}, '#'), h('th', {}, 'Punkte'), h('th', {}, 'Modus'), h('th', {}, 'Wellen'), h('th', {}, 'Ergebnis'), h('th', {}, 'Seed'), h('th', {}, 'Datum'))),
        h('tbody', {}, p.bestScores.slice(0, 20).map((b, i) => h('tr', {},
          h('td', {}, String(i + 1)), h('td', {}, fmt(b.score)), h('td', {}, MODE_NAMES[b.mode]), h('td', {}, String(b.waves)),
          h('td', {}, b.victory ? '✔ Sieg' : '✖ Niederlage'),
          h('td', {}, h('button', { class: 'btn sm', title: 'Seed kopieren', onclick: () => void copyText(app, b.seed) }, b.seed)),
          h('td', {}, b.date))))));
}
