import { CHAMPIONS, STAT_MULT, STAT_NAMES, STAT_PCT, type SkillNode } from '../config/champions.ts';
import { PARAGON, PARAGON_NODES, PARAGON_TREES, paragonPrePoints, paragonSpent, paragonXpToNext } from '../config/paragon.ts';
import { paragonEffects } from '../config/passiveTree.ts';
import { paragonFree, paragonRaiseReason } from '../sim/ChampionSystem.ts';
import { effectLines } from './passiveTreeView.ts';
import { boundKey, keyName } from '../config/keys.ts';
import { HERO_ENDLESS, MILESTONES, PERKS, RESOURCE_ORDER, RESOURCES, WAVE_PROFILES, xpToNext } from '../config/heroEndless.ts';
import { ITEMS, itemStats, MAX_PLUS, POTIONS, RARITY, RECIPES, sellValue, SHOP_CATEGORIES, SHOP_OFFERS, SLOTS, type Cost, type ShopCategory } from '../config/items.ts';
import type { ChampionStats, ItemInstance, PotionId } from '../core/types.ts';
import type { IconFactory } from '../render/IconFactory.ts';
import { iconImg } from '../render/IconFactory.ts';
import { championStats, nodeState, skillState } from '../sim/ChampionSystem.ts';
import type { GameSimulation } from '../sim/GameSimulation.ts';
import { collectionOffers, collectionState, nearShop, offerState, potionState, recipeState, upgradeState } from '../sim/InventorySystem.ts';
import { clear, fmt, fmtNum, h, put, setText, toggleClass } from './dom.ts';
import { svgIcon } from './icons.ts';
import { renderPassiveTree } from './passiveTreeView.ts';

// HUD-Erweiterung des Helden-Endlosmodus: Heldenleiste (LP/Mana/EP, Skills, Tränke),
// Ressourcen in der Kopfzeile, Laden und Charakterbogen als Fenster. Liest nur; Aktionen über HeroHudActions.

export interface HeroHudActions {
  championSkillKey(slot: number): void;
  usePotion(potion: PotionId): void;
  slotPotion(potion: PotionId): void;
  learnSkill(nodeId: string): void;
  paragonPoint(nodeId: string): void;
  paragonReset(): void;
  treeReset(): void;
  equip(uid: number): void;
  unequip(slot: number): void;
  shopBuy(offerId: string): void;
  shopBuyItem(item: string): void;
  shopSell(uid: number): void;
  craft(recipeId: string): void;
  upgradeItem(uid: number): void;
  focusChampion(): void;
  toggleFollow(): void;
  toggleTopDown(): void;
  toggleHold(): void;
  moveToShop(): void;
}

/** Aktuell gebundene Taste einer Heldenfähigkeit (Anzeige). */
export const skillKey = (slot: number): string => keyName(boundKey(`skill${slot + 1}`));
export const POTION_KEYS = ['⇧1', '⇧2', '⇧3', '⇧4'];

type WindowKind = '' | 'sheet' | 'shop';
type SheetTab = 'stats' | 'gear' | 'tree' | 'paragon' | 'passive' | 'collection' | 'progress';
type ShopTab = ShopCategory | 'forge' | 'sell';

const costText = (c: Cost): string => {
  const parts: string[] = [];
  if (c.gold) parts.push(`${c.gold} Gold`);
  for (const r of RESOURCE_ORDER) if (c[r]) parts.push(`${c[r]} ${RESOURCES[r].icon}`);
  return parts.join(' · ') || 'kostenlos';
};

export function statValue(k: keyof ChampionStats, v: number): string {
  if (STAT_PCT.has(k)) return `${Math.round(v * 100)} %`;
  if (STAT_MULT.has(k)) return `×${fmtNum(v, 2)}`;
  return fmtNum(v, k === 'attackSpeed' || k === 'range' || k === 'moveSpeed' || k === 'pickupRadius' || k === 'supportRadius' ? 2 : 1);
}

function statDelta(k: keyof ChampionStats, v: number): string {
  const sign = v >= 0 ? '+' : '−';
  const a = Math.abs(v);
  if (STAT_PCT.has(k) || STAT_MULT.has(k)) return `${sign}${Math.round(a * 100)} %`;
  return `${sign}${fmtNum(a, 2)}`;
}

const hex = (n: number): string => `#${n.toString(16).padStart(6, '0')}`;

export class HeroHud {
  readonly dock: HTMLElement;
  readonly resBar: HTMLElement;
  readonly win: HTMLElement;
  private resVals = new Map<string, HTMLElement>();
  private best = h('span', { class: 'v' });
  private profile = h('span', { class: 'he-profile' });
  private portrait: HTMLButtonElement;
  private lvl = h('span', { class: 'hero-lvl' });
  private nameEl = h('div', { class: 'he-name' });
  private hp = { fill: h('div', { class: 'hb-fill' }), text: h('span', { class: 'hb-t' }) };
  private mana = { fill: h('div', { class: 'hb-fill mana' }), text: h('span', { class: 'hb-t' }) };
  private xp = { fill: h('div', { class: 'hb-fill xp' }), text: h('span', { class: 'hb-t' }) };
  private buffs = h('div', { class: 'he-buffs' });
  private buffSig = '';
  private skillBtns: { b: HTMLButtonElement; fill: HTMLElement; cd: HTMLElement }[] = [];
  private potionRow = h('div', { class: 'he-potions', 'data-testid': 'potion-bar' });
  private potionSig = '';
  private sheetBtn: HTMLButtonElement;
  private shopBtn: HTMLButtonElement;
  private followBtn: HTMLButtonElement;
  private holdBtn: HTMLButtonElement;
  private deadNote = h('div', { class: 'he-dead hidden' });
  kind: WindowKind = '';
  private sheetTab: SheetTab = 'gear';
  private shopTab: ShopTab = 'potions';
  private winSig = '';
  private pointerHeld = false;
  private selectedItem = 0;
  /** Gerade gewählter Skill (Zielwahl), für die Hervorhebung. */
  targeting = -1;
  follow = false;

  constructor(private readonly sim: GameSimulation, private readonly a: HeroHudActions, icons: IconFactory) {
    const he = sim.s.he;
    if (!he) throw new Error('HeroHud ohne Helden-Endlos-Zustand');
    const def = CHAMPIONS[he.champion.id];
    this.resBar = h('div', { class: 'he-res', 'data-testid': 'resources' },
      RESOURCE_ORDER.map((r) => {
        const v = h('span', { class: 'v', 'data-testid': `res-${r}` });
        this.resVals.set(r, v);
        return h('div', { class: 'stat res', title: `${RESOURCES[r].name}: ${RESOURCES[r].description}`, style: `--rc:${hex(RESOURCES[r].color)}` }, h('span', { class: 'ic' }, RESOURCES[r].icon), v);
      }),
      h('div', { class: 'stat best', title: 'Bestwelle (dauerhaft gespeichert)' }, h('span', { class: 'ic' }, svgIcon('trophy')), this.best),
      this.profile);
    this.portrait = h('button', { class: 'hero-portrait', title: `${def.name} – Kamera zentrieren`, 'aria-label': `${def.name} auswählen`, onclick: () => a.focusChampion() },
      iconImg(icons.hero(def.model, 96), def.name[0], 'hp-img'), this.lvl);
    const skills = h('div', { class: 'he-skills', 'data-testid': 'skillbar' });
    def.skills.forEach((sk, slot) => {
      const fill = h('span', { class: 'ab-fill' });
      const cd = h('span', { class: 'sk-cd' });
      const b = h('button', {
        class: `sk${slot === 3 ? ' ult' : ''}`, 'data-testid': `skill-${slot}`,
        title: `${sk.name} (${skillKey(slot)}) – ${sk.description}\nAbklingzeit ${sk.cooldown} s · Mana ${sk.mana} · Ziel: ${sk.target === 'self' ? 'selbst' : sk.target === 'tower' ? 'Turm' : 'Bereich'}${sk.unlockLevel > 1 ? ` · ab Stufe ${sk.unlockLevel}` : ''}`,
        'aria-label': `${sk.name}, Taste ${skillKey(slot)}`,
        onclick: () => a.championSkillKey(slot),
      }, fill, svgIcon(sk.icon), h('span', { class: 'sk-k' }, skillKey(slot)), h('span', { class: 'sk-m' }, String(sk.mana)), cd);
      this.skillBtns.push({ b, fill, cd });
      skills.appendChild(b);
    });
    this.sheetBtn = h('button', { class: 'btn sm', 'data-testid': 'open-sheet', title: 'Charakter, Inventar, Skillbaum (Tab)', onclick: () => this.toggle('sheet') }, svgIcon('book'), 'Held');
    this.shopBtn = h('button', { class: 'btn sm', 'data-testid': 'open-shop', title: 'Laden & Schmiede (X) – nur in Reichweite des Ladens', onclick: () => this.toggle('shop') }, svgIcon('coins'), 'Laden');
    this.followBtn = h('button', { class: 'btn sm icon', title: 'Kamera folgt dem Helden (Y)', 'aria-label': 'Kamera folgt dem Helden', 'aria-pressed': 'false', onclick: () => a.toggleFollow() }, svgIcon('advance'));
    this.holdBtn = h('button', { class: 'btn sm icon', 'data-testid': 'hold-position', title: 'Position festsetzen (Z): Klicks bewegen den Helden nicht mehr, er verfolgt keine Gegner', 'aria-label': 'Position festsetzen', 'aria-pressed': 'false', onclick: () => a.toggleHold() }, svgIcon('hold'));
    const topBtn = h('button', { class: 'btn sm icon', title: 'Draufsicht / Isometrie umschalten (V)', 'aria-label': 'Draufsicht umschalten', onclick: () => a.toggleTopDown() }, svgIcon('map'));
    this.dock = h('div', { class: 'he-dock', 'data-testid': 'champion', style: `--hc:${hex(def.accent)}` },
      h('div', { class: 'he-head' }, this.portrait,
        h('div', { class: 'he-bars' }, this.nameEl,
          h('div', { class: 'hb lg', title: 'Lebenspunkte' }, this.hp.fill, this.hp.text),
          h('div', { class: 'hb', title: 'Mana' }, this.mana.fill, this.mana.text),
          h('div', { class: 'hb thin', title: 'Erfahrung' }, this.xp.fill, this.xp.text))),
      this.deadNote,
      this.buffs,
      skills,
      h('div', { class: 'he-foot' }, this.potionRow, h('div', { class: 'he-btns' }, this.sheetBtn, this.shopBtn, this.holdBtn, this.followBtn, topBtn)));
    this.win = h('div', { class: 'he-win hidden', role: 'dialog', 'data-testid': 'he-window' });
    this.win.addEventListener('pointerdown', () => { this.pointerHeld = true; });
    window.addEventListener('pointerup', () => { this.pointerHeld = false; });
    window.addEventListener('pointercancel', () => { this.pointerHeld = false; });
    this.win.addEventListener('pointerdown', (e) => e.stopPropagation());
    this.win.addEventListener('wheel', (e) => e.stopPropagation());
  }

  toggle(kind: Exclude<WindowKind, ''>, tab?: SheetTab | ShopTab): void {
    if (this.kind === kind && !tab) this.kind = '';
    else this.kind = kind;
    if (tab && kind === 'sheet') this.sheetTab = tab as SheetTab;
    if (tab && kind === 'shop') this.shopTab = tab as ShopTab;
    this.winSig = '';
  }

  close(): boolean {
    if (!this.kind) return false;
    this.kind = '';
    this.winSig = '';
    return true;
  }

  refresh(): void {
    this.winSig = '';
    this.potionSig = '';
  }

  // ------------------------------------------------------------ pro Frame

  update(): void {
    const s = this.sim.s;
    const he = s.he;
    if (!he) return;
    const c = he.champion;
    const def = CHAMPIONS[c.id];
    const st = championStats(he);
    for (const r of RESOURCE_ORDER) setText(this.resVals.get(r) as HTMLElement, fmt(he.resources[r] ?? 0));
    setText(this.best, String(Math.max(he.bestWaveAtStart, s.stats.wavesCleared)));
    const prof = WAVE_PROFILES.find((p) => p.id === he.profile);
    setText(this.profile, prof && s.phase !== 'combat' ? `Nächste: ${prof.name}` : '');
    setText(this.lvl, String(c.level));
    setText(this.nameEl, `${def.name} · ${def.role}${c.skillPoints > 0 ? ` · ★ ${c.skillPoints} Punkt${c.skillPoints > 1 ? 'e' : ''}` : ''}`);
    this.hp.fill.style.width = `${Math.max(0, (100 * c.hp) / Math.max(1, st.maxHp))}%`;
    setText(this.hp.text, `${Math.ceil(c.hp)}/${Math.round(st.maxHp)}`);
    this.mana.fill.style.width = `${st.maxMana > 0 ? Math.max(0, (100 * c.mana) / st.maxMana) : 0}%`;
    setText(this.mana.text, `${Math.floor(c.mana)}/${Math.round(st.maxMana)}`);
    const pg = he.paragon;
    if (c.level >= PARAGON.heroMaxLevel && pg) {
      const pneed = paragonXpToNext(pg.level);
      const maxed = pg.level >= PARAGON.maxLevel;
      this.xp.fill.style.width = maxed ? '100%' : `${Math.min(100, (100 * pg.xp) / pneed)}%`;
      setText(this.xp.text, maxed ? `Paragon ${pg.level} (max.)` : `Paragon ${pg.level} · ${fmt(pg.xp)}/${fmt(pneed)}`);
      toggleClass(this.xp.fill, 'paragon', true);
    } else {
      const need = xpToNext(c.level);
      this.xp.fill.style.width = `${Math.min(100, (100 * c.xp) / need)}%`;
      setText(this.xp.text, `EP ${Math.floor(c.xp)}/${need}`);
      toggleClass(this.xp.fill, 'paragon', false);
    }
    toggleClass(this.dock, 'dead', c.dead > 0);
    const mining = he.mining;
    toggleClass(this.deadNote, 'hidden', c.dead <= 0 && !mining);
    if (c.dead > 0) setText(this.deadNote, `Gefallen – Wiederbelebung am Kern in ${Math.ceil(c.dead)} s`);
    else if (mining) setText(this.deadNote, `⛏ Baut ab – noch ${Math.ceil(mining.t)} s (nicht unterbrechbar)`);
    toggleClass(this.sheetBtn, 'glow', c.skillPoints > 0);
    const near = nearShop(this.sim);
    toggleClass(this.shopBtn, 'on', near);
    this.shopBtn.title = near ? 'Laden & Schmiede öffnen (X)' : 'Laden (X): der Held muss beim Laden stehen – Klick zeigt das Angebot';
    toggleClass(this.followBtn, 'on', this.follow);
    toggleClass(this.holdBtn, 'on', !!c.holdPos);
    this.holdBtn.setAttribute('aria-pressed', String(!!c.holdPos));
    toggleClass(this.dock, 'held', !!c.holdPos);
    this.followBtn.setAttribute('aria-pressed', String(this.follow));
    // Buffs
    const bsig = c.buffs.map((b) => `${b.src}:${Math.ceil(b.t)}`).join(',');
    if (bsig !== this.buffSig) {
      this.buffSig = bsig;
      clear(this.buffs);
      for (const b of c.buffs) {
        const name = b.src.startsWith('potion:') ? POTIONS[b.src.slice(7) as PotionId]?.name ?? b.src : def.skills.find((k) => k.id === b.src)?.name ?? b.src;
        const icon = b.src.startsWith('potion:') ? POTIONS[b.src.slice(7) as PotionId]?.icon ?? '✦' : '✦';
        put(this.buffs, h('span', { class: 'chip buff', title: `${name}: ${Object.entries(b.add).map(([k, v]) => `${STAT_NAMES[k as keyof ChampionStats]} ${statDelta(k as keyof ChampionStats, v ?? 0)}`).join(', ')}` }, icon, ' ', `${Math.ceil(b.t)} s`));
      }
    }
    // Skills
    def.skills.forEach((sk, slot) => {
      const e = this.skillBtns[slot];
      const chk = skillState(this.sim, slot);
      const locked = c.level < sk.unlockLevel;
      toggleClass(e.b, 'ready', chk.ok);
      toggleClass(e.b, 'locked', locked);
      toggleClass(e.b, 'nomana', !locked && c.skillCd[slot] <= 0 && c.mana < sk.mana);
      toggleClass(e.b, 'targeting', this.targeting === slot);
      const total = Math.max(0.1, sk.cooldown * (1 - st.cdr));
      const p = c.skillCd[slot] > 0 ? 1 - c.skillCd[slot] / total : 1;
      e.fill.style.height = `${Math.round(Math.max(0, Math.min(1, p)) * 100)}%`;
      setText(e.cd, locked ? `St. ${sk.unlockLevel}` : c.skillCd[slot] > 0 ? `${Math.ceil(c.skillCd[slot])}` : '');
    });
    // Tränke
    const psig = `${c.potions.map((p) => `${p.potion}${p.count}`).join(',')}|${c.specialPotion ? c.specialPotion.potion + c.specialPotion.count : ''}|${Object.entries(c.potionCd).map(([k, v]) => `${k}${Math.ceil(v ?? 0)}`).join(',')}`;
    if (psig !== this.potionSig) {
      this.potionSig = psig;
      clear(this.potionRow);
      const slots: { potion: PotionId; count: number; key: string; special: boolean }[] = c.potions.slice(0, 4).map((p, i) => ({ potion: p.potion, count: p.count, key: POTION_KEYS[i], special: false }));
      if (c.specialPotion) slots.push({ potion: c.specialPotion.potion, count: c.specialPotion.count, key: '⇧5', special: true });
      for (const p of slots) {
        const d = POTIONS[p.potion];
        const ps = potionState(this.sim, p.potion);
        const cd = c.potionCd[p.potion] ?? 0;
        put(this.potionRow, h('button', {
          class: `potion${ps.ok ? ' ready' : ''}${p.special ? ' special' : ''}`, 'data-testid': `potion-${p.potion}`,
          title: `${d.name} (${p.key}) – ${d.description}${p.special ? ' · im Spezialslot' : ''}`, style: `--pc:${hex(d.color)}`,
          onclick: () => this.a.usePotion(p.potion),
        }, h('span', { class: 'p-ic' }, d.icon), h('span', { class: 'p-n' }, `×${p.count}`), h('span', { class: 'p-k' }, p.key), cd > 0 ? h('span', { class: 'p-cd' }, String(Math.ceil(cd))) : null));
      }
      if (!slots.length) put(this.potionRow, h('span', { class: 'muted tiny' }, 'Keine Tränke'));
    }
    this.updateWindow();
  }

  // ------------------------------------------------------------ Fenster

  private updateWindow(): void {
    toggleClass(this.win, 'hidden', this.kind === '');
    if (!this.kind) return;
    const s = this.sim.s;
    const he = s.he;
    if (!he) return;
    const c = he.champion;
    const sig = [this.kind, this.sheetTab, this.shopTab, this.selectedItem, s.gold, JSON.stringify(he.resources), c.level, c.skillPoints, JSON.stringify(c.tree), he.paragon ? `${he.paragon.level}:${he.paragon.rev}:${Math.floor(he.paragon.xp / 500)}` : '', s.phase,
      c.inventory.map((i) => `${i.uid}${i.item}${i.plus}`).join(','), c.equipment.join(','), c.potions.map((p) => p.potion + p.count).join(','),
      c.specialPotion?.potion ?? '', c.specialPotion?.count ?? 0, JSON.stringify(he.fragments), JSON.stringify(he.shopStock), nearShop(this.sim), s.wave, he.runUnlocks.join(',')].join('|');
    if (sig === this.winSig) return;
    // Nicht neu aufbauen, solange im Fenster eine Taste/ein Finger gedrückt ist (sonst gehen Klicks verloren)
    if (this.pointerHeld && this.winSig !== '') return;
    this.winSig = sig;
    clear(this.win);
    const tabs = (list: [string, string][], cur: string, set: (t: string) => void): HTMLElement =>
      h('div', { class: 'seg he-tabs', role: 'tablist' }, list.map(([id, name]) => h('button', { class: `btn sm${id === cur ? ' on' : ''}`, role: 'tab', 'aria-selected': String(id === cur), 'data-testid': `tab-${id}`, onclick: () => { set(id); this.winSig = ''; } }, name)));
    const head = (title: string): HTMLElement => h('div', { class: 'he-win-head' }, h('h2', {}, title),
      h('button', { class: 'btn icon sm', 'aria-label': 'Schließen', title: 'Schließen (Esc)', 'data-testid': 'close-window', onclick: () => this.close() }, svgIcon('close')));
    if (this.kind === 'sheet') {
      const pFree = paragonFree(he);
      put(this.win, head(`${CHAMPIONS[c.id].name} – Stufe ${c.level}/${PARAGON.heroMaxLevel}${he.paragon?.level ? ` · Paragon ${he.paragon.level}` : ''}`),
        tabs([['gear', 'Ausrüstung'], ['stats', 'Werte'], ['tree', `Skillbaum${c.skillPoints ? ` (${c.skillPoints})` : ''}`], ['paragon', `Paragon${pFree ? ` (${pFree})` : ''}`], ['passive', 'Passiv'], ['collection', 'Sammlung'], ['progress', 'Fortschritt']], this.sheetTab, (t) => { this.sheetTab = t as SheetTab; }));
      const body = h('div', { class: 'he-win-body' });
      if (this.sheetTab === 'gear') this.renderGear(body, false);
      else if (this.sheetTab === 'stats') this.renderStats(body);
      else if (this.sheetTab === 'tree') this.renderTree(body);
      else if (this.sheetTab === 'paragon') this.renderParagon(body);
      else if (this.sheetTab === 'collection') this.renderCollection(body);
      else if (this.sheetTab === 'passive') put(body, h('p', { class: 'small muted' }, 'Stand dieses Laufs (beim Start übernommen). Punkte vergibst du im Startbildschirm des Helden-Endlosmodus.'), renderPassiveTree(he.passive ?? {}, he.bestWaveAtStart, false));
      else this.renderProgress(body);
      put(this.win, body);
    } else {
      const near = nearShop(this.sim);
      put(this.win, head('Laden & Schmiede'),
        near ? null : h('div', { class: 'notice warn small' }, 'Der Held ist nicht am Laden. Angebote ansehen ist möglich, Kaufen, Verkaufen und Herstellen nur in Reichweite. ',
          h('button', { class: 'btn sm', 'data-testid': 'walk-shop', onclick: () => this.a.moveToShop() }, 'Zum Laden laufen')),
        tabs([...SHOP_CATEGORIES.map((x): [string, string] => [x.id, x.name]), ['forge', 'Schmiede'], ['sell', 'Verkaufen']], this.shopTab, (t) => { this.shopTab = t as ShopTab; }));
      const body = h('div', { class: 'he-win-body' });
      if (this.shopTab === 'forge') this.renderForge(body);
      else if (this.shopTab === 'sell') this.renderGear(body, true);
      else if (this.shopTab === 'collection') this.renderCollectionShop(body);
      else this.renderOffers(body, this.shopTab);
      put(this.win, body, h('div', { class: 'he-wallet' }, `Gold ${fmt(s.gold)} · `, RESOURCE_ORDER.map((r) => `${RESOURCES[r].icon} ${he.resources[r] ?? 0}  `)));
    }
  }

  private itemCard(inst: ItemInstance, equippedSlot: number): HTMLElement {
    const d = ITEMS[inst.item];
    const r = RARITY[d.rarity];
    return h('button', {
      class: `item${this.selectedItem === inst.uid ? ' on' : ''}${equippedSlot >= 0 ? ' worn' : ''}`, style: `--rc:${r.color}`, 'data-testid': `item-${inst.item}`,
      title: `${d.name}${inst.plus ? ` +${inst.plus}` : ''} (${r.name}) – ${d.description}`,
      onclick: () => { this.selectedItem = this.selectedItem === inst.uid ? 0 : inst.uid; this.winSig = ''; },
    }, h('span', { class: 'i-ic' }, d.icon), h('span', { class: 'i-n' }, d.name), inst.plus ? h('span', { class: 'i-p' }, `+${inst.plus}`) : null);
  }

  private renderGear(body: HTMLElement, selling: boolean): void {
    const he = this.sim.s.he;
    if (!he) return;
    const c = he.champion;
    const byUid = new Map(c.inventory.map((i) => [i.uid, i]));
    if (!selling) {
      put(body, h('h3', {}, 'Ausrüstung'), h('div', { class: 'slots' }, SLOTS.map((sl, i) => {
        const uid = c.equipment[i];
        const inst = uid !== null ? byUid.get(uid) : undefined;
        if (i === 5 && c.specialPotion) {
          const p = POTIONS[c.specialPotion.potion];
          return h('div', { class: 'slot filled' }, h('span', { class: 'sl-n' }, sl.name),
            h('button', { class: 'item worn', title: `${p.name} ×${c.specialPotion.count} – Klick legt ihn zurück`, onclick: () => this.a.unequip(5) }, h('span', { class: 'i-ic' }, p.icon), h('span', { class: 'i-n' }, `${p.name} ×${c.specialPotion.count}`)));
        }
        return h('div', { class: `slot${inst ? ' filled' : ''}`, 'data-testid': `slot-${sl.slot}` }, h('span', { class: 'sl-n' }, `${sl.icon} ${sl.name}`),
          inst ? this.itemCard(inst, i) : h('span', { class: 'muted tiny' }, 'leer'));
      })));
    }
    const list = selling ? c.inventory : c.inventory;
    put(body, h('h3', {}, `Inventar ${c.inventory.length}/${HERO_ENDLESS.inventorySlots + 6}`),
      list.length ? h('div', { class: 'items' }, list.map((i) => this.itemCard(i, c.equipment.indexOf(i.uid)))) : h('p', { class: 'muted small' }, 'Noch keine Gegenstände. Gegner lassen Fragmente und seltene Items fallen; Fragmente werden in der Schmiede zu Items.'));
    const sel = byUid.get(this.selectedItem);
    if (sel) put(body, this.itemDetail(sel, selling));
    if (!selling) {
      put(body, h('h3', {}, `Tränke (${c.potions.length}/${HERO_ENDLESS.potionStacks} Stapel)`),
        c.potions.length ? h('div', { class: 'items' }, c.potions.map((p) => {
          const d = POTIONS[p.potion];
          return h('div', { class: 'item potion-item', style: `--rc:${hex(d.color)}`, title: d.description },
            h('span', { class: 'i-ic' }, d.icon), h('span', { class: 'i-n' }, `${d.name} ×${p.count}/${d.stack}`),
            h('button', { class: 'btn sm', onclick: () => this.a.usePotion(p.potion) }, 'Nutzen'),
            h('button', { class: 'btn sm', title: 'In den Spezialslot legen (Schnellzugriff ⇧5)', disabled: c.equipment[5] !== null, onclick: () => this.a.slotPotion(p.potion) }, 'Spezialslot'));
        })) : h('p', { class: 'muted small' }, 'Keine Tränke.'));
    }
  }

  private itemDetail(inst: ItemInstance, selling: boolean): HTMLElement {
    const he = this.sim.s.he;
    if (!he) return h('div');
    const c = he.champion;
    const d = ITEMS[inst.item];
    const slotIdx = SLOTS.findIndex((s) => s.slot === d.slot);
    const worn = c.equipment.indexOf(inst.uid);
    const otherUid = c.equipment[slotIdx];
    const other = otherUid !== null && otherUid !== inst.uid ? c.inventory.find((i) => i.uid === otherUid) : undefined;
    const mine = itemStats(inst.item, inst.plus);
    const theirs = other ? itemStats(other.item, other.plus) : {};
    const keys = [...new Set([...Object.keys(mine), ...Object.keys(theirs)])] as (keyof ChampionStats)[];
    const allowed = !d.champions || d.champions.includes(c.id);
    const up = upgradeState(this.sim, inst.uid);
    return h('div', { class: 'item-detail', 'data-testid': 'item-detail', style: `--rc:${RARITY[d.rarity].color}` },
      h('h4', {}, `${d.icon} ${d.name}${inst.plus ? ` +${inst.plus}` : ''}`, h('span', { class: 'rar' }, ` ${RARITY[d.rarity].name} · ${SLOTS[slotIdx].name}`)),
      h('p', { class: 'small muted' }, d.description, d.champions ? ` Nur für: ${d.champions.map((x) => CHAMPIONS[x].name).join(', ')}.` : ''),
      h('table', { class: 'stats' }, keys.map((k) => {
        const delta = (mine[k] ?? 0) - (theirs[k] ?? 0);
        return h('tr', {}, h('td', {}, STAT_NAMES[k]), h('td', {}, statDelta(k, mine[k] ?? 0)),
          other ? h('td', { class: delta > 0 ? 'good' : delta < 0 ? 'bad' : 'muted' }, delta === 0 ? '±0' : statDelta(k, delta)) : null);
      })),
      other ? h('p', { class: 'tiny muted' }, `Vergleich mit angelegtem ${ITEMS[other.item].name}`) : null,
      h('div', { class: 'row wrap' },
        selling
          ? h('button', { class: 'btn danger', 'data-testid': 'sell-item', onclick: () => { this.a.shopSell(inst.uid); this.selectedItem = 0; } }, `Verkaufen (${costText(sellValue(inst.item, inst.plus))})`)
          : worn >= 0
            ? h('button', { class: 'btn', 'data-testid': 'unequip', onclick: () => this.a.unequip(worn) }, 'Ablegen')
            : h('button', { class: 'btn primary', 'data-testid': 'equip', disabled: !allowed, title: allowed ? '' : 'Dieser Held kann das Item nicht tragen', onclick: () => this.a.equip(inst.uid) }, 'Anlegen'),
        inst.plus < MAX_PLUS ? h('button', { class: 'btn', 'data-testid': 'upgrade-item', disabled: !up.ok, title: up.reason ?? '', onclick: () => this.a.upgradeItem(inst.uid) }, `Verstärken +${inst.plus + 1} (${costText(up.cost)})`) : null));
  }

  private renderStats(body: HTMLElement): void {
    const he = this.sim.s.he;
    if (!he) return;
    const st = championStats(he);
    const c = he.champion;
    put(body, h('table', { class: 'stats' }, (Object.keys(STAT_NAMES) as (keyof ChampionStats)[]).map((k) => h('tr', {}, h('td', {}, STAT_NAMES[k]), h('td', {}, statValue(k, st[k]))))),
      h('p', { class: 'small muted' }, `Abschüsse ${c.kills} · Schaden ${fmt(c.damageDealt)} · EP gesammelt ${fmt(he.stats.xpCollected)} · Tränke ${he.stats.potionsUsed} · hergestellt ${he.stats.itemsCrafted}`));
  }

  private renderTree(body: HTMLElement): void {
    const he = this.sim.s.he;
    if (!he) return;
    const c = he.champion;
    const tree = CHAMPIONS[c.id].tree;
    const spentTree = tree.reduce((a, n) => a + (c.tree[n.id] ?? 0), 0);
    put(body, h('div', { class: 'row tight', style: 'justify-content:space-between;align-items:center' },
      h('p', { class: 'small' }, `Fertigkeitspunkte: `, h('b', { 'data-testid': 'skill-points' }, String(c.skillPoints)), ` · 1 Punkt je Stufe, Bonuspunkt alle ${HERO_ENDLESS.bonusPointEvery} Stufen.`),
      spentTree > 0 ? h('button', { class: 'btn sm', 'data-testid': 'tree-reset', title: `Alle ${spentTree} vergebenen Punkte kostenlos zurückgeben – jederzeit möglich, wird dauerhaft gespeichert`, onclick: () => this.a.treeReset() }, '↺ Skillbaum zurücksetzen') : null));
    const tiers = [...new Set(tree.map((n) => n.tier))].sort((x, y) => x - y);
    for (const tier of tiers) {
      put(body, h('h3', {}, `Stufe ${tier}`), h('div', { class: 'tree-row' }, tree.filter((n) => n.tier === tier).map((n) => this.nodeCard(n))));
    }
  }

  private renderParagon(body: HTMLElement): void {
    const he = this.sim.s.he;
    if (!he) return;
    const c = he.champion;
    const p = he.paragon ?? { level: 0, xp: 0, points: {}, rev: 0 };
    const free = paragonFree(he);
    const fx = paragonEffects(p.points);
    const lines = effectLines(fx);
    put(body,
      h('p', { class: 'small' }, `Paragon `, h('b', { 'data-testid': 'paragon-level' }, `${p.level}/${PARAGON.maxLevel}`), ` · frei `, h('b', { 'data-testid': 'paragon-free' }, String(free)),
        ` · gemeinsam für alle Helden, dauerhaft. `, `Punkte: ${p.level} aus Paragonstufen (ab Heldenstufe ${PARAGON.heroMaxLevel}) + ${paragonPrePoints(c.level)} Vorab-Punkte (ab Stufe 50 alle 5 Stufen, jetzt Stufe ${c.level}) + ${he.paragonBonus ?? 0} aus Meilensteinen (W100/150/200/250/300).`),
      lines.length ? h('p', { class: 'tiny muted' }, `Wirkung: ${lines.join(' · ')}`) : null,
      h('div', { class: 'paragon-trees' }, PARAGON_TREES.map((t) => {
        const nodes = PARAGON_NODES.filter((n) => n.tree === t.id);
        const spent = nodes.reduce((a, n) => a + (p.points[n.id] ?? 0), 0);
        return h('div', { class: 'pg-tree', style: `--pc:${t.color}`, 'data-testid': `pg-tree-${t.id}` },
          h('h3', {}, `${t.icon} ${t.name}`, h('span', { class: 'tiny muted' }, ` ${spent} Punkte`)),
          nodes.map((n) => {
            const r = p.points[n.id] ?? 0;
            const why = paragonRaiseReason(he, n.id);
            return h('button', {
              class: `node pg${r > 0 ? ' has' : ''}${r >= n.maxRank ? ' max' : ''}${!why ? ' can' : ''}`, 'data-testid': `pg-${n.id}`, disabled: !!why,
              title: `${n.description} je Rang${why && r < n.maxRank ? `\n${why}` : ''}`, onclick: () => this.a.paragonPoint(n.id),
            }, h('b', {}, `${n.icon} ${n.name}`), h('span', { class: 'rank' }, `${r}/${n.maxRank}`), h('span', { class: 'tiny' }, `${n.description} je Rang`));
          }));
      })),
      paragonSpent(p.points) > 0 ? h('button', { class: 'btn sm', 'data-testid': 'paragon-reset', title: 'Alle Paragonpunkte kostenlos zurückgeben – jederzeit möglich', onclick: () => this.a.paragonReset() }, '↺ Paragon zurücksetzen') : null);
  }

  private nodeCard(n: SkillNode): HTMLElement {
    const c = this.sim.s.he?.champion;
    const rank = c?.tree[n.id] ?? 0;
    const chk = nodeState(this.sim, n);
    const req = (n.requires ?? []).map((r) => `${CHAMPIONS[c?.id ?? 'lyra'].tree.find((x) => x.id === r.id)?.name ?? r.id} ${r.rank}`).join(', ');
    return h('button', {
      class: `node${rank > 0 ? ' has' : ''}${rank >= n.maxRank ? ' max' : ''}${chk.ok ? ' can' : ''}`, 'data-testid': `node-${n.id}`,
      title: `${n.description}${req ? `\nBenötigt: ${req}` : ''}${n.minLevel ? `\nAb Stufe ${n.minLevel}` : ''}${chk.reason && rank < n.maxRank ? `\n${chk.reason}` : ''}`,
      disabled: !chk.ok, onclick: () => this.a.learnSkill(n.id),
    }, h('b', {}, n.name), h('span', { class: 'rank' }, `${rank}/${n.maxRank}`), h('span', { class: 'tiny' }, n.description), n.perk ? h('span', { class: 'tag' }, '★ Meister') : null);
  }

  private renderCollection(body: HTMLElement): void {
    const he = this.sim.s.he;
    if (!he) return;
    const owned = new Set([...he.metaOwned, ...he.runUnlocks]);
    put(body, h('p', { class: 'small muted' }, 'Dauerhafte Sammlung: einmal freigeschaltete Items bleiben erhalten, werden nie erneut als identischer Drop vergeben und können im Laden für Ressourcen in einen Lauf geholt werden. Neue Freischaltungen dieser Partie (✦) werden bei Partieende gespeichert.'),
      h('div', { class: 'items coll' }, Object.values(ITEMS).filter((d) => !d.champions || d.champions.includes(he.champion.id) || owned.has(d.id)).map((d) => {
        const has = owned.has(d.id);
        const frag = he.fragments[d.id] ?? 0;
        const need = RARITY[d.rarity].fragments;
        return h('div', { class: `item${has ? '' : ' locked'}`, style: `--rc:${RARITY[d.rarity].color}`, title: `${d.name} (${RARITY[d.rarity].name}) – ${d.description}${d.craftOnly ? ' · nur durch Kombination' : ''}`, 'data-testid': `coll-${d.id}` },
          h('span', { class: 'i-ic' }, has ? d.icon : '?'), h('span', { class: 'i-n' }, d.name),
          has ? (he.runUnlocks.includes(d.id) ? h('span', { class: 'i-p' }, '✦ neu') : null) : d.craftOnly ? h('span', { class: 'i-p' }, 'Rezept') : h('span', { class: 'i-p' }, `${frag}/${need}`));
      })));
  }

  private renderProgress(body: HTMLElement): void {
    const he = this.sim.s.he;
    if (!he) return;
    const best = he.bestWaveAtStart;
    put(body, h('p', { class: 'small' }, `Bestwelle bei Partiebeginn: `, h('b', {}, String(best)), `. Perks gelten ab dem nächsten Lauf, sobald die Welle erreicht wurde.`),
      h('h3', {}, 'Perks'),
      h('div', { class: 'perk-list' }, PERKS.map((p) => h('div', { class: `perk-row${he.perks.includes(p.id) ? ' on' : ''}` }, h('span', { class: 'p-ic' }, p.icon), h('b', {}, p.name), h('span', { class: 'small muted' }, ` Welle ${p.wave}: ${p.description}`)))),
      h('h3', {}, 'Meilensteine (einmalig)'),
      h('div', { class: 'perk-list' }, MILESTONES.map((m) => h('div', { class: `perk-row${best >= m.wave ? ' on' : ''}` }, h('b', {}, `W${m.wave} ${m.name}`), h('span', { class: 'small muted' }, ` ${m.description}`)))));
  }

  private renderOffers(body: HTMLElement, cat: ShopCategory): void {
    const he = this.sim.s.he;
    if (!he) return;
    const offers = SHOP_OFFERS.filter((o) => o.category === cat);
    put(body, cat === 'fragments' || cat === 'materials' || cat === 'special' ? h('p', { class: 'tiny muted' }, `Begrenzter Bestand wird alle ${HERO_ENDLESS.stockEvery} Wellen aufgefüllt.`) : null,
      h('div', { class: 'offers' }, offers.map((o) => {
        const chk = offerState(this.sim, o);
        return h('div', { class: `offer${chk.ok ? '' : ' no'}` },
          h('div', {}, h('b', {}, o.name), chk.left !== null ? h('span', { class: 'tiny muted' }, ` · Bestand ${chk.left}`) : null, h('div', { class: 'small muted' }, o.description)),
          h('button', { class: 'btn sm primary', 'data-testid': `buy-${o.id}`, disabled: !chk.ok, title: chk.reason ?? '', onclick: () => this.a.shopBuy(o.id) }, costText(o.cost)),
          !chk.ok && chk.reason ? h('div', { class: 'tiny bad' }, chk.reason) : null);
      })));
  }

  private renderCollectionShop(body: HTMLElement): void {
    const he = this.sim.s.he;
    if (!he) return;
    const items = collectionOffers(he);
    put(body, h('p', { class: 'small muted' }, 'Freigeschaltete Items aus der Sammlung können hier für Ressourcen in den aktuellen Lauf geholt werden (jedes Item einmal pro Lauf). Rucksack-Perks erlauben kostenlose Mitnahme beim Start.'),
      items.length ? h('div', { class: 'offers' }, items.map((id) => {
        const d = ITEMS[id];
        const chk = collectionState(this.sim, id);
        return h('div', { class: `offer${chk.ok ? '' : ' no'}`, style: `--rc:${RARITY[d.rarity].color}` },
          h('div', {}, h('b', {}, `${d.icon} ${d.name}`), h('span', { class: 'tiny', style: `color:${RARITY[d.rarity].color}` }, ` ${RARITY[d.rarity].name}`), h('div', { class: 'small muted' }, d.description)),
          h('button', { class: 'btn sm primary', 'data-testid': `buyitem-${id}`, disabled: !chk.ok, title: chk.reason ?? '', onclick: () => this.a.shopBuyItem(id) }, costText(chk.cost)),
          !chk.ok && chk.reason ? h('div', { class: 'tiny bad' }, chk.reason) : null);
      })) : h('p', { class: 'muted' }, 'Noch keine Items freigeschaltet.'));
  }

  private renderForge(body: HTMLElement): void {
    const he = this.sim.s.he;
    if (!he) return;
    const frag = RECIPES.filter((r) => r.kind === 'fragments' && r.fragments && (he.fragments[r.fragments.item] ?? 0) > 0);
    const combine = RECIPES.filter((r) => r.kind === 'combine');
    const heroRec = RECIPES.filter((r) => r.kind === 'hero' && r.champion === he.champion.id);
    const row = (r: (typeof RECIPES)[number]): HTMLElement => {
      const chk = recipeState(this.sim, r);
      const d = r.result ? ITEMS[r.result] : null;
      const parts = [r.fragments ? `${chk.have}/${r.fragments.count} Fragmente` : '', ...(r.items ?? []).map((i) => ITEMS[i].name), costText(r.cost)].filter(Boolean).join(' + ');
      return h('div', { class: `offer${chk.ok ? '' : ' no'}`, style: d ? `--rc:${RARITY[d.rarity].color}` : '' },
        h('div', {}, h('b', {}, `${d?.icon ?? ''} ${r.name}`), d && r.kind === 'hero' ? h('span', { class: 'tiny', style: `color:${RARITY[d.rarity].color}` }, ` ${RARITY[d.rarity].name} · ${SLOTS.find((x) => x.slot === d.slot)?.name ?? ''}`) : null, h('div', { class: 'small muted' }, parts), d && r.kind === 'hero' ? h('div', { class: 'tiny' }, `${d.description} ${Object.entries(d.stats).map(([k, v]) => `${STAT_NAMES[k as keyof ChampionStats]} ${statDelta(k as keyof ChampionStats, v ?? 0)}`).join(', ')}`) : null),
        h('button', { class: 'btn sm primary', 'data-testid': `craft-${r.id}`, disabled: !chk.ok, title: chk.reason ?? '', onclick: () => this.a.craft(r.id) }, 'Herstellen'),
        !chk.ok && chk.reason ? h('div', { class: 'tiny bad' }, chk.reason) : null);
    };
    put(body, h('h3', {}, `Heldenschmiede – ${CHAMPIONS[he.champion.id].name}`),
      h('p', { class: 'small muted' }, 'Signaturgegenstände nur für diesen Helden: der epische ab Welle 8 aus Ressourcen, die legendäre Waffe ab Welle 20 aus dem epischen Gegenstand.'),
      h('div', { class: 'offers', 'data-testid': 'hero-forge' }, heroRec.map(row)),
      h('h3', {}, 'Fragmente zusammensetzen'),
      frag.length ? h('div', { class: 'offers' }, frag.map(row)) : h('p', { class: 'muted small' }, 'Keine Fragmente vorhanden.'),
      h('h3', {}, 'Kombinationen (legendär)'), h('div', { class: 'offers' }, combine.map(row)),
      h('h3', {}, 'Verstärken'), h('p', { class: 'small muted' }, `Items im Tab „Verkaufen“ oder im Charakterbogen auswählen und verstärken (bis +${MAX_PLUS}, je Stufe +15 % Werte).`));
  }
}
