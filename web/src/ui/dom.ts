// Kleines, typisiertes DOM-Hilfsmodul (ersetzt ein UI-Framework).

type Child = Node | string | number | null | undefined | false | Child[];

export interface Attrs {
  class?: string;
  id?: string;
  style?: string;
  title?: string;
  role?: string;
  type?: string;
  value?: string;
  disabled?: boolean;
  tabindex?: number;
  'aria-label'?: string;
  'aria-pressed'?: string;
  'aria-live'?: string;
  'data-testid'?: string;
  [key: `data-${string}`]: string | undefined;
  onclick?: (e: MouseEvent) => void;
  oninput?: (e: Event) => void;
  onchange?: (e: Event) => void;
  onkeydown?: (e: KeyboardEvent) => void;
  onpointerenter?: (e: PointerEvent) => void;
  onpointerleave?: (e: PointerEvent) => void;
  [key: string]: unknown;
}

export function h<K extends keyof HTMLElementTagNameMap>(tag: K, attrs: Attrs | null = null, ...children: Child[]): HTMLElementTagNameMap[K] {
  const el = document.createElement(tag);
  if (attrs) {
    for (const [k, v] of Object.entries(attrs)) {
      if (v === undefined || v === null || v === false) continue;
      if (k.startsWith('on') && typeof v === 'function') {
        el.addEventListener(k.slice(2), v as EventListener);
      } else if (k === 'class') el.className = String(v);
      else if (k === 'style') el.setAttribute('style', String(v));
      else if (k === 'value' && 'value' in el) (el as HTMLInputElement).value = String(v);
      else if (k === 'disabled') (el as HTMLButtonElement).disabled = Boolean(v);
      else if (v === true) el.setAttribute(k, '');
      else el.setAttribute(k, String(v));
    }
  }
  append(el, children);
  return el;
}

/** Hängt Kinder an (null/false werden übersprungen). */
export function put(el: Element, ...children: Child[]): void {
  append(el, children);
}

function append(el: Element, children: Child[]): void {
  for (const c of children) {
    if (c === null || c === undefined || c === false) continue;
    if (Array.isArray(c)) append(el, c);
    else if (c instanceof Node) el.appendChild(c);
    else el.appendChild(document.createTextNode(String(c)));
  }
}

export function clear(el: Element): void {
  while (el.firstChild) el.removeChild(el.firstChild);
}

/** Setzt Text nur bei Änderung (vermeidet unnötige Layoutarbeit im HUD). */
export function setText(el: Element, text: string): void {
  if (el.textContent !== text) el.textContent = text;
}

export function toggleClass(el: Element, cls: string, on: boolean): void {
  if (el.classList.contains(cls) !== on) el.classList.toggle(cls, on);
}

export function fmt(n: number): string {
  return Math.round(n).toLocaleString('de-DE');
}

export function fmtNum(n: number, digits = 1): string {
  return n.toLocaleString('de-DE', { maximumFractionDigits: digits, minimumFractionDigits: 0 });
}

export function pct(n: number): string {
  return `${Math.round(n * 100)} %`;
}
