import type { GameApplication, ScreenId } from '../../app/GameApplication.ts';
import { AFFIX_ORDER, AFFIXES, SCALING, CHALLENGE, LANE_BARRICADE, TOWER_LIMIT } from '../../config/scaling.ts';
import { PRESPEC } from '../../sim/EconomySystem.ts';
import { PARAGON, PARAGON_NODES, PARAGON_TREES } from '../../config/paragon.ts';
import { RUNE_ORDER, RUNE_RULES, RUNE_TIERS, RUNES } from '../../config/runes.ts';
import { ECONOMY, SIM } from '../../config/balance.ts';
import { ABILITIES, BOSSES, ENEMIES, MUTATION_IDS, MUTATIONS, WAVE_ENEMIES } from '../../config/enemies.ts';
import { DUOS, HERO_ORDER, HERO_RULES, HEROES } from '../../config/heroes.ts';
import type { BossId, EnemyId, TowerId } from '../../core/types.ts';
import { iconImg, type IconFactory } from '../../render/IconFactory.ts';
import { CAMPAIGN } from '../../config/campaign.ts';
import { REACTIONS, RESONANCES, STATUS_DEFS } from '../../config/elements.ts';
import { MODIFIER_IDS, MODIFIERS } from '../../config/modifiers.ts';
import { ACTIVE_MIN_LEVEL, FUSION_RANGE, FUSIONS, LEGENDARY_ORDER, SELL_REFUND, TOWER_ACTIVES, TOWER_ORDER, TOWERS } from '../../config/towers.ts';
import { DAMAGE_NAMES } from '../../render/palette.ts';
import { h } from '../dom.ts';
import { baseStatsOf, keyLabel, statLines } from '../text.ts';
import { shell } from './common.ts';
import { PASSIVE, PASSIVE_NODES, PASSIVE_TOTAL_RANKS, TOWER_UNLOCKS } from '../../config/passiveTree.ts';
import { CHAMPION_ORDER, CHAMPIONS, championUnlocked, championUnlockText } from '../../config/champions.ts';
import { HERO_ENDLESS, MILESTONES, PERKS, RESOURCE_ORDER, RESOURCES, WAVE_PROFILES } from '../../config/heroEndless.ts';
import { ITEMS, POTIONS, RARITY } from '../../config/items.ts';

// Hilfe & Kodex. Alle Werte stammen direkt aus der zentralen Konfiguration.

const TABS: [string, string][] = [
  ['controls', 'Steuerung'], ['heroEndless', 'Helden-Endlos'], ['towers', 'Türme'], ['legend', 'Fusion'], ['enemies', 'Gegner'], ['bosses', 'Bosse'],
  ['elements', 'Elemente'], ['network', 'Netzwerke'], ['mods', 'Modifikatoren'], ['economy', 'Wirtschaft'],
];

function resist(r: Partial<Record<string, number>>): string {
  const parts = Object.entries(r).filter(([, v]) => v).map(([k, v]) => `${DAMAGE_NAMES[k as keyof typeof DAMAGE_NAMES]} ${(v as number) > 0 ? '−' : '+'}${Math.round(Math.abs(v as number) * 100)} %`);
  return parts.length ? parts.join(', ') : 'keine';
}

let ICONS: IconFactory | null = null;
const tIc = (id: TowerId): HTMLElement => iconImg(ICONS?.tower(id, 64) ?? null, TOWERS[id].icon, 'codex-ic');
const eIc = (id: Exclude<EnemyId, 'boss'>): HTMLElement => iconImg(ICONS?.enemy(id, 64) ?? null, ENEMIES[id].icon, 'codex-ic');
const bIc = (id: BossId): HTMLElement => iconImg(ICONS?.boss(id, 64) ?? null, '👑', 'codex-ic');

function tabContent(tab: string): HTMLElement {
  switch (tab) {
    case 'heroEndless':
      return h('div', {},
        h('p', {}, `Im Helden-Endlosmodus steuerst du einen frei beweglichen Helden zusätzlich zu deinen Türmen. Nach ${HERO_ENDLESS.prepTime} s Vorbereitung startet die erste Welle automatisch, danach folgt jede Welle nach ${HERO_ENDLESS.transitionTime} s (mit Leertaste früher). Pause (P/Esc) hält alles an.`),
        h('h3', {}, 'Steuerung'),
        h('ul', { class: 'small' },
          h('li', {}, 'Linksklick auf ein freies Feld oder Rechtsklick irgendwo: Held läuft dorthin (auch über die Gegnerbahn und durch Türme). Linksklick auf einen Turm wählt ihn aus.'),
          h('li', {}, 'A / S / D / W: Fähigkeiten. Ziel-Fähigkeiten: Taste drücken, dann Ziel anklicken (oder Taste erneut drücken). Esc/Rechtsklick bricht ab.'),
          h('li', {}, 'Umschalt + 1–4: Tränke der Schnellleiste, Umschalt + 5: Trank im Spezialslot.'),
          h('li', {}, 'Tab: Charakter (Ausrüstung, Werte, Skillbaum, Sammlung, Fortschritt) · X: Laden & Schmiede · Y: Kamera folgt · V: Draufsicht.'),
          h('li', {}, 'Enter oder Startknopf: nächste Welle sofort starten – im Kampf vorzeitig rufen, sobald die laufende Welle vollständig erschienen ist (+Gold je Gegner auf dem Feld, nicht im Bosskampf).'),
          h('li', {}, 'Z oder ⚓: Held festsetzen – Klicks bewegen ihn nicht mehr, er verfolgt keine Gegner (erneut drücken löst). Leertaste im Kampf oder „Mini“: Mini-Pause ohne Menü, Bauen und Aufwerten bleiben möglich.'),
          h('li', {}, 'O oder „Türme“: Turmübersicht mit Aufwerten, Fähigkeit und Info. Türme sind über das ganze Modell anklickbar, Gegner per Klick für Live-Werte (LP, Tempo, Resistenzen, Effekte). Schloss-Knopf: Kameradrehung sperren.'),
          h('li', {}, 'Alle Tasten lassen sich unter Einstellungen → Tastenbelegung ändern (die Angaben hier sind die Standardtasten).')),
        h('h3', {}, 'Späte Wellen (ab 3.0)'),
        h('ul', { class: 'small' },
          h('li', {}, `Ab Welle ${SCALING.expFrom} wachsen die Gegner-LP zusätzlich um ${Math.round((SCALING.expRate - 1) * 100)} % je Welle.`),
          h('li', {}, `Weltstufen: alle ${SCALING.tierEvery} Wellen eine Stufe – je Stufe +${Math.round(SCALING.tierHp * 100)} % LP, mehr Rüstung und Tempo, aber +${Math.round(SCALING.tierReward * 100)} % Kopfgeld.`),
          h('li', {}, `🏰 Bauplätze (Endlos): zu Beginn ${TOWER_LIMIT.base} Türme (Barrikaden, Goldgräber sowie die Unterstützungstürme Leitpylon und Totemschrein zählen nicht), alle ${TOWER_LIMIT.perWaves} Wellen und nach jeder geschafften Bosswelle ein Platz mehr. Fokusbonus – weniger ist mehr: jeder freie Bauplatz macht ALLE Türme um ${Math.round(TOWER_LIMIT.focusPer * 100)} % stärker (höchstens +${Math.round(TOWER_LIMIT.focusMax * 100)} %). Beispiel: 30 Plätze, 20 Türme gebaut → 10 frei → +20 % Schaden.`),
          h('li', {}, `🚧 Lane-Barrikade (Endlos, Classic): direkt auf die Lane setzen – Bodengegner werden darüber um 50 % langsamer. Kostenlos, ohne Turmlimit, mindestens ${LANE_BARRICADE.minSpawnDist} Felder vom Riss entfernt, höchstens ${LANE_BARRICADE.maxCharges} je ${LANE_BARRICADE.window / 60} Minuten Spielzeit.`),
          h('li', {}, `⚔ Challenge-Bosse: alle ${CHALLENGE.every} Wellen bis Welle ${CHALLENGE.switchAt}, danach alle ${CHALLENGE.everyLate} Wellen. ${CHALLENGE.hpMul.toString().replace('.', ',')}× LP (ab Welle 150 je 100 Wellen noch einmal so viel, z. B. Welle 250 ×7), +${Math.round(CHALLENGE.resist * 100)} % Resistenzen, Kontrolleffekte nur zu ${Math.round(CHALLENGE.ccMul * 100)} %, Schildschub alle ${CHALLENGE.shieldEvery} s, ${CHALLENGE.summonCount} Elite-Begleiter alle ${CHALLENGE.summonEvery} s, Raserei nach ${CHALLENGE.enrageAfter} s. Ein Durchbruch kostet mindestens ${CHALLENGE.leakMin} bzw. ${Math.round(CHALLENGE.leakPct * 100)} % der Kernenergie; der Sieg bringt ${CHALLENGE.rewardMul}× Kopfgeld, Relikte und eine Rune.`),
          h('li', {}, `Spezialisierter Turmkauf ab Welle ${PRESPEC.from}: Ein Klick auf einen Turm in der Bauleiste öffnet die Auswahl Normal / Spezialisierung A / B. Spezialisierte Türme werden direkt auf Höchststufe gebaut und kosten Grundpreis + alle Stufen + Spezialisierung, +${Math.round((PRESPEC.premium - 1) * 100)} % Aufpreis.`),
          h('li', {}, `Wellen-Affixe ab Welle ${SCALING.affixFrom} (1, ab ${SCALING.affixFrom + SCALING.affixStep} zwei, ab ${SCALING.affixFrom + 2 * SCALING.affixStep} drei): ${AFFIX_ORDER.map((a) => `${AFFIXES[a].icon} ${AFFIXES[a].name} – ${AFFIXES[a].description}`).join(' · ')}. Sie stehen vor jeder Welle in der Wellenvorschau.`),
          h('li', {}, `Ab Welle ${SCALING.pressureFrom}: Elitegegner in jeder Welle, Mini-Bosse alle ${SCALING.miniBossEveryLate} Wellen, durchbrechende Bosse kosten doppelt Kernenergie.`),
          h('li', {}, `Ab Welle ${SCALING.overrunFrom} rückt nach ${SCALING.overrunAfter} s Kampf die nächste Welle automatisch nach (Countdown in der Wellenleiste).`),
          h('li', {}, `Turm-Meisterschaft: Jeder Turmtyp sammelt dauerhaft Abschüsse – Rang r ab 50·r² Abschüssen, je Rang +1 % Schaden (max. Rang 20).`),
          h('li', {}, `Meilensteine W100/150/200/250/300 geben dauerhaft Paragonpunkte (insgesamt 15) und Startgold.`)),
        h('h3', {}, 'Heldenstufe & Paragon'),
        h('p', { class: 'small' }, `Heldenstufe und Skillbaum jedes Helden bleiben dauerhaft erhalten (höchstens Stufe ${PARAGON.heroMaxLevel}; ab Stufe ${PARAGON.softcapLevel} wachsen die Grundwerte nur noch halb so schnell). Ab Stufe 50 gibt es alle 5 Stufen einen Vorab-Paragonpunkt (10 bis Stufe 95). Ab Stufe ${PARAGON.heroMaxLevel} fließt jede Erfahrung ins Paragon – gemeinsam für alle Helden, bis Paragon ${PARAGON.maxLevel}. Jede Paragonstufe gibt einen Punkt für einen von drei Bäumen: ${PARAGON_TREES.map((t) => `${t.icon} ${t.name} (${PARAGON_NODES.filter((n) => n.tree === t.id).map((n) => n.name).join(', ')})`).join(' · ')}. Vergeben unter Charakter → Paragon, kostenlos zurücksetzbar in der Bauphase.`),
        h('h3', {}, 'Runen'),
        h('p', { class: 'small' }, `Runen sind nur findbar – Runensplitter ab Welle ${RUNE_RULES.splinterFrom}, volle Runen ab Welle ${RUNE_RULES.fromWave} (Gegner, vor allem Elite, Mini-Bosse und Bosse, sowie Wellenabschluss). Drei Qualitäten (${RUNE_TIERS.map((t) => `${t.name} ×${String(t.mul).replace('.', ',')}`).join(', ')}). Runensockel gehören der Turmsorte (auch jeder Fusion): 1 ab Welle 100, 2 ab Welle 250, 3 ab Welle 400, 4 ab Welle 500, legendäre Fusionen je +1. Eine gesockelte Rune wirkt auf alle Türme dieser Sorte, auch auf später gebaute; ein Wechsel gilt sofort für alle. Im Turmpanel zeigt ein Klick auf eine Rune ihre Wirkung, ein Doppelklick sockelt sie bzw. nimmt sie wieder heraus (kostenlos, auch per Knopf in der Infobox). Beim Verkauf bleiben die Runen bei der Sorte. ${RUNE_ORDER.map((r) => `${RUNES[r].icon} ${RUNES[r].name}: ${RUNES[r].description}`).join(' · ')}.`),
        h('h3', {}, 'Abbauen'),
        h('p', { class: 'small' }, `Bäume, Felsen, Kristalle und andere Hindernisse (nicht Lava und Wolken) anklicken und „Abbauen“ wählen. Der Held läuft hin und baut so lange ab, wie eine Welle im Schnitt dauert (${HERO_ENDLESS.mineTimeMin}–${HERO_ENDLESS.mineTimeMax} s, anfangs ${HERO_ENDLESS.mineTimeDefault} s). Der Abbau lässt sich nicht unterbrechen: Der Held bewegt sich nicht, kämpft nicht und wirkt keine Fähigkeiten, nimmt aber auch keinen Schaden. Ausbeute: zufällig Gold (steigt mit der Welle) und meist Metall, oft Kristall, selten Essenz oder ein Relikt. Danach ist das Feld frei und bebaubar.`),
        h('h3', {}, 'Spezialfelder'),
        h('p', { class: 'small' }, `Goldadern (gelb): Goldgräber zahlen +${Math.round((ECONOMY.veinMul - 1) * 100)} % und zusätzlich ${ECONOMY.veinFlat} + ${ECONOMY.veinFlatPerWave} × Welle Gold. Kristallvorkommen (blau, nur Helden-Endlos, ${HERO_ENDLESS.depositCount[0]}–${HERO_ENDLESS.depositCount[1]} je Karte): ein Goldgräber darauf fördert jede Welle zusätzlich Kristall. Beim Platzieren eines Turms zeigt die Bauleiste das Feld und alle möglichen Fusionen samt Partnern in Reichweite.`),
        h('h3', {}, 'Helden'),
        h('p', { class: 'small' }, 'Zwölf Helden sind spielbar – weitere werden über die Bestwelle freigeschaltet. Jeder Skillbaum enthält den Knoten „Kernwall“: +3 Kernenergie je Rang (bis +15), also mehr Gegner, die den Kern erreichen dürfen.'),
        h('ul', { class: 'small' }, CHAMPION_ORDER.map((id) => h('li', {}, h('b', {}, `${CHAMPIONS[id].name} (${CHAMPIONS[id].role})`), `${championUnlocked(id, [], 0, []) ? '' : ` – ${championUnlockText(id)}`}: ${CHAMPIONS[id].description} Fähigkeiten: ${CHAMPIONS[id].skills.map((s) => `${s.name} (${s.cooldown} s, ${s.mana} Mana)`).join(', ')}.`))),
        h('h3', {}, 'Beute, Ressourcen, Herstellung'),
        h('p', { class: 'small' }, `Gegner lassen Erfahrung fallen, die der Held im Aufsammelradius einsammelt. Dazu Ressourcen (${RESOURCE_ORDER.map((r) => `${RESOURCES[r].icon} ${RESOURCES[r].name}`).join(', ')}), Tränke, Fragmente und selten ganze Items. Elite, Mini-Bosse (ab Welle ${HERO_ENDLESS.miniBossFrom} alle ${HERO_ENDLESS.miniBossEvery} Wellen) und Bosse haben bessere Beute. Jede Welle bringt zusätzlich Ressourcen.`),
        h('p', { class: 'small' }, `Fragmente ergeben in der Schmiede ein Item (${Object.values(RARITY).map((r) => `${r.name}: ${r.fragments}`).join(', ')}). Bereits freigeschaltete Items werden nie erneut als identischer Drop vergeben; überzählige Fragmente werden zu Essenz. Items lassen sich bis +3 verstärken und kombinieren (legendär).`),
        h('p', { class: 'small' }, `${Object.keys(ITEMS).length} Items in 6 Slots, ${Object.keys(POTIONS).length} Tränke (stapelbar). Laden, Schmiede und Verkauf nur in Reichweite des Ladens nahe dem Kern.`),
        h('h3', {}, 'Türme nach und nach'),
        h('p', { class: 'small' }, `Im Helden-Endlosmodus kommen Türme mit den Wellen hinzu: ${Object.entries(TOWER_UNLOCKS).filter(([id, w]) => w > 0 && !TOWERS[id as keyof typeof TOWERS].legendary).sort((a, b) => a[1] - b[1]).reduce<string[]>((acc, [id, w]) => { const last = acc[acc.length - 1]; const name = TOWERS[id as keyof typeof TOWERS].name; if (last && last.startsWith(`W${w}:`)) acc[acc.length - 1] = `${last}, ${name}`; else acc.push(`W${w}: ${name}`); return acc; }, []).join(' · ')}. Luftabwehr kommt vor den ersten Fliegern, Detektoren vor den Schattenschleichern, Fusionspartner paarweise. Turmfähigkeiten lösen ab Stufe 2 automatisch aus.`),
        h('h3', {}, 'Passiver Baum'),
        h('p', { class: 'small' }, `1 Punkt je ${PASSIVE.wavesPerPoint} Bestwellen, höchstens ${PASSIVE.maxPoints} (Bestwelle ${PASSIVE.maxPoints * PASSIVE.wavesPerPoint}). ${PASSIVE_NODES.length} Knoten in ${PASSIVE.tierGate.length} Stufen, insgesamt ${PASSIVE_TOTAL_RANKS} Ränge; Punkte werden im Startbildschirm vergeben und können kostenlos zurückgesetzt werden. Wirkt automatisch: ${PASSIVE_NODES.map((n) => n.name).join(', ')}.`),
        h('h3', {}, 'Wellenprofile'),
        h('p', { class: 'small' }, WAVE_PROFILES.map((p) => p.name).join(' · ')),
        h('h3', {}, 'Dauerhafter Fortschritt'),
        h('ul', { class: 'small' }, PERKS.map((p) => h('li', {}, `${p.icon} ${p.name} (Welle ${p.wave}): ${p.description}`))),
        h('p', { class: 'small' }, 'Meilensteine: ', MILESTONES.map((m) => `W${m.wave} ${m.name}`).join(' · ')),
        h('p', { class: 'small muted' }, 'Freischaltungen eines Laufs werden bei Partieende gespeichert (auch bei Neustart oder wenn ein fortsetzbarer Lauf durch einen neuen ersetzt wird). Die Bestwelle wird nach jeder Welle gespeichert.'));
    case 'heroes':
      return h('div', {},
        h('p', {}, `Helden werden mit Rissenergie (⚡) auf einer Wegzelle eingesetzt (Taste Z/X oder Porträt antippen). Rissenergie (max. ${HERO_RULES.maxEnergy}) erhältst du für Abschüsse, Elitegegner, Bosse, Elementarreaktionen und überstandene Wellen. Ein Einsatz dauert eine begrenzte Zeit, danach erholt sich der Held ${HERO_RULES.cooldown} s; fällt er, dauert es ${HERO_RULES.downCooldown} s. Rückzug zum Kern erstattet ${Math.round(HERO_RULES.retreatRefund * 100)} % der Abklingzeit.`),
        h('p', {}, `Helden sammeln Erfahrung durch Abschüsse in ihrer Nähe und steigen bis Stufe ${HERO_RULES.maxLevel} auf. Jede Stufe bietet eine Wahl zwischen zwei Verbesserungen. Zwei Fähigkeiten (A/S) und eine Ultimate (D), die sich im Kampf auflädt. Verhalten: Halten, Vorrücken, Rückzug (V wechselt).`),
        h('div', { class: 'codex' }, HERO_ORDER.map((id) => {
          const d = HEROES[id];
          const m = CAMPAIGN.find((x) => x.id === d.unlock);
          return h('details', { class: 'codex-entry' },
            h('summary', {}, iconImg(ICONS?.hero(id, 64) ?? null, d.icon, 'codex-ic'), ` ${d.name} – ${d.title} · ${d.role}${d.support ? ' (Unterstützer)' : ''} · ${d.cost} ⚡`),
            h('p', {}, d.description),
            h('p', { class: 'small' }, `LP ${d.base.maxHp} · Schaden ${d.base.damage} · Reichweite ${d.base.range}${d.base.auraRadius ? ` · Aura ${d.base.auraRadius} Felder` : ''}${d.base.blockCount ? ` · hält ${d.base.blockCount} Gegner fest` : ''} · Einsatz ${d.base.activeTime} s`),
            h('ul', { class: 'small' },
              d.abilities.map((a, i) => h('li', {}, h('b', {}, `${i ? 'S' : 'A'} · ${a.name}`), ` (${a.cooldown} s): ${a.description}`)),
              h('li', {}, h('b', {}, `D · ${d.ultimate.name}`), `: ${d.ultimate.description}`)),
            h('p', { class: 'small' }, h('b', {}, 'Perks: '), d.perks.map((pair, i) => `St. ${i + 2}: ${pair[0].name} / ${pair[1].name}`).join(' · ')),
            h('p', { class: 'small muted' }, d.unlock === 'start' ? 'Von Beginn an verfügbar.' : `Freischaltung: Mission ${m ? `${m.index + 1} „${m.name}“` : d.unlock} abschließen.`));
        })),
        h('h2', {}, 'Duo-Synergien'),
        h('ul', {}, DUOS.map((d) => h('li', {}, h('b', {}, d.name), ` (${HEROES[d.a].name.split(' ').slice(-1)[0]} + ${HEROES[d.b].name.split(' ').slice(-1)[0]}, höchstens ${HERO_RULES.duoRadius} Felder entfernt): ${d.description}`))));
    case 'legend':
      return h('div', {},
        h('p', {}, `Zwei voll ausgebaute (Stufe 4 und spezialisiert) passende Türme, die höchstens ${FUSION_RANGE} Felder auseinander stehen, lassen sich zu einem legendären Turm verschmelzen. Der zweite Turm geht dabei in den ersten über. Legendäre Türme können nicht direkt gebaut werden.`),
        h('div', { class: 'codex' }, FUSIONS.map((f) => {
          const d = TOWERS[f.id];
          return h('details', { class: 'codex-entry' },
            h('summary', {}, tIc(f.id), ` ${d.name} = ${TOWERS[f.a].name} + ${TOWERS[f.b].name} · ${f.cost} Gold`),
            h('p', {}, d.description),
            h('table', { class: 'stats-table' }, h('tbody', {}, statLines(baseStatsOf(f.id), d).map(([k, v]) => h('tr', {}, h('th', {}, k), h('td', {}, v))))),
            d.active ? h('p', { class: 'small' }, h('b', {}, `Aktiv: ${TOWER_ACTIVES[d.active].name}`), ` – ${TOWER_ACTIVES[d.active].description}`) : null);
        })),
        h('h2', {}, 'Aktive Turmfähigkeiten'),
        h('p', { class: 'small' }, `Ab Stufe ${ACTIVE_MIN_LEVEL} lösen viele Türme während einer Welle automatisch eine Fähigkeit aus, sobald die Abklingzeit abgelaufen ist und Gegner in Reichweite sind. Mit Taste C oder dem Knopf in den Turmdetails geht es auch von Hand.`),
        h('ul', { class: 'small' }, Object.values(TOWER_ACTIVES).map((a) => h('li', {}, h('b', {}, a.name), ` (${a.cooldown} s): ${a.description} – ${[...TOWER_ORDER, ...LEGENDARY_ORDER].filter((id) => TOWERS[id].active === a.id).map((id) => TOWERS[id].name).join(', ')}`))));
    case 'towers':
      return h('div', { class: 'codex' }, TOWER_ORDER.map((id) => {
        const d = TOWERS[id];
        return h('details', { class: 'codex-entry' },
          h('summary', {}, tIc(id), ` ${d.name} – ${d.role} · ${d.cost} Gold · Taste ${keyLabel(d.hotkey)}`),
          h('p', {}, d.description),
          d.active ? h('p', { class: 'small' }, h('b', {}, `Aktiv (ab Stufe ${ACTIVE_MIN_LEVEL}): ${TOWER_ACTIVES[d.active].name}`), ` – ${TOWER_ACTIVES[d.active].description}`) : null,
          d.detector ? h('p', { class: 'small good' }, 'Detektor: enttarnt getarnte Gegner in Reichweite.') : null,
          h('table', { class: 'stats-table' }, h('tbody', {}, statLines(baseStatsOf(id), d).map(([k, v]) => h('tr', {}, h('th', {}, k), h('td', {}, v))))),
          h('p', { class: 'small' }, h('b', {}, 'Stufen: '), d.upgrades.map((u, i) => `${i + 2}. ${u.name} (${u.cost}): ${u.description}`).join(' · ')),
          h('p', { class: 'small' }, h('b', {}, 'Spezialisierungen: '), d.specs.map((s, i) => `${i ? 'Ⓑ' : 'Ⓐ'} ${s.name} (${s.cost}): ${s.description}`).join(' · ')),
          h('p', { class: 'small' }, '✚ ', d.synergies), h('p', { class: 'small' }, '✖ ', d.counters));
      }), h('details', { class: 'codex-entry' }, h('summary', {}, `🧱 ${TOWERS.barricade.name} – nur Mazing · ${TOWERS.barricade.cost} Gold · Taste B`), h('p', {}, TOWERS.barricade.description)));
    case 'enemies':
      return h('div', { class: 'codex' }, WAVE_ENEMIES.map((id) => {
        const d = ENEMIES[id];
        return h('details', { class: 'codex-entry' },
          h('summary', {}, eIc(id as Exclude<EnemyId, 'boss'>), ` ${d.name} – ${d.role} (ab Welle ${d.minWave})`),
          h('p', {}, d.description),
          h('p', { class: 'small' }, `LP ${d.hp} · Tempo ${d.speed} · Kopfgeld ${d.reward} · Resistenzen: ${resist(d.resist)}${d.flying ? ' · fliegt' : ''}${d.stealth ? ' · getarnt (Detektoren: Runenobelisk, Resonator, Totem)' : ''}${d.burrow ? ' · gräbt sich ein' : ''}${d.evasion ? ` · weicht ${Math.round(d.evasion * 100)} % der Einzeltreffer aus` : ''}`),
          d.abilities.length ? h('p', { class: 'small' }, d.abilities.map((a) => `${ABILITIES[a].name}: ${ABILITIES[a].description}`).join(' ')) : null,
          h('p', { class: 'small' }, '🛡 Konter: ', d.counter));
      }),
      h('details', { class: 'codex-entry' }, h('summary', {}, '★ Elitevarianten'),
        h('p', {}, 'Elitegegner (goldener Ring) haben dreifache Lebenspunkte, +10 % Resistenzen und wirken „Erdstoß“: Stehen mindestens drei Türme dicht beieinander, kündigen sie einen Stoß an (Ring), der diese Türme 3 s betäubt. Resonatoren unterbrechen das, verteilte Türme verhindern es.')),
      h('details', { class: 'codex-entry' }, h('summary', {}, '🧬 Mutationen'),
        h('p', {}, 'Ab Welle 8 können Elitegegner mutiert sein (farbiger Ring am Boden, orange markiert in der Wellenvorschau):'),
        h('ul', {}, MUTATION_IDS.map((m) => h('li', {}, `${MUTATIONS[m].icon} `, h('b', {}, MUTATIONS[m].name), `: ${MUTATIONS[m].description}`)))),
      h('details', { class: 'codex-entry' }, h('summary', {}, '🛡 Adaptive Schilde'),
        h('p', {}, 'Schildträger passen ihren Schild an Schadensarten an, die sie wiederholt treffen (sichtbar als farbige Würfel über dem Lebensbalken, max. −45 %). Gemischte Schadensarten umgehen das. Die Anpassung ist begrenzt und gilt nur für den Schild.')));
    case 'bosses':
      return h('div', { class: 'codex' }, Object.values(BOSSES).map((b) => h('div', { class: 'card' },
        h('h3', {}, bIc(b.id), b.name), h('p', { class: 'muted' }, b.title), h('p', {}, b.description),
        h('ul', {}, b.phases.map((p) => h('li', {}, h('b', {}, p.name), p.abilities.length ? `: ${p.abilities.map((a) => `${ABILITIES[a].name} – ${ABILITIES[a].description}`).join(' ')}` : ''))),
        h('p', { class: 'small' }, `Resistenzen: ${resist(b.resist)} · Belohnung ${b.reward} Gold`),
        h('p', {}, '🛡 Konter: ', b.counter))),
      h('p', { class: 'small muted' }, 'Bossfähigkeiten werden mit Ringen und Warnungen angekündigt. Schall- und Frostschaden senken die Standfestigkeit; bricht sie, wird die aktuelle Fähigkeit abgebrochen und der Boss taumelt.'));
    case 'elements':
      return h('div', {},
        h('h2', {}, 'Reaktionen'),
        h('ul', {}, REACTIONS.map((r) => h('li', {}, h('b', {}, r.name), ` – ${r.description}${r.cooldown ? ` (Abklingzeit ${r.cooldown} s je Gegner)` : ''}`))),
        h('p', { class: 'small muted' }, 'Reaktionsschaden löst keine weiteren Reaktionen aus. Kälte ist auf 75 % Verlangsamung begrenzt, Markierungen auf 60 % Resistenzsenkung; nach dem Einfrieren folgt 3 s Immunität.'),
        h('h2', {}, 'Zustände'),
        h('ul', { class: 'small' }, Object.values(STATUS_DEFS).filter((s) => s.id !== 'weather' && s.id !== 'freezeImmune').map((s) => h('li', {}, `${s.icon} ${s.name}: ${s.description}`))));
    case 'network':
      return h('div', {},
        h('p', {}, 'Leitpylone verbinden Türme in ihrer Verbindungsreichweite (Linien). Verbundene Türme erhalten den Netzwerkbonus des stärksten Pylons im Netz als Schaden und Angriffstempo. Ein Turm kann maximal zwei Pylonverbindungen haben; Pylone haben eine begrenzte Anzahl Verbindungen.'),
        h('p', {}, 'Netzwerke werden nur bei Bau, Verkauf, Ausbau oder Störung neu berechnet. Funkenfresser und der Nullhüter stören Türme (rotes ✖) – gestörte Türme fallen aus dem Netz.'),
        h('h2', {}, 'Resonanzen'),
        h('ul', {}, RESONANCES.map((r) => h('li', {}, h('b', {}, r.name), ` (${r.requires.map((t) => TOWERS[t].name).join(' + ')}): ${r.description}`)),
          h('li', {}, h('b', {}, 'Heldenchor'), ' (Runenbardin in Aurareichweite eines Netzes mit Feuer-, Frost- und Blitzturm): alle Türme dieses Netzes +20 % Schaden.')),
        h('p', { class: 'small muted' }, 'Verbindungslinien leuchten golden, wenn eine Resonanz aktiv ist.'));
    case 'mods':
      return h('div', {},
        h('p', {}, 'Nach bestimmten Wellen wählst du einen von drei Riss-Modifikatoren (oder +30 Gold). Unvereinbare Kombinationen werden nie gemeinsam angeboten; mindestens ein Angebot ist sofort nutzbar.'),
        h('ul', {}, MODIFIER_IDS.map((id) => h('li', {}, `${MODIFIERS[id].icon} `, h('b', {}, MODIFIERS[id].name), `: ${MODIFIERS[id].description}`))));
    case 'economy':
      return h('div', {},
        h('ul', {},
          h('li', {}, `Wellenbonus: ${ECONOMY.waveBonusBase} + ${ECONOMY.waveBonusPerWave} × Welle Gold (Bosswellen +${ECONOMY.bossWaveBonus}).`),
          h('li', {}, 'Abschüsse geben Kopfgeld, das mit der Welle leicht steigt.'),
          h('li', {}, `Verkauf: ${Math.round(SELL_REFUND * 100)} % der Investition. In derselben Bauphase gebaute Türme werden voll erstattet (Rückgängig) – das ermöglicht keine Geldvermehrung.`),
          h('li', {}, 'Goldgräber zahlen am Ende jeder Welle, frühestens ab der Welle nach dem Bau. Auf Goldadern +50 % und ein fester Zusatzbetrag, der mit der Welle wächst. Amortisation ca. 5 Wellen auf Goldadern, sonst ca. 8 Wellen – zu viele Minen schwächen die frühe Verteidigung.'),
          h('li', {}, `Anhöhen: +${ECONOMY.highgroundRangeAdd} Reichweite. Leylinien: +${Math.round((ECONOMY.leyDamageMul - 1) * 100)} % Schaden.`)));
    default:
      return h('div', {},
        h('h2', {}, 'Ziel'),
        h('p', {}, 'Halte die Gegnerwellen auf, bevor sie den Kern erreichen. Baue Türme mit Gold, werte sie auf und nutze Elementarreaktionen, Netzwerke und Modifikatoren.'),
        h('h2', {}, 'Maus & Tastatur'),
        h('ul', {},
          h('li', {}, 'Turm wählen: Klick in der Turmleiste oder Tasten 1–0, −, = (Barrikade: B).'),
          h('li', {}, 'Bauen: Klick auf ein freies Feld (grün = erlaubt, rot = nicht erlaubt). Umschalt+Klick baut mehrere.'),
          h('li', {}, 'Turm anklicken: Details, Aufwerten (U), Zielwahl (T), Verkaufen (Entf, zweimal).'),
          h('li', {}, 'Doppelklick auf einen Turm wählt alle Türme dieser Sorte (gold markiert) – „Alle aufwerten“ baut sie gemeinsam aus. „Alle ▲“ oben bzw. in der Turmübersicht wertet alle Türme bis Stufe 4 auf, solange das Gold reicht (günstigste zuerst, Spezialisierung wählst du selbst).'),
          h('li', {}, 'Goldgräber belegen 2×2 Felder (das gewählte Feld ist oben links). Die Turmübersicht blendet voll ausgebaute Türme aus (umschaltbar).'),
          h('li', {}, 'Abbrechen: Rechtsklick oder Esc. Pause mit Menü: P oder Esc. Mini-Pause ohne Menü (weiter bauen/aufwerten): Leertaste im Kampf oder „Mini“. Tempo (1×/2×/3×/5×/8×): F. Welle starten: Leertaste. Das Spiel pausiert nicht automatisch beim Tab- oder Fensterwechsel.'),
          h('li', {}, 'Kamera: Ziehen = verschieben, Mausrad = zoomen, Q/E = um 90° drehen. R = alle Reichweiten, G = Raster, H = Hilfe.'),
          h('li', {}, 'Freie Kamera: mittlere Maustaste (oder Strg/Alt + linke Maustaste) ziehen – links/rechts dreht beliebig, hoch/runter neigt. Tasten , und . drehen um 15°, Bild↑/Bild↓ neigen, Pos1 bzw. ⟲ setzt den Blickwinkel zurück. Touch: mit zwei Fingern drehen.'),
          h('li', {}, 'Turmfähigkeit des ausgewählten Turms: C. Fusion über die Turmdetails.'),
          h('li', {}, 'Minikarte (oben rechts, bei großen Karten automatisch): anklicken, um dorthin zu springen.'),
          h('li', {}, 'Alle Tasten sind unter Einstellungen → Tastenbelegung frei belegbar; doppelt vergebene Tasten werden getauscht, „Standard wiederherstellen“ setzt alles zurück.')),
        h('h2', {}, 'Touch'),
        h('ul', {},
          h('li', {}, 'Turm in der Leiste antippen, dann ein Feld antippen: Vorschau mit Reichweite (und Route im Mazing) erscheint.'),
          h('li', {}, 'Bestätigen mit „Hier bauen“ in der Seitenleiste oder erneutem Tippen auf dasselbe Feld.'),
          h('li', {}, 'Ein Finger ziehen = verschieben, zwei Finger = zoomen. Kamerasteuerung auch über die Knöpfe unten rechts.'),
          h('li', {}, 'Querformat wird empfohlen; im Hochformat bleibt das Spiel bedienbar.')),
        h('h2', {}, 'Simulation'),
        h('p', { class: 'small muted' }, `Die Simulation läuft mit festem Takt (${SIM.tickRate} Hz) unabhängig von der Bildrate. Schnellvorlauf erhöht nur die Anzahl der Takte. Bei verstecktem Tab pausiert der Kampf automatisch.`));
  }
}

export function helpContent(initial: string, icons: IconFactory | null = null): HTMLElement {
  ICONS = icons;
  const body = h('div', { class: 'help-body' });
  const tabs = h('div', { class: 'tabs', role: 'tablist' });
  const show = (tab: string): void => {
    tabs.querySelectorAll('button').forEach((b) => {
      const on = b.dataset.tab === tab;
      b.classList.toggle('on', on);
      b.setAttribute('aria-selected', String(on));
    });
    body.replaceChildren(tabContent(tab));
  };
  for (const [id, label] of TABS) tabs.append(h('button', { class: 'btn sm', role: 'tab', 'data-tab': id, onclick: () => show(id) }, label));
  show(initial);
  return h('div', { class: 'help' }, tabs, body);
}

export function renderHelp(app: GameApplication, tab: string, back: ScreenId): HTMLElement {
  return shell(app, 'Hilfe & Kodex', back, helpContent(tab, app.icons));
}
