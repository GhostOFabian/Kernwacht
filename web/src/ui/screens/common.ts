import type { GameApplication, ScreenId } from '../../app/GameApplication.ts';
import { CAMPAIGN } from '../../config/campaign.ts';
import { HERO_ORDER, HERO_RULES, HEROES, isHeroUnlocked } from '../../config/heroes.ts';
import type { HeroId } from '../../core/types.ts';
import { iconImg } from '../../render/IconFactory.ts';
import { h } from '../dom.ts';
import { svgIcon } from '../icons.ts';

export function shell(app: GameApplication, title: string, back: ScreenId | null, ...content: (HTMLElement | null)[]): HTMLElement {
  const el = h('section', { class: 'screen', role: 'region', 'aria-label': title },
    h('div', { class: 'screen-inner' },
      h('div', { class: 'screen-head' },
        back ? h('button', { class: 'btn icon', 'aria-label': 'Zurück', title: 'Zurück (Esc)', 'data-testid': 'back', onclick: () => { app.audio.play('ui'); app.go(back); } }, svgIcon('back')) : null,
        h('h1', {}, title)),
      ...content));
  if (back) {
    el.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') app.go(back);
    });
  }
  return el;
}

export function seg<T extends string>(options: [T, string][], value: T, onChange: (v: T) => void, label: string): HTMLElement {
  const root = h('div', { class: 'seg', role: 'radiogroup', 'aria-label': label });
  const render = (cur: T): void => {
    root.replaceChildren(...options.map(([v, text]) => h('button', {
      class: `btn sm${v === cur ? ' on' : ''}`, role: 'radio', 'aria-checked': String(v === cur), 'data-value': v,
      onclick: () => { render(v); onChange(v); },
    }, text)));
  };
  render(value);
  return root;
}

export async function copyText(app: GameApplication, text: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(text);
    app.toast(`Kopiert: ${text.length > 40 ? text.slice(0, 40) + '…' : text}`, 'info', 1800);
  } catch {
    // Zwischenablage nicht verfügbar: Text gut lesbar anzeigen, damit er abgeschrieben werden kann.
    app.toast(`Kopieren nicht möglich – ${text}`, 'warn', 8000);
  }
}

/**
 * Heldenauswahl vor der Partie (bis zu zwei Helden). forced: von der Mission vorgegebene Helden.
 * Ändert app.save.settings.lastHeroes und ruft onChange mit der aktuellen Auswahl auf.
 */
export function heroLoadout(app: GameApplication, forced: HeroId[] | null, onChange: (ids: HeroId[]) => void): HTMLElement {
  const done = app.save.progress.campaignCompleted;
  const unlocked = (id: HeroId): boolean => isHeroUnlocked(id, done);
  let picked: HeroId[] = forced ? [...forced] : app.save.settings.lastHeroes.filter(unlocked).slice(0, HERO_RULES.loadoutSize);
  const root = h('div', { class: 'loadout', role: 'group', 'aria-label': 'Heldenauswahl', 'data-testid': 'loadout' });
  const render = (): void => {
    root.replaceChildren(...HERO_ORDER.map((id) => {
      const d = HEROES[id];
      const ok = unlocked(id);
      const idx = picked.indexOf(id);
      const mission = CAMPAIGN.find((m) => m.id === d.unlock);
      return h('button', {
        class: `hero-pick${idx >= 0 ? ' on' : ''}${ok ? '' : ' locked'}${forced?.includes(id) ? ' forced' : ''}`,
        'data-testid': `pick-${id}`, 'aria-pressed': String(idx >= 0), disabled: !ok || !!forced,
        title: ok ? `${d.name} – ${d.description}` : `Gesperrt: schließe Mission „${mission?.name ?? d.unlock}“ ab`,
        onclick: () => {
          if (forced || !ok) return;
          if (idx >= 0) picked.splice(idx, 1);
          else {
            picked.push(id);
            if (picked.length > HERO_RULES.loadoutSize) picked = picked.slice(-HERO_RULES.loadoutSize);
          }
          app.save.settings.lastHeroes = [...picked];
          app.persist();
          app.audio.play('select');
          render();
          onChange([...picked]);
        },
      },
      idx >= 0 ? h('span', { class: 'slot' }, idx === 0 ? 'Z' : 'X') : null,
      iconImg(app.icons.hero(id, 96), d.icon),
      h('b', {}, d.name.split(' ').slice(-1)[0]),
      h('span', { class: 'role' }, ok ? `${d.role}${d.support ? ' · Unterstützer' : ''}` : `🔒 ${mission ? `Mission ${mission.index + 1}` : 'gesperrt'}`));
    }));
  };
  render();
  onChange([...picked]);
  return root;
}
