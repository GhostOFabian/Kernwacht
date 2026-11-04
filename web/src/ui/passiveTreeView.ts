import { PASSIVE, PASSIVE_NODES, PASSIVE_TOTAL_RANKS, passiveEffects, passivePoints, passiveRaiseReason, passiveSpent, type PassiveEffects } from '../config/passiveTree.ts';
import { fmtNum, h } from './dom.ts';

// Darstellung des passiven Baums (Startbildschirm: bearbeitbar; im Lauf: nur Anzeige).

const FX_LABEL: Record<keyof PassiveEffects, [string, 'pct' | 'num' | 'int']> = {
  heroHpPct: ['Helden-LP', 'pct'], heroDmgPct: ['Heldenschaden', 'pct'], heroArmor: ['Heldenrüstung', 'pct'], heroCdr: ['Abklingzeitreduktion', 'pct'],
  heroCrit: ['Krit-Chance Held', 'pct'], heroRegen: ['LP-Regeneration', 'num'], manaRegen: ['Manaregeneration', 'num'], supportPower: ['Unterstützungsstärke', 'pct'],
  pickupRadius: ['Aufsammelradius', 'num'], xpPct: ['Erfahrung', 'pct'], towerDmgPct: ['Turmschaden', 'pct'], towerSpeedPct: ['Turmtempo', 'pct'],
  towerRange: ['Turmreichweite', 'num'], startGold: ['Startgold', 'int'], killGoldPct: ['Kopfgeld', 'pct'], resourcePct: ['Ressourcen', 'pct'],
  lootPct: ['Beutechance', 'pct'], coreLives: ['Kernenergie', 'int'], respawnPct: ['Schnellere Wiederbelebung', 'pct'], unlockEarlier: ['Türme früher (Wellen)', 'int'],
  heroAtkSpeed: ['Angriffstempo Held', 'pct'], abilityPct: ['Fähigkeitsstärke', 'pct'], moveSpeed: ['Bewegungstempo', 'num'], lifesteal: ['Lebensraub', 'pct'],
  towerCrit: ['Krit-Chance Türme', 'pct'], towerPierce: ['Panzerbrechung Türme', 'pct'], towerCostPct: ['Baukosten Türme −', 'pct'], mineIncomePct: ['Goldgräber-Ertrag', 'pct'], towerSplashPct: ['Flächenradius Türme', 'pct'],
};

export function effectLines(fx: PassiveEffects): string[] {
  const out: string[] = [];
  for (const [k, [name, kind]] of Object.entries(FX_LABEL) as [keyof PassiveEffects, [string, string]][]) {
    const v = fx[k];
    if (!v) continue;
    out.push(`${name} +${kind === 'pct' ? `${fmtNum(v * 100, 1)} %` : kind === 'int' ? Math.round(v) : fmtNum(v, 2)}`);
  }
  return out;
}

export function renderPassiveTree(passive: Record<string, number>, bestWave: number, editable: boolean, onRaise?: (id: string) => void, onReset?: () => void): HTMLElement {
  const points = passivePoints(bestWave);
  const spent = passiveSpent(passive);
  const fx = passiveEffects(passive);
  const lines = effectLines(fx);
  const nextAt = points < PASSIVE.maxPoints ? (points + 1) * PASSIVE.wavesPerPoint : null;
  const root = h('div', { class: 'passive', 'data-testid': 'passive-tree' },
    h('div', { class: 'passive-head' },
      h('div', {}, h('b', {}, 'Passiver Baum'), ` · Punkte `, h('b', { 'data-testid': 'passive-points' }, `${points - spent} frei`), ` (${spent}/${points} vergeben, max. ${PASSIVE.maxPoints} bei Bestwelle ${PASSIVE.maxPoints * PASSIVE.wavesPerPoint})`),
      editable && spent > 0 && onReset ? h('button', { class: 'btn sm', 'data-testid': 'passive-reset', title: 'Alle Punkte kostenlos zurückerstatten', onclick: () => onReset() }, 'Zurücksetzen') : null),
    h('p', { class: 'tiny muted' }, `1 Punkt je ${PASSIVE.wavesPerPoint} Bestwellen${nextAt ? ` · nächster Punkt bei Bestwelle ${nextAt}` : ''}. Wirkt automatisch in jedem Helden-Endloslauf (Stand beim Laufstart). ${PASSIVE_TOTAL_RANKS} Ränge insgesamt.`),
    lines.length ? h('p', { class: 'small passive-fx' }, 'Aktiv: ', lines.join(' · ')) : null);
  for (let tier = 1; tier <= PASSIVE.tierGate.length; tier++) {
    const gate = PASSIVE.tierGate[tier - 1];
    const open = spent >= gate;
    root.appendChild(h('div', { class: `ptier${open ? '' : ' closed'}` },
      h('div', { class: 'ptier-h' }, `Stufe ${tier}`, gate ? h('span', { class: 'tiny muted' }, ` · ab ${gate} vergebenen Punkten`) : null),
      h('div', { class: 'pnodes' }, PASSIVE_NODES.filter((n) => n.tier === tier).map((n) => {
        const rank = passive[n.id] ?? 0;
        const why = passiveRaiseReason(passive, bestWave, n.id);
        return h('button', {
          class: `pnode${rank ? ' has' : ''}${rank >= n.maxRank ? ' max' : ''}${editable && !why ? ' can' : ''}`, 'data-testid': `pnode-${n.id}`,
          disabled: !editable || !!why, title: `${n.name}: ${n.description} je Rang${why && rank < n.maxRank ? `\n${why}` : ''}`,
          onclick: () => onRaise?.(n.id),
        }, h('span', { class: 'p-ic' }, n.icon), h('b', {}, n.name), h('span', { class: 'rank' }, `${rank}/${n.maxRank}`), h('span', { class: 'tiny' }, `${n.description} je Rang`));
      }))));
  }
  return root;
}
