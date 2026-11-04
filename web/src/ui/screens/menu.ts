import type { GameApplication } from '../../app/GameApplication.ts';
import { MODE_NAMES } from '../text.ts';
import { h } from '../dom.ts';
import { svgIcon } from '../icons.ts';


export function renderMenu(app: GameApplication): HTMLElement {
  const resume = app.canResume();
  const st = resume.state;
  const go = (fn: () => void) => () => {
    app.audio.play('ui');
    fn();
  };
  const el = h('section', { class: 'screen menu', 'data-testid': 'menu' },
    h('div', { class: 'menu-box' },
      h('div', { class: 'logo' }, h('span', { class: 'logo-main' }, 'KERNWACHT'), h('span', { class: 'logo-sub' }, 'Welten unter Belagerung')),
      app.loadError ? h('div', { class: 'notice warn', role: 'alert' }, app.loadError) : null,
      h('nav', { class: 'menu-buttons', 'aria-label': 'Hauptmenü' },
        st ? h('button', { class: 'btn primary big', 'data-testid': 'resume', onclick: go(() => app.resumeRun()) },
          h('span', { class: 'bl' }, svgIcon('resume'), 'Fortsetzen'), h('span', { class: 'sub' }, `${MODE_NAMES[st.mode]} · Welle ${st.wave + 1} · ${st.map.name}`)) : null,
        resume.error && app.save.resume ? h('div', { class: 'notice warn small' }, `Gespeicherte Partie nicht ladbar: ${resume.error}`) : null,
        h('button', { class: `btn ${st ? '' : 'primary'} big he-menu`, 'data-testid': 'hero-endless', 'data-autofocus': '', onclick: go(() => app.go('heroSetup')) }, h('span', { class: 'bl' }, svgIcon('star'), 'Helden-Endlos'), h('span', { class: 'sub' }, `Frei steuerbarer Held · Beute, Laden, Skillbaum · Bestwelle ${app.save.progress.heroMeta.bestWave}`)),
        h('div', { class: 'row' },
          h('button', { class: 'btn', 'data-testid': 'editor', onclick: go(() => app.go('editor')) }, svgIcon('pen'), 'Karteneditor'),
          h('button', { class: 'btn', 'data-testid': 'records', onclick: go(() => app.go('records')) }, svgIcon('trophy'), `Erfolge ${app.save.progress.achievements.length}`)),
        h('div', { class: 'row' },
          h('button', { class: 'btn', 'data-testid': 'help', onclick: go(() => app.go('help')) }, svgIcon('book'), 'Hilfe & Kodex'),
          h('button', { class: 'btn', 'data-testid': 'settings', onclick: go(() => app.go('settings')) }, svgIcon('gear'), 'Einstellungen')),
      ),
      h('p', { class: 'muted small foot' }, `Version ${__APP_VERSION__} · Einzelspieler, offline spielbar · Alle Bestwerte werden nur lokal gespeichert.`),
    ));
  return el;
}
