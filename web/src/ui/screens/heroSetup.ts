import type { MapDefinition } from '../../core/types.ts';
import { decodeMap } from '../../editor/mapCode.ts';
import { validateMap } from '../../sim/WorldGenerator.ts';
import type { GameApplication } from '../../app/GameApplication.ts';
import { PARAGON, paragonPrePoints, paragonSpent } from '../../config/paragon.ts';
import { milestoneParagon } from '../../config/heroEndless.ts';
import { DIFFICULTY } from '../../config/balance.ts';
import { BIOME_ORDER, BIOMES } from '../../config/biomes.ts';
import { CHAMPION_ORDER, CHAMPIONS, championUnlocked, championUnlockText } from '../../config/champions.ts';
import { activePerks, carrySlots, HERO_ENDLESS, MILESTONES, PERKS } from '../../config/heroEndless.ts';
import { canEquipItem, ITEMS, RARITY } from '../../config/items.ts';
import { randomSeedText } from '../../core/rng.ts';
import type { BiomeId, ChampionId, Difficulty, ItemId, MapSize, MapVariant } from '../../core/types.ts';
import { iconImg } from '../../render/IconFactory.ts';
import type { RunSetup } from '../../sim/setup.ts';
import { h } from '../dom.ts';
import { svgIcon } from '../icons.ts';
import { seg, shell } from './common.ts';
import { passiveRaiseReason } from '../../config/passiveTree.ts';
import { renderPassiveTree } from '../passiveTreeView.ts';

// Startbildschirm des Helden-Endlosmodus: Heldenwahl, Startausrüstung (Rucksack), Karte, Fortschritt.

export function renderHeroSetup(app: GameApplication, heroMap: MapDefinition | null = null): HTMLElement {
  const st = app.save.settings;
  const meta = app.save.progress.heroMeta;
  const perks = activePerks(meta.bestWave);
  const slots = carrySlots(perks);
  const done = app.save.progress.campaignCompleted;
  let champion: ChampionId = CHAMPIONS[meta.lastChampion] && championUnlocked(meta.lastChampion, perks, meta.bestWave, done) ? meta.lastChampion : 'lyra';
  let kit: ItemId[] = meta.startKit.filter((i) => meta.unlockedItems.includes(i)).slice(0, slots);
  const setup: RunSetup = {
    mode: 'heroEndless', seedText: randomSeedText((Date.now() ^ (Math.random() * 1e9)) >>> 0), variant: st.lastVariant, biome: st.lastBiome,
    difficulty: st.lastDifficulty, size: st.lastSize === 'normal' ? 'large' : st.lastSize,
  };
  // 3.10: eigene Karte aus dem Karteneditor (direkt übergeben oder aus den gespeicherten Karten gewählt)
  const savedMaps = app.save.customMaps.map((m) => ({ name: m.name, map: decodeMap(m.code).map })).filter((m): m is { name: string; map: MapDefinition } => !!m.map && validateMap(m.map).ok);
  let mapChoice = heroMap ? 'editor' : 'random';
  const mapHint = h('div', { class: 'tiny muted', 'data-testid': 'map-hint' });
  const applyMap = (): void => {
    const m = mapChoice === 'editor' ? heroMap : mapChoice.startsWith('saved:') ? savedMaps[Number(mapChoice.slice(6))]?.map ?? null : null;
    setup.customMap = m ? { ...m } : null;
    if (m) {
      setup.variant = m.variant;
      setup.biome = m.biome;
    }
    mapHint.textContent = m ? `Eigene Karte „${m.name}“ (${m.variant === 'mazing' ? 'Mazing' : 'Classic'}, ${m.width}×${m.height}) – Variante, Größe und Biom kommen aus der Karte.` : 'Zufällige Welt aus dem Seed.';
  };
  applyMap();
  const mapSelect = h('select', { 'aria-label': 'Karte', 'data-testid': 'map-select', onchange: (e: Event) => { mapChoice = (e.target as HTMLSelectElement).value; applyMap(); } },
    h('option', { value: 'random', selected: mapChoice === 'random' }, 'Zufallswelt (Seed)'),
    heroMap ? h('option', { value: 'editor', selected: mapChoice === 'editor' }, `Aus dem Editor: ${heroMap.name}`) : null,
    savedMaps.map((m, i) => h('option', { value: `saved:${i}` }, `Gespeichert: ${m.name}`)));

  const champBox = h('div', { class: 'champ-picks', role: 'radiogroup', 'aria-label': 'Held wählen', 'data-testid': 'champ-picks' });
  const renderChamps = (): void => {
    champBox.replaceChildren(...CHAMPION_ORDER.map((id) => {
      const d = CHAMPIONS[id];
      const ok = championUnlocked(id, perks, meta.bestWave, done);
      const perk = d.unlockPerk ? PERKS.find((p) => p.id === d.unlockPerk) : undefined;
      return h('button', {
        class: `champ-pick${champion === id ? ' on' : ''}${ok ? '' : ' locked'}`, role: 'radio', 'aria-checked': String(champion === id), 'data-testid': `champ-${id}`, disabled: !ok,
        style: `--hc:#${d.accent.toString(16).padStart(6, '0')}`,
        onclick: () => { champion = id; renderChamps(); renderKit(); app.audio.play('select'); },
      }, iconImg(app.icons.hero(d.model, 96), d.name[0]), h('b', {}, d.name), h('span', { class: 'champ-lvl', 'data-testid': `champ-lvl-${id}` }, `Stufe ${meta.heroLevel.level}/${PARAGON.heroMaxLevel}`), h('span', { class: 'role' }, `${d.title} · ${d.role}`),
      h('span', { class: 'small muted' }, ok ? d.description : `🔒 ${perk ? `Freischaltung: Welle ${perk.wave} erreichen` : championUnlockText(id)}`),
      ok ? h('span', { class: 'tiny' }, `Skills: ${d.skills.map((s) => s.name).join(', ')}`) : null);
    }));
  };

  const kitBox = h('div', { class: 'kit', 'data-testid': 'start-kit' });
  const renderKit = (): void => {
    kit = kit.filter((i) => canEquipItem(i, champion) || ITEMS[i].slot === 'special');
    // 3.4: nur Gegenstände anzeigen, die der gewählte Held tragen kann
    const owned = meta.unlockedItems.filter((i) => ITEMS[i] && canEquipItem(i, champion));
    kitBox.replaceChildren(
      h('p', { class: 'small' }, slots > 0 ? `Rucksack: ${kit.length}/${slots} Items aus der Sammlung kostenlos mitnehmen.` : `Rucksack gesperrt – erreiche Welle ${PERKS.find((p) => p.id === 'carry1')?.wave} für den ersten Platz. Freigeschaltete Items können im Laden jederzeit für Ressourcen in den Lauf geholt werden.`),
      owned.length ? h('div', { class: 'items' }, owned.map((id) => {
        const d = ITEMS[id];
        const on = kit.includes(id);
        const can = canEquipItem(id, champion);
        return h('button', {
          class: `item${on ? ' on' : ''}`, style: `--rc:${RARITY[d.rarity].color}`, disabled: slots === 0 || !can || (!on && kit.length >= slots),
          title: `${d.name} – ${d.description}${can ? '' : ' (nicht für diesen Helden)'}`, 'data-testid': `kit-${id}`,
          onclick: () => { kit = on ? kit.filter((x) => x !== id) : [...kit, id]; renderKit(); },
        }, h('span', { class: 'i-ic' }, d.icon), h('span', { class: 'i-n' }, d.name));
      })) : h('p', { class: 'muted small' }, 'Noch keine Items in der Sammlung.'));
  };

  const nextPerk = PERKS.find((p) => !perks.includes(p.id));
  const nextMs = MILESTONES.find((m) => !meta.claimedMilestones.includes(m.wave));
  const summary = h('div', { class: 'card he-meta', 'data-testid': 'hero-meta' },
    h('h3', {}, 'Fortschritt'),
    h('ul', { class: 'facts' },
      h('li', {}, `Bestwelle: `, h('b', { 'data-testid': 'best-wave' }, String(meta.bestWave)), ` · Läufe: ${meta.runs}`),
      h('li', {}, `Paragon (alle Helden): `, h('b', { 'data-testid': 'setup-paragon' }, `${meta.paragon.level}/${PARAGON.maxLevel}`), ` · ${Math.max(0, meta.paragon.level + paragonPrePoints(meta.heroLevel.level) + milestoneParagon(meta.bestWave) - paragonSpent(meta.paragon.points))} Punkte frei (inkl. Vorab-Punkte ab Stufe 50 und Meilensteine) – vergeben im Lauf unter Charakter → Paragon. Die Heldenstufe (max. ${PARAGON.heroMaxLevel}) gilt für alle Helden gemeinsam und bleibt dauerhaft erhalten; jeder Held hat seinen eigenen Skillbaum.`),
      h('li', {}, `Sammlung: ${meta.unlockedItems.length}/${Object.keys(ITEMS).length} Items · Fragmente: ${Object.values(meta.fragments).reduce<number>((a, b) => a + (b ?? 0), 0)}`),
      h('li', {}, `Aktive Perks: ${perks.length ? perks.map((p) => PERKS.find((x) => x.id === p)?.name).join(', ') : 'keine'}`),
      nextPerk ? h('li', {}, `Nächster Perk: ${nextPerk.icon} ${nextPerk.name} ab Welle ${nextPerk.wave}`) : null,
      nextMs ? h('li', {}, `Nächster Meilenstein: ${nextMs.name} (Welle ${nextMs.wave})`) : null,
    ),
    h('p', { class: 'tiny muted' }, `Ablauf: ${HERO_ENDLESS.prepTime} s Vorbereitung, danach starten die Wellen automatisch (Pause hält alles an). Der Held läuft per Klick/Rechtsklick über die Karte – auch auf der Gegnerbahn und durch Türme hindurch. Gegner lassen Erfahrung, Ressourcen, Tränke und Fragmente fallen. Der Laden steht nahe dem Kern.`));

  const passiveBox = h('div', { class: 'card' });
  const renderPassive = (): void => {
    passiveBox.replaceChildren(renderPassiveTree(meta.passive, meta.bestWave, true,
      (id) => {
        if (passiveRaiseReason(meta.passive, meta.bestWave, id)) return;
        meta.passive[id] = (meta.passive[id] ?? 0) + 1;
        app.persist();
        app.audio.play('upgrade');
        renderPassive();
      },
      () => {
        meta.passive = {};
        app.persist();
        app.audio.play('sell');
        renderPassive();
      }));
  };
  renderPassive();
  renderChamps();
  renderKit();
  const el = shell(app, 'Helden-Endlos', 'menu',
    h('div', { class: 'hero-setup' },
      h('div', { class: 'col' }, h('h3', {}, 'Held'), champBox, h('h3', {}, 'Startausrüstung'), kitBox, passiveBox),
      h('div', { class: 'col' }, summary,
        h('div', { class: 'card options' },
          h('label', { class: 'field' }, h('span', {}, 'Karte'), mapSelect),
          mapHint,
          h('button', { class: 'btn sm', 'data-testid': 'open-editor', onclick: () => app.go('editor') }, '✎ Karteneditor öffnen'),
          h('div', { class: 'field' }, h('span', {}, 'Kartenvariante'), seg<MapVariant>([['classic', 'Classic'], ['mazing', 'Mazing']], setup.variant, (x) => { setup.variant = x; }, 'Kartenvariante')),
          h('div', { class: 'field' }, h('span', {}, 'Kartengröße'), seg<MapSize>([['normal', 'Normal'], ['large', 'Groß'], ['huge', 'Riesig']], setup.size ?? 'large', (x) => { setup.size = x; }, 'Kartengröße')),
          h('label', { class: 'field' }, h('span', {}, 'Biom'),
            h('select', { 'aria-label': 'Biom', onchange: (e: Event) => { setup.biome = (e.target as HTMLSelectElement).value as BiomeId | 'random'; } },
              h('option', { value: 'random', selected: setup.biome === 'random' }, 'Zufällig (aus Seed)'),
              BIOME_ORDER.map((b) => h('option', { value: b, selected: setup.biome === b }, BIOMES[b].name)))),
          h('div', { class: 'field' }, h('span', {}, 'Schwierigkeit'), seg<Difficulty>((['easy', 'normal', 'hard'] as Difficulty[]).map((d) => [d, DIFFICULTY[d].name]), setup.difficulty, (x) => { setup.difficulty = x; }, 'Schwierigkeit')),
          h('label', { class: 'field' }, h('span', {}, 'Seed'),
            h('input', { type: 'text', value: setup.seedText, maxlength: '32', 'aria-label': 'Seed', onchange: (e: Event) => { setup.seedText = (e.target as HTMLInputElement).value || setup.seedText; } })),
          h('button', {
            class: 'btn primary big', 'data-testid': 'start-hero', 'data-autofocus': '',
            onclick: () => {
              app.audio.play('ui');
              if (setup.customMap) { setup.variant = setup.customMap.variant; setup.biome = setup.customMap.biome; }
              app.startRun({ ...setup, heroEndless: app.heroEndlessSetup(champion, kit) });
            },
          }, svgIcon('play'), ' Lauf starten')))));
  return el;
}
