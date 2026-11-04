import type { GameApplication, ScreenId } from '../../app/GameApplication.ts';
import type { PlayerSettings } from '../../core/types.ts';
import { ACHIEVEMENTS, SKINS, skinUnlocked } from '../../config/achievements.ts';
import { iconImg } from '../../render/IconFactory.ts';
import { defaultProgress } from '../../save/SaveSystem.ts';
import { h } from '../dom.ts';
import { KEY_ACTIONS, keyName, rebind, RESERVED_KEYS } from '../../config/keys.ts';
import { seg, shell } from './common.ts';

/** Einstellungsinhalte (auch als Overlay im Spiel nutzbar). */
export function settingsContent(app: GameApplication, inGame: boolean): HTMLElement {
  const s = app.save.settings;
  const commit = (): void => {
    app.applySettings();
    app.persist();
  };
  const slider = (label: string, key: 'masterVolume' | 'musicVolume' | 'sfxVolume'): HTMLElement => {
    const out = h('output', {}, `${Math.round(s[key] * 100)} %`);
    return h('label', { class: 'field' }, h('span', {}, label),
      h('div', { class: 'row tight' },
        h('input', {
          type: 'range', min: '0', max: '100', value: String(Math.round(s[key] * 100)), 'aria-label': label,
          oninput: (e: Event) => {
            s[key] = Number((e.target as HTMLInputElement).value) / 100;
            out.textContent = `${Math.round(s[key] * 100)} %`;
            commit();
          },
        }), out));
  };
  const check = (label: string, key: 'muted' | 'reducedMotion' | 'showGrid' | 'showRanges', hint?: string): HTMLElement =>
    h('label', { class: 'check' },
      h('input', { type: 'checkbox', checked: s[key], onchange: (e: Event) => { s[key] = (e.target as HTMLInputElement).checked; commit(); } }),
      h('span', {}, label, hint ? h('span', { class: 'muted small' }, ` – ${hint}`) : null));
  const resetBtn = h('button', { class: 'btn danger', onclick: () => {
    if (resetBtn.dataset.armed) {
      app.save.progress = defaultProgress();
      app.save.resume = null;
      app.persist();
      app.toast('Fortschritt wurde zurückgesetzt.', 'info');
      delete resetBtn.dataset.armed;
      resetBtn.textContent = 'Fortschritt zurücksetzen';
    } else {
      resetBtn.dataset.armed = '1';
      resetBtn.textContent = 'Wirklich alles löschen? Erneut tippen';
    }
  } }, 'Fortschritt zurücksetzen');
  return h('div', { class: 'settings' },
    h('h2', {}, 'Audio'),
    slider('Gesamtlautstärke', 'masterVolume'),
    slider('Musik', 'musicVolume'),
    slider('Effekte', 'sfxVolume'),
    check('Stumm', 'muted'),
    h('h2', {}, 'Darstellung & Bedienung'),
    check('Bewegung reduzieren', 'reducedMotion', 'kein Kamerawackeln, ruhigere Effekte'),
    check('Bauraster immer anzeigen', 'showGrid'),
    check('Alle Turmreichweiten anzeigen', 'showRanges'),
    h('label', { class: 'check' },
      h('input', { type: 'checkbox', 'data-testid': 'set-cinematic', checked: s.cinematicFx !== false, onchange: (e: Event) => { s.cinematicFx = (e.target as HTMLInputElement).checked; commit(); } }),
      h('span', {}, 'Kino-Effekte', h('span', { class: 'muted small' }, ' – Tiefenunschärfe am Bildrand, Filmkorn, Sonnendunst (ab Qualität Mittel)'))),
    h('div', { class: 'field' }, h('span', {}, 'UI-Größe'),
      seg<string>([['0.85', 'Klein'], ['1', 'Normal'], ['1.15', 'Groß'], ['1.3', 'Sehr groß']], String(s.uiScale), (v) => { s.uiScale = Number(v); commit(); }, 'UI-Größe')),
    h('div', { class: 'field' }, h('span', {}, 'Grafikqualität'),
      seg<PlayerSettings['quality']>([['auto', 'Automatisch'], ['low', 'Niedrig'], ['medium', 'Mittel'], ['high', 'Hoch']], s.quality, (v) => { s.quality = v; commit(); app.resize(); }, 'Grafikqualität')),
    h('p', { class: 'muted small' }, 'Automatisch passt die Auflösung an, wenn die Bildrate dauerhaft sinkt. Niedrig schaltet Schatten, Kantenglättung, Glühen und Bodenbewuchs ab.'),
    h('h2', {}, 'Tastenbelegung'),
    keyBinder(app),
    h('h2', {}, 'Turm-Skin'),
    skinPicker(app),
    inGame ? null : h('h2', {}, 'Daten'),
    inGame ? null : h('div', { class: 'row' },
      h('button', { class: 'btn', onclick: () => { s.tutorialSeen = false; commit(); app.toast('Die Einführung wird in der nächsten Partie erneut gezeigt.'); } }, 'Einführung erneut zeigen'),
      resetBtn),
  );
}

function keyBinder(app: GameApplication): HTMLElement {
  const root = h('div', { class: 'keybinds' });
  let capturing: string | null = null;
  let stop: (() => void) | null = null;
  const render = (): void => {
    const s = app.save.settings;
    const groups = [...new Set(KEY_ACTIONS.map((a) => a.group))];
    root.replaceChildren(
      h('p', { class: 'muted small' }, 'Aktion anklicken und danach die gewünschte Taste drücken (Esc bricht ab). Ist die Taste schon vergeben, tauschen beide Aktionen ihre Tasten. Esc bleibt fest für Abbrechen/Pause, Umschalt+1–5 für Tränke.'),
      ...groups.map((g) => h('details', { class: 'kb-group', open: g === 'Allgemein' },
        h('summary', {}, g),
        h('div', { class: 'kb-grid' }, ...KEY_ACTIONS.filter((a) => a.group === g).map((a) => {
          const k = s.keys[a.id] ?? a.def;
          const changed = s.keys[a.id] !== undefined;
          return h('div', { class: 'kb-row' },
            h('span', {}, a.name),
            h('button', {
              class: `btn small kb-key${capturing === a.id ? ' on' : ''}${changed ? ' changed' : ''}`, 'data-testid': `key-${a.id}`,
              title: changed ? `Standard: ${keyName(a.def)}` : 'Klicken zum Ändern',
              onclick: () => start(a.id),
            }, capturing === a.id ? 'Taste drücken …' : keyName(k)));
        })))),
      h('div', { class: 'row' }, h('button', { class: 'btn', 'data-testid': 'keys-reset', onclick: () => {
        s.keys = {};
        app.applySettings();
        app.persist();
        app.toast('Tastenbelegung auf Standard zurückgesetzt.');
        render();
      } }, 'Standard wiederherstellen')));
  };
  const start = (id: string): void => {
    stop?.();
    capturing = id;
    const onKey = (e: KeyboardEvent): void => {
      e.preventDefault();
      e.stopImmediatePropagation();
      if (['shift', 'control', 'alt', 'meta'].includes(e.key.toLowerCase())) return;
      const k = e.key.toLowerCase();
      const s = app.save.settings;
      if (!RESERVED_KEYS.has(k)) {
        const r = rebind(id, k, s.keys);
        s.keys = r.map;
        app.applySettings();
        app.persist();
        if (r.swapped) {
          const other = KEY_ACTIONS.find((a) => a.id === r.swapped);
          app.toast(`Getauscht: „${other?.name}“ liegt jetzt auf ${keyName(s.keys[r.swapped] ?? other?.def ?? '')}.`);
        }
      }
      stop?.();
    };
    window.addEventListener('keydown', onKey, true);
    stop = () => {
      window.removeEventListener('keydown', onKey, true);
      capturing = null;
      stop = null;
      render();
    };
    render();
  };
  render();
  return root;
}

function skinPicker(app: GameApplication): HTMLElement {
  const root = h('div', { class: 'skins', role: 'radiogroup', 'aria-label': 'Turm-Skin' });
  const render = (): void => {
    const s = app.save.settings;
    root.replaceChildren(...SKINS.map((k) => {
      const ok = skinUnlocked(k, app.save.progress.achievements);
      const req = ACHIEVEMENTS.find((a) => a.id === k.requires);
      const hex = (n: number): string => `#${n.toString(16).padStart(6, '0')}`;
      return h('button', {
        class: `skin${s.skin === k.id ? ' on' : ''}${ok ? '' : ' locked'}`, role: 'radio', 'aria-checked': String(s.skin === k.id), disabled: !ok, 'data-testid': `skin-${k.id}`,
        title: ok ? k.description : `Gesperrt – Erfolg „${req?.name ?? k.requires}“: ${req?.description ?? ''}`,
        onclick: () => {
          if (!ok) return;
          s.skin = k.id;
          app.applySettings();
          app.persist();
          render();
        },
      }, h('span', { class: 'sw', style: `background: linear-gradient(135deg, ${hex(k.stone)} 0 50%, ${k.tintAmount > 0 ? hex(k.tint) : '#b77bff'} 50% 100%)` }),
      ok && s.skin === k.id ? iconImg(app.icons.tower('ember', 64, 3, 1), '', 'codex-ic') : null,
      h('b', {}, k.name), h('span', { class: 'tiny muted' }, ok ? k.description : `🔒 ${req?.name ?? ''}`));
    }));
  };
  render();
  return root;
}

export function renderSettings(app: GameApplication, back: ScreenId): HTMLElement {
  return shell(app, 'Einstellungen', back, settingsContent(app, false));
}
