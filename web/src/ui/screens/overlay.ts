import type { GameApplication } from '../../app/GameApplication.ts';
import { h } from '../dom.ts';
import { helpContent } from './help.ts';
import { settingsContent } from './settings.ts';

/** Zeigt Hilfe oder Einstellungen als Overlay über der pausierten Partie. */
export function openOverlay(app: GameApplication, kind: 'help' | 'settings', onClose: () => void): void {
  document.querySelector('.overlay-screen')?.remove();
  const close = (): void => {
    el.remove();
    window.removeEventListener('keydown', onKey, true);
    onClose();
  };
  const onKey = (e: KeyboardEvent): void => {
    if (e.key === 'Escape') {
      e.stopPropagation();
      e.preventDefault();
      close();
    }
  };
  const el = h('div', { class: 'overlay-screen', role: 'dialog', 'aria-label': kind === 'help' ? 'Hilfe' : 'Einstellungen' },
    h('div', { class: 'screen-inner' },
      h('div', { class: 'screen-head' },
        h('button', { class: 'btn icon', 'aria-label': 'Schließen', 'data-testid': 'overlay-close', onclick: close }, '✕'),
        h('h1', {}, kind === 'help' ? 'Hilfe & Kodex' : 'Einstellungen')),
      kind === 'help' ? helpContent('controls', app.icons) : settingsContent(app, true)));
  window.addEventListener('keydown', onKey, true);
  document.body.appendChild(el);
  (el.querySelector('button') as HTMLElement | null)?.focus();
}
