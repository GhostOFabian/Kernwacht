import { GameApplication } from './app/GameApplication.ts';

// Einstiegspunkt. Fehler beim Start werden verständlich angezeigt statt eines leeren Bildschirms.

function fatal(msg: string): void {
  const el = document.createElement('div');
  el.className = 'fatal';
  el.setAttribute('role', 'alert');
  el.innerHTML = '<h1>KERNWACHT konnte nicht starten</h1><p></p><p class="muted">Benötigt wird ein aktueller Browser mit WebGL2 (Chrome, Edge, Firefox, Safari ab 15).</p>';
  (el.querySelector('p') as HTMLElement).textContent = msg;
  document.body.appendChild(el);
}

function boot(): void {
  const canvas = document.getElementById('game') as HTMLCanvasElement | null;
  const ui = document.getElementById('ui');
  if (!canvas || !ui) {
    fatal('Seitenstruktur unvollständig.');
    return;
  }
  try {
    const app = new GameApplication(canvas, ui);
    (window as unknown as { __kernwacht?: GameApplication }).__kernwacht = app;
    app.go('menu');
  } catch (err) {
    console.error(err);
    fatal(err instanceof Error ? err.message : String(err));
  }
  if ('serviceWorker' in navigator && location.protocol === 'https:' && window.top === window.self) {
    navigator.serviceWorker.register('./sw.js').catch((e: unknown) => console.warn('[PWA] Service Worker nicht registriert', e));
  }
}

window.addEventListener('error', (e) => console.error('[Fehler]', e.error ?? e.message));
boot();
